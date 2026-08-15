using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Hubs;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Hangfire;
using MDC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Globalization;
using System.Text;
//Recommit DINA
namespace DMS.Controllers
{
    public class DocumentControlDashboardController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;

        public DocumentControlDashboardController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private DocumentControlDashboardRepo DocumentControlDashboardRepo = DocumentControlDashboardRepo.Instance;
        private P4DMaintenanceRepo P4DMaintenanceRepo = P4DMaintenanceRepo.Instance;

        public IActionResult Index(string DOCUMENT_CODE)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                string param = "/DocumentControlDashboard/Index?DOCUMENT_CODE=" + DOCUMENT_CODE;
                return RedirectToAction("Login", "Auth", new { param });
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentControlDashboard/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["DocumentControlAccess"] = GetDocumentAccessControl();


            ViewData["Title"] = "Document Control";

            return View();
        }

        // Rincian per-department untuk popup "Acceptance Detail" (QMS-only, klik
        // dari kolom Acceptance di grid) - reuse SearchDocumentDistribution yang
        // sama persis dipakai popup Distribution di P4D, supaya datanya (termasuk
        // ACCEPTED_FLAG) konsisten (request Hendra 2026-08-15).
        public JsonResult SearchDistributionDetail(int documentTransactionId)
        {
            try
            {
                IList<DocumentDistribution> dataList = P4DMaintenanceRepo.SearchDocumentDistribution(
                    new DocumentDistribution { DOCUMENT_TRANSACTION_ID = documentTransactionId }, db, null, null);

                return Json(new { status = true, data = dataList });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(DocumentControlMaintenance data, bool initialMode)
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
                    // OPERATION_TYPE is forced to 1 (real P4D registrations only)
                    // inside DocumentControlDashboardRepo.Search - see the comment
                    // there for why OPERATION_TYPE=2 "Request Document" rows must not
                    // show up here (request user 2026-08-11).
                    var listData = DocumentControlDashboardRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = DocumentControlDashboardRepo.Search(data, db, null, null).Count;
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

        public IActionResult DownloadDocument(string path)
        {
            string webRootPath = Environment.WebRootPath;
            string fullPath = webRootPath + path;
            string[] split = path.Split("/");
            string fileName = split[4];

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

            return File(bytes, "application/force-download", fileName);
        }

        public IActionResult DownloadExcel()
        {
            IList<DocumentControlDashboardDocument> listData = DocumentControlDashboardRepo.Search(
                new DocumentControlMaintenance(), db, null, null);

            var memoryStream = new MemoryStream();
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Control");

            string[] headers = {
                "No", "Document No", "Document Name", "Category", "Revision", "Status",
                "Effective Date", "Next Review", "Document Owner", "Acceptance",
                "Department", "Classification", "Last Updated"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (DocumentControlDashboardDocument item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_CODE);
                row.CreateCell(2).SetCellValue(item.DOCUMENT_NAME);
                row.CreateCell(3).SetCellValue(item.CATEGORY_CODE);
                row.CreateCell(4).SetCellValue(item.REVISION ?? 0);
                row.CreateCell(5).SetCellValue(item.DOC_STATUS_VAL);
                row.CreateCell(6).SetCellValue(item.DOCUMENT_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(7).SetCellValue(item.NEXT_REVIEW_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(8).SetCellValue(item.OWNER_FULL_NAME);
                row.CreateCell(9).SetCellValue((item.ACK_DONE ?? 0) + "/" + (item.ACK_TOTAL ?? 0));
                row.CreateCell(10).SetCellValue(item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME);
                row.CreateCell(11).SetCellValue(item.CLASSIFIED_VAL);
                row.CreateCell(12).SetCellValue((item.CHANGED_DT ?? item.CREATED_DT)?.ToString("dd-MM-yyyy HH:mm") ?? "");

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
                "DOCUMENT-CONTROL-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

    }
}
