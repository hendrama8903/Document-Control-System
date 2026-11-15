using DMS.Common.Controllers;
using DMS.Hubs;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Hangfire;
using MDC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NPOI.POIFS.FileSystem;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DMS.Controllers
{
    public class DocumentReportController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;

        public DocumentReportController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
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

                if (!menuURLList.Contains("/DocumentReport/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Title"] = "Document Distribution";

            return View();
        }

        public ActionResult Search(DocumentReport data, bool initialMode)
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
                    var listData = documentMaintenanceRepo.SearchReport(data, db, pageNumber, pageSize);
                    var dataCount = documentMaintenanceRepo.SearchReport(data, db, null, null).Count;
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

        // Export to Excel with the same columns as the grid (pattern of DocumentMasterlistController.DownloadExcel).
        public IActionResult DownloadExcel()
        {
            if (!(HttpContext.Session.GetString("menuURL") ?? "").Contains("/DocumentReport/Index"))
            {
                return StatusCode(403);
            }

            IList<DocumentReport> listData = documentMaintenanceRepo.SearchReport(new DocumentReport(), db, null, null);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Distribution");

            string[] headers = {
                "No", "Document Code", "Document Name", "Document Date", "Next Review Date", "Division", "Department",
                "Revision", "Published", "Creator", "Distribution Date"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int no = 1;
            foreach (DocumentReport item in listData)
            {
                IRow row = sheet.CreateRow(no);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_CODE ?? "");
                row.CreateCell(2).SetCellValue(item.DOCUMENT_TRANSACTION_NAME ?? "");
                row.CreateCell(3).SetCellValue(item.DOCUMENT_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(4).SetCellValue(item.NEXT_REVIEW_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(5).SetCellValue(item.DIVISION != null ? item.DIVISION + " - " + item.DIVISION_NAME : "");
                row.CreateCell(6).SetCellValue(item.DEPARTMENT_CODE != null ? item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME : "");
                row.CreateCell(7).SetCellValue(item.REVISION ?? 0);
                row.CreateCell(8).SetCellValue(item.PUBLISH_FLAG == 1 ? "Yes" : "No");
                row.CreateCell(9).SetCellValue(item.DOCUMENT_CREATOR ?? "");
                row.CreateCell(10).SetCellValue(item.DISTRIBUTION_DATE?.ToString("dd-MM-yyyy") ?? "");
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            using var memoryStream = new MemoryStream();
            workbook.Write(memoryStream);
            return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DOCUMENT-DISTRIBUTION-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
