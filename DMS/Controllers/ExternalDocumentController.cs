using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DMS.Controllers
{
    public class ExternalDocumentController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        public ExternalDocumentController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private ExternalDocumentRepo externalDocumentRepo = ExternalDocumentRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/ExternalDocument/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("EXTERNALDOCUMENT-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("EXTERNALDOCUMENT-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("EXTERNALDOCUMENT-DELETE");
            ViewData["Delete-FilePath"] = HttpContext.Session.GetString("functionList").Contains("EXTERNALDOCUMENT-DELETE-FILEPATH");
            ViewData["Download-FilePath"] = HttpContext.Session.GetString("functionList").Contains("EXTERNALDOCUMENT-DOWNLOAD-FILEPATH");

            ViewData["Title"] = "External Document";

            return View();
        }

        public JsonResult GetByKey(ExternalDocument data)
        {
            try
            {
                ExternalDocument result = externalDocumentRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(ExternalDocument data, bool initialMode)
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

                string division = GetLoginDivision();
                int? departmentId = GetLoginDepartmentId();

                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = "" };
                    return Ok(jsonData);
                }
                else
                {
                    var listData = externalDocumentRepo.Search(data, division, departmentId, db, pageNumber, pageSize);
                    var dataCount = externalDocumentRepo.Search(data, division, departmentId, db, null, null).Count;
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

        public async Task<JsonResult> AddEditAsync(string screenMode, ExternalDocument data)
        {
            DBResult result = null;
            string folderName = "/Upload/";
            string webRootPath = Environment.WebRootPath;

            // Path file lama (sebelum di-overwrite di bawah kalau ada file baru
            // di-upload) - dulu dihapus fisik SEBELUM tahu hasil Update, jadi
            // kalau Update gagal (mis. validasi DOCUMENT_NAME kosong di
            // sp_ExternalDocument_Update), file lama sudah terlanjur hilang
            // padahal baris DB masih menunjuk ke path itu (SP gagal = FILE_PATH
            // di DB tidak berubah) - referensinya jadi rusak. Sekarang dihapus
            // HANYA setelah Update sukses, pola sama seperti
            // DocumentMaintenanceController.AddEditAsync (request Hendra
            // 2026-08-16). Modul ini TIDAK punya konsep Approved/Published
            // seperti DocumentMaintenance (STATUS di sini cuma Berlaku/Tidak
            // Berlaku, semua baris memang boleh diedit bebas by design) -
            // jadi tidak perlu guard status, cuma urutan hapus-nya yang salah.
            string previousFilePath = data.FILE_PATH;

            try
            {
                if (Request.Form.Files.Count > 0)
                {
                    IFormFile file = Request.Form.Files[0];
                    MSystem mSystem = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "EXTERNAL_DOCUMENT" }, db);

                    string extension = Path.GetExtension(file.FileName);
                    string path = folderName + mSystem.SYSTEM_VALUE.Trim();
                    string pathSave = webRootPath + folderName + mSystem.SYSTEM_VALUE.Trim();
                    string documentname = data.DOCUMENT_NAME;
                    documentname = documentname.Replace(" ", "_").Replace("/", "_");
                    string fileName = documentname + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;
                    string finalPath = pathSave + fileName;

                    if (!Directory.Exists(pathSave))
                    {
                        Directory.CreateDirectory(pathSave);
                    }

                    data.FILE_PATH = path + fileName;
                    using (var stream = new FileStream(finalPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                }

                data.DIVISION = GetLoginDivision();
                data.DEPARTMENT_ID = GetLoginDepartmentId();

                if (screenMode == "ADD")
                {
                    result = externalDocumentRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = externalDocumentRepo.Update(data, GetLoginUsername(), db);
                }

                bool fileReplaced = previousFilePath != null && previousFilePath != data.FILE_PATH;
                if (result.status && fileReplaced)
                {
                    string oldPhysicalPath = webRootPath + previousFilePath.Trim();
                    if (System.IO.File.Exists(oldPhysicalPath))
                    {
                        System.IO.File.Delete(oldPhysicalPath);
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(ExternalDocument data, string path)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                result = externalDocumentRepo.Delete(data, GetLoginUsername(), db);

                if (result.status)
                {
                    if (data.FILE_PATH != null)
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

        public JsonResult RemoveAttachment(ExternalDocument data)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                if (data.FILE_PATH != null)
                {
                    string pathCheck = webRootPath + data.FILE_PATH.Trim();
                    if (System.IO.File.Exists(pathCheck))
                    {
                        System.IO.File.Delete(pathCheck);
                    }

                    result = externalDocumentRepo.RemoveAttachment(data, GetLoginUsername(), db);
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

        //Select2 dropdown untuk field "Pengaruh Terhadap Dokumen Internal" - sumber dari
        //Document Masterlist (dokumen internal current/approved), bukan tabel eksternal
        public JsonResult GetImpactedDocument(string q, string pageLimit, string page)
        {
            try
            {
                IList<DocumentMasterlist> dataList = documentMaintenanceRepo.GetMasterlist(db);

                if (!string.IsNullOrEmpty(q))
                {
                    dataList = dataList.Where(x =>
                        (x.DOCUMENT_CODE != null && x.DOCUMENT_CODE.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                        (x.DOCUMENT_TRANSACTION_NAME != null && x.DOCUMENT_TRANSACTION_NAME.Contains(q, StringComparison.OrdinalIgnoreCase))
                    ).ToList();
                }

                var list = new List<Select2>();
                foreach (var data in dataList.Take(int.Parse(pageLimit)))
                {
                    list.Add(new Select2() { text = data.DOCUMENT_CODE + " - " + data.DOCUMENT_TRANSACTION_NAME, id = data.DOCUMENT_TRANSACTION_ID.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel()
        {
            IList<ExternalDocument> listData = externalDocumentRepo.Search(new ExternalDocument(), GetLoginDivision(), GetLoginDepartmentId(), db, null, null);

            var memoryStream = new MemoryStream();
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("External Document");

            string[] headers = {
                "No", "Document Name", "External Type", "Revision/Edition", "Received Date",
                "Storage Location", "Storage PIC", "Review Frequency", "Review Source",
                "Last Check Date", "Next Review Date", "Status", "Internal Impact", "Impacted Document"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (ExternalDocument item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_NAME);
                row.CreateCell(2).SetCellValue(item.EXTERNAL_TYPE);
                row.CreateCell(3).SetCellValue(item.REVISION_EDITION);
                row.CreateCell(4).SetCellValue(item.RECEIVED_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(5).SetCellValue(item.STORAGE_LOCATION);
                row.CreateCell(6).SetCellValue(item.STORAGE_PIC);
                row.CreateCell(7).SetCellValue(item.REVIEW_FREQUENCY);
                row.CreateCell(8).SetCellValue(item.REVIEW_SOURCE);
                row.CreateCell(9).SetCellValue(item.LAST_CHECK_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(10).SetCellValue(item.NEXT_REVIEW_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(11).SetCellValue(item.STATUS_DISPLAY);
                row.CreateCell(12).SetCellValue(item.INTERNAL_IMPACT_DISPLAY);
                row.CreateCell(13).SetCellValue(string.Join(" - ", new[] { item.IMPACTED_DOCUMENT_CODE, item.IMPACTED_DOCUMENT_NAME }.Where(x => !string.IsNullOrEmpty(x))));

                rowIndex++;
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            workbook.Write(memoryStream);
            memoryStream.Position = 0;

            return File(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "EXTERNAL-DOCUMENT-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
