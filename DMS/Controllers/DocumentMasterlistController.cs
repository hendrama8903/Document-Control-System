using DMS.Common.Controllers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DMS.Controllers
{
    public class DocumentMasterlistController : BaseController
    {
        DBContext db;

        public DocumentMasterlistController(DBContext db)
        {
            this.db = db;
        }

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentMasterlist/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Title"] = "Document Masterlist";

            return View();
        }

        public ActionResult Search(bool initialMode)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                int recordsTotal = 0;

                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = "" };
                    return Ok(jsonData);
                }
                else
                {
                    var listData = documentMaintenanceRepo.GetMasterlist(db);
                    recordsTotal = listData.Count;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        //Export masterlist ke Excel - dibangun dari nol (bukan template) supaya mudah
        //dirawat; kolom mengikuti grid di halaman (pola sama seperti
        //P4DMaintenanceController.Download, tapi tulis ke MemoryStream, bukan file fisik
        //sementara - pola sama seperti DocumentMaintenanceController.DownloadReport).
        public IActionResult DownloadExcel()
        {
            IList<DocumentMasterlist> listData = documentMaintenanceRepo.GetMasterlist(db);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Masterlist");

            string[] headers = {
                "No", "Document Code", "Document Name", "Category", "Division", "Department",
                "Classified", "Revision", "Status", "Effective Date", "Last Change", "Owner", "System"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (DocumentMasterlist item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_CODE);
                row.CreateCell(2).SetCellValue(item.DOCUMENT_TRANSACTION_NAME);
                row.CreateCell(3).SetCellValue(item.DOCUMENT_CATEGORY);
                row.CreateCell(4).SetCellValue(item.DIVISION + " - " + item.DIVISION_NAME);
                row.CreateCell(5).SetCellValue(item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME);
                row.CreateCell(6).SetCellValue(item.CLASSIFIED_VAL);
                row.CreateCell(7).SetCellValue(item.REVISION ?? 0);
                row.CreateCell(8).SetCellValue(item.STATUS_DISPLAY);
                row.CreateCell(9).SetCellValue(item.DOCUMENT_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(10).SetCellValue(string.Join(" - ", new[] { item.ITEM_CHANGED, item.REASON }.Where(x => !string.IsNullOrEmpty(x))));
                row.CreateCell(11).SetCellValue(item.DOCUMENT_CREATOR);
                row.CreateCell(12).SetCellValue(item.MGMT_SYSTEM ?? "QMS");

                rowIndex++;
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                workbook.Write(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DOCUMENT-MASTERLIST-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        // ------------------------------------------------------------------ Matrix (form QCD/FR-QMS-00/004)
        // 2026-11-19: the same register as a matrix of revision dates and distribution
        // history per division. Screen, Excel and PDF all come from MasterListMatrixService.

        private bool HasMenuAccess()
        {
            string menuURLList = HttpContext.Session.GetString("menuURL");
            return HttpContext.Session.GetString("USERNAME") != null
                && menuURLList != null && menuURLList.Contains("/DocumentMasterlist/Index");
        }

        [HttpGet]
        public IActionResult MatrixData()
        {
            if (!HasMenuAccess()) return StatusCode(403);
            try
            {
                return Json(MasterListMatrixService.Load(db));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        public IActionResult MatrixExcel(string division, string department, string sort, string dir, string mgmtSystem)
        {
            if (!HasMenuAccess()) return StatusCode(403);
            var report = LoadMatrix(division, department, sort, dir, mgmtSystem);
            using var workbook = MasterListMatrixService.BuildWorkbook(report);
            byte[] bytes = workbook.SaveDocument(DevExpress.Spreadsheet.DocumentFormat.Xlsx);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", MatrixFileName(division, department) + ".xlsx");
        }

        [HttpGet]
        public IActionResult MatrixPdf(string division, string department, string sort, string dir, string mgmtSystem)
        {
            if (!HasMenuAccess()) return StatusCode(403);
            var report = LoadMatrix(division, department, sort, dir, mgmtSystem);
            using var workbook = MasterListMatrixService.BuildWorkbook(report);
            using var stream = new MemoryStream();
            workbook.ExportToPdf(stream);
            // inline (no download name): opens in a new tab, like the other printed forms
            return File(stream.ToArray(), "application/pdf");
        }

        // the export follows the order chosen on screen (click a column header in the matrix)
        private MasterListMatrixService.Report LoadMatrix(string division, string department, string sort, string dir, string mgmtSystem = null)
        {
            var report = MasterListMatrixService.Load(db, division, department, mgmtSystem);
            bool descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            foreach (var g in report.Groups) MasterListMatrixService.Sort(g.Documents, sort, descending);
            return report;
        }

        private static string MatrixFileName(string division, string department)
        {
            string scope = !string.IsNullOrWhiteSpace(department) ? department.Trim()
                : !string.IsNullOrWhiteSpace(division) ? division.Trim() : "ALL";
            return "MASTER-LIST-" + scope + "-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        }
    }
}
