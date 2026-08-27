using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class DocumentMasterController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        public DocumentMasterController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentMaster/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-DELETE");
            ViewData["Delete-FilePath"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-DELETE-FILEPATH");
            ViewData["Download-FilePath"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-DOWNLOAD-FILEPATH");

            ViewData["Title"] = "Document Category";

            // Kode dokumen yang punya master template pengesahan (wwwroot/document/Template/{CODE}.xls
            // atau .xlsx - PDM & PRO ditambahkan sebagai .xlsx 2026-08-18) - dipakai grid untuk
            // menampilkan menu "Download Template" hanya di baris yang memang punya filenya, tanpa
            // perlu hardcode daftar kode di JS. Scan .xls & .xlsx terpisah (bukan cuma "*.xls") -
            // Directory.GetFiles di Windows kadang salah ikut match "*.xlsx" lewat pattern "*.xls"
            // (bug lama 8.3 short-filename di FindFirstFile), tapi DownloadTemplate tetap butuh tahu
            // ekstensi sebenarnya per file, jadi tidak bisa diandalkan.
            string templateFolder = Path.Combine(Environment.WebRootPath, "document", "Template");
            var templateFiles = Directory.Exists(templateFolder)
                ? Directory.GetFiles(templateFolder, "*.xls")
                    .Concat(Directory.GetFiles(templateFolder, "*.xlsx"))
                    .GroupBy(f => Path.GetFileNameWithoutExtension(f))
                    .Select(g => g.First())
                    .ToList()
                : new List<string>();
            var templateCodes = templateFiles.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
            ViewData["TemplateCodes"] = System.Text.Json.JsonSerializer.Serialize(templateCodes);

            // Info per kategori (nama file + ukuran) utk kolom "File Path" di grid - diganti dari
            // field FILE_PATH generik (attachment upload lewat form Add/Edit, sering basi/nunjuk ke
            // file yang sudah tidak ada) supaya kolom ini benar-benar mencerminkan file master
            // template pengesahan yang dipakai GeneratePengesahanPdf, bukan attachment lepas (request
            // Hendra 2026-08-19).
            var templateInfo = templateFiles.ToDictionary(
                f => Path.GetFileNameWithoutExtension(f),
                f => new
                {
                    fileName = Path.GetFileName(f),
                    sizeKb = Math.Ceiling(new FileInfo(f).Length / 1024.0)
                });
            ViewData["TemplateInfo"] = System.Text.Json.JsonSerializer.Serialize(templateInfo);

            return View();
        }

        public JsonResult GetByKey(DocumentMaster data)
        {
            try
            {
                DocumentMaster result = documentMasterRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(DocumentMaster data, bool initialMode)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int pageNumber = skip / pageSize + 1;
                int recordsTotal = 0;
                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = "" };
                    return Ok(jsonData);
                }
                else
                {
                    var listData = documentMasterRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = documentMasterRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public async Task<JsonResult> AddEditAsync(string screenMode, DocumentMaster data)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                if (Request.Form.Files.Count > 0)
                {
                    // Upload di form ini sekarang langsung jadi file master template
                    // pengesahan (wwwroot/document/Template/{CODE}.xls atau .xlsx) yang
                    // dipakai GeneratePengesahanPdf/DownloadTemplate - dulu ditulis ke
                    // attachment generik (Upload/ATTACHMENT/DOCUMENT_MASTER) yang lepas
                    // sama sekali dari mekanisme template pengesahan, bikin bingung
                    // (request Hendra 2026-08-19).
                    IFormFile file = Request.Form.Files[0];
                    string extension = Path.GetExtension(file.FileName).ToLower();

                    if (extension != ".xls" && extension != ".xlsx")
                    {
                        return Json(new { status = false, message = "Only .xls or .xlsx files are allowed for the signing template." });
                    }

                    string code = (data.DOCUMENT_CODE ?? "").Trim();
                    if (string.IsNullOrEmpty(code) || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9]+$"))
                    {
                        return Json(new { status = false, message = "Invalid document code." });
                    }

                    string templateFolder = Path.Combine(webRootPath, "document", "Template");
                    if (!Directory.Exists(templateFolder))
                    {
                        Directory.CreateDirectory(templateFolder);
                    }

                    // Hapus varian ekstensi lain punya kode yang sama supaya tidak ambigu -
                    // DownloadTemplate/GeneratePengesahanPdf cek .xls dulu baru .xlsx, jadi
                    // kalau dua-duanya ada, file lama yang tidak dipakai lagi bisa
                    // ketimpa/terlihat seolah upload baru tidak berlaku.
                    foreach (string otherExt in new[] { ".xls", ".xlsx" })
                    {
                        if (otherExt == extension) continue;
                        string otherPath = Path.Combine(templateFolder, code + otherExt);
                        if (System.IO.File.Exists(otherPath))
                        {
                            System.IO.File.Delete(otherPath);
                        }
                    }

                    string finalPath = Path.Combine(templateFolder, code + extension);
                    using (var stream = new FileStream(finalPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                }

                if (screenMode == "ADD")
                {
                    result = documentMasterRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = documentMasterRepo.Update(data, GetLoginUsername(), db);
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Hapus file master template pengesahan (wwwroot/document/Template/{CODE}.xls atau
        // .xlsx) - dipakai tombol trash di kolom "Template" pada form Add/Edit, pasangan dari
        // upload di AddEditAsync (request Hendra 2026-08-19).
        public JsonResult RemoveSigningTemplate(string code)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(code) || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9]+$"))
                {
                    return Json(new { status = false, message = "Invalid document code." });
                }

                string webRootPath = Environment.WebRootPath;
                bool deleted = false;
                foreach (string ext in new[] { ".xls", ".xlsx" })
                {
                    string path = Path.Combine(webRootPath, "document", "Template", code + ext);
                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                        deleted = true;
                    }
                }

                return Json(new { status = true, message = deleted ? "Template removed successfully." : "No template file found." });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(DocumentMaster data, string path)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                result = documentMasterRepo.Delete(data, GetLoginUsername(), db);

                if (result.status)
                {
                    if (data.FILE_PATH != null)
                    {
                        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                        //Delete File
                        if (System.IO.File.Exists(pathCheck))
                        {
                            System.IO.File.Delete(pathCheck);
                        }
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult RemoveAttachment(DocumentMaster data)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                if (data.FILE_PATH != null)
                {
                    // DB dulu, file fisik belakangan - kalau dibalik dan
                    // RemoveAttachment gagal, file sudah hilang padahal baris
                    // DB masih menunjuk ke situ (request Hendra 2026-08-16).
                    result = documentMasterRepo.RemoveAttachment(data, GetLoginUsername(), db);

                    if (result.status)
                    {
                        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                        if (System.IO.File.Exists(pathCheck))
                        {
                            System.IO.File.Delete(pathCheck);
                        }
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadAttachment(string path)
        {
            string webRootPath = Environment.WebRootPath;
            string fullPath = webRootPath + path;
            string[] split = path.Split("/");
            string fileName = split[4];

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

            return File(bytes, "application/force-download", fileName);
        }

        // Download file master template pengesahan (wwwroot/document/Template/{CODE}.xls atau
        // .xlsx) - ini file yang benar-benar dipakai GeneratePengesahanPdf & ValidateTemplateConfiguration
        // di DocumentMaintenanceController, berbeda dari FILE_PATH attachment generik di atas.
        // Cek .xls dulu baru .xlsx (PDM & PRO ditambahkan sebagai .xlsx 2026-08-18).
        public IActionResult DownloadTemplate(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9]+$"))
            {
                return NotFound();
            }

            string webRootPath = Environment.WebRootPath;
            string xlsPath = Path.Combine(webRootPath, "document", "Template", code + ".xls");
            string xlsxPath = Path.Combine(webRootPath, "document", "Template", code + ".xlsx");

            string fullPath;
            string fileName;
            string contentType;
            if (System.IO.File.Exists(xlsPath))
            {
                fullPath = xlsPath;
                fileName = code + ".xls";
                contentType = "application/vnd.ms-excel";
            }
            else if (System.IO.File.Exists(xlsxPath))
            {
                fullPath = xlsxPath;
                fileName = code + ".xlsx";
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            }
            else
            {
                return NotFound();
            }

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
            return File(bytes, contentType, fileName);
        }

        public JsonResult GetDocumentCode(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaster oDocumentMaster = new DocumentMaster();
                if (q != null)
                    oDocumentMaster.DOCUMENT_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentMaster> dataList = documentMasterRepo.Search(oDocumentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_CODE, id = data.DOCUMENT_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDocumentName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaster oDocumentMaster = new DocumentMaster();
                if (q != null)
                    oDocumentMaster.DOCUMENT_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentMaster> dataList = documentMasterRepo.Search(oDocumentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_NAME, id = data.DOCUMENT_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

    }
}
