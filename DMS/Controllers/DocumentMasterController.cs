using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using DMS.Services;
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

                    // Pemeriksaan penanda pengesahan (DCS_) - error menolak simpan, peringatan
                    // tidak (lihat CheckTemplate untuk cek langsung saat file dipilih di form).
                    TemplateMarkerService.Report report = AnalyzeTemplateFile(file, data.DOCUMENT_ID);
                    if (!report.IsValid)
                    {
                        return Json(new { status = false, message = "Template tidak bisa disimpan: " + string.Join(" ", report.Errors) });
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

        // ---------------------------------------------------------------------
        // Pemeriksaan & preview penanda pengesahan DCS_ (langkah 2 opsi B, 2026-11-06)
        // ---------------------------------------------------------------------

        static DevExpress.Spreadsheet.DocumentFormat FormatOf(string fileName) =>
            Path.GetExtension(fileName).ToLower() == ".xlsx"
                ? DevExpress.Spreadsheet.DocumentFormat.Xlsx
                : DevExpress.Spreadsheet.DocumentFormat.Xls;

        static DevExpress.Spreadsheet.Workbook LoadWorkbook(IFormFile file)
        {
            var workbook = new DevExpress.Spreadsheet.Workbook();
            using (Stream stream = file.OpenReadStream())
            {
                workbook.LoadDocument(stream, FormatOf(file.FileName));
            }
            return workbook;
        }

        // Template tanpa penanda DCS_ masih boleh, ASAL jenis dokumennya sudah punya
        // konfigurasi baris/kolom lama (TB_M_EXCEL_TEMPLATE) - dokumen dari template itu
        // jatuh ke jalur lama. Tanpa keduanya, tanda tangan tidak akan pernah tercetak.
        private TemplateMarkerService.Report AnalyzeTemplateFile(IFormFile file, int? documentId)
            => AnalyzeTemplate(() => LoadWorkbook(file), documentId);

        private TemplateMarkerService.Report AnalyzeTemplate(Func<DevExpress.Spreadsheet.Workbook> load, int? documentId)
        {
            TemplateMarkerService.Report report;
            try
            {
                using (DevExpress.Spreadsheet.Workbook workbook = load())
                {
                    report = TemplateMarkerService.Analyze(workbook);
                }
            }
            catch (Exception)
            {
                report = new TemplateMarkerService.Report();
                report.Errors.Add("File Excel tidak dapat dibaca. Pastikan file tidak rusak dan formatnya .xls/.xlsx.");
                return report;
            }

            if (!report.HasMarkers)
            {
                bool hasLegacyConfig = documentId != null && documentMasterRepo
                    .SearchTemplate(new ExcelTemplateMaster { DOCUMENT_ID = documentId }, db).Count > 0;

                if (hasLegacyConfig)
                {
                    report.Warnings.Add("Template belum berpenanda DCS_. Sistem memakai konfigurasi baris/kolom lama, jadi tata letak kotak pengesahan HARUS sama persis dengan template sebelumnya. Disarankan memasang penanda DCS_.");
                }
                else
                {
                    report.Errors.Add("Template belum berpenanda DCS_ dan jenis dokumen ini belum punya konfigurasi pengesahan, sehingga tanda tangan & data dokumen tidak akan tercetak. Pasang penanda DCS_ di template terlebih dahulu.");
                }
            }

            return report;
        }

        // Path template pengesahan tersimpan ({CODE}.xls dicek dulu, lalu .xlsx - sama seperti
        // DownloadTemplate/GeneratePengesahanPdf). null kalau kode tidak valid / file tidak ada.
        private string FindSavedTemplate(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || !System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Za-z0-9]+$")) return null;
            string folder = Path.Combine(Environment.WebRootPath, "document", "Template");
            return new[] { ".xls", ".xlsx" }
                .Select(ext => Path.Combine(folder, code + ext))
                .FirstOrDefault(System.IO.File.Exists);
        }

        // Kotak POS<id> di template yang jabatannya tidak pernah ada di alur approval
        // kategori ini (alur khusus kategori, atau default level) akan selalu kosong -
        // diperingatkan, tidak diblokir (request Hendra 2026-11-06).
        private void AddWorkflowWarnings(TemplateMarkerService.Report report, int? level, int? documentId)
        {
            if (!report.HasMarkers || level == null) return;

            WorkflowCategoryPositions flow = WorkflowDocRepo.Instance.CategoryPositions(level, documentId, db);
            if (!flow.HasWorkflow)
            {
                report.Warnings.Add($"Belum ada alur approval untuk Document Level {level}. Atur dulu di menu Approval Workflow Setup, kalau tidak dokumen jenis ini tidak bisa dibuat.");
                return;
            }

            var boxKeys = report.Boxes
                .Select(b => b.Split(':')[0].Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (string key in boxKeys)
            {
                int? positionId = TemplateMarkerService.PositionIdOfBox(key);
                if (positionId == null || flow.Positions.ContainsKey(positionId.Value)) continue;

                string name = PositionMasterRepo.Instance.GetByKey(new PositionMaster { POSITION_ID = positionId }, db)?.POSITION_NAME ?? "jabatan " + positionId;
                report.Warnings.Add($"Kotak {key} ({name}) tidak ada di alur approval {(documentId == null ? "level ini" : "jenis dokumen ini")}, jadi kotak itu akan selalu kosong. Tambahkan {name} di Approval Workflow Setup, atau periksa kode POS-nya. Alur saat ini memuat: {string.Join(", ", flow.Positions.Values)}.");
            }
        }

        [HttpPost]
        public JsonResult CheckTemplate(int? documentId, string code, int? level)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
                return Json(new { status = false, message = "Session expired." });

            try
            {
                TemplateMarkerService.Report report;
                if (Request.HasFormContentType && Request.Form.Files.Count > 0)
                {
                    report = AnalyzeTemplateFile(Request.Form.Files[0], documentId);
                }
                else
                {
                    // Tanpa file = periksa template yang sudah tersimpan (dibuka saat Edit).
                    string path = FindSavedTemplate(code);
                    if (path == null) return Json(new { status = false, message = "Template file not found." });
                    report = AnalyzeTemplate(() => { var wb = new DevExpress.Spreadsheet.Workbook(); wb.LoadDocument(path); return wb; }, documentId);
                }
                AddWorkflowWarnings(report, level, documentId);
                return Json(new
                {
                    status = true,
                    hasMarkers = report.HasMarkers,
                    isValid = report.IsValid,
                    errors = report.Errors,
                    warnings = report.Warnings,
                    fields = report.Fields,
                    boxes = report.Boxes,
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Preview lembar pengesahan dengan data & tanda tangan contoh. POST + file = file
        // yang baru dipilih di form (belum disimpan); GET + code = template yang tersimpan.
        public IActionResult PreviewTemplate(string code)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
                return Unauthorized();

            string tempPdf = Path.Combine(Path.GetTempPath(), "dcs_template_preview_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                DevExpress.Spreadsheet.Workbook workbook;
                if (Request.HasFormContentType && Request.Form.Files.Count > 0)
                {
                    workbook = LoadWorkbook(Request.Form.Files[0]);
                }
                else
                {
                    string path = FindSavedTemplate(code);
                    if (path == null) return NotFound("Template file not found.");

                    workbook = new DevExpress.Spreadsheet.Workbook();
                    workbook.LoadDocument(path);
                }

                using (workbook)
                {
                    if (!TemplateMarkerService.HasMarkers(workbook))
                        return BadRequest("Template belum berpenanda DCS_, preview hanya tersedia untuk template berpenanda.");

                    TemplateMarkerService.RenderPreviewPdf(workbook, tempPdf, positionId =>
                        PositionMasterRepo.Instance.GetByKey(new PositionMaster { POSITION_ID = positionId }, db)?.POSITION_NAME);
                }

                byte[] bytes = System.IO.File.ReadAllBytes(tempPdf);
                return File(bytes, "application/pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
            finally
            {
                if (System.IO.File.Exists(tempPdf)) System.IO.File.Delete(tempPdf);
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
                        string pathCheck = Services.UploadPath.Resolve(webRootPath, data.FILE_PATH);
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
                        string pathCheck = Services.UploadPath.Resolve(webRootPath, data.FILE_PATH);
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
            // Hanya file di wwwroot/Upload/ATTACHMENT/ (perbaikan path traversal 2026-11-06)
            string fullPath = Services.UploadPath.Resolve(webRootPath, path);
            if (fullPath == null || !System.IO.File.Exists(fullPath)) return NotFound();
            string fileName = Path.GetFileName(fullPath);

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
