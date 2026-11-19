using DMS.Common.Controllers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    public class DocumentReceiptReportController : BaseController
    {
        DBContext db;

        public DocumentReceiptReportController(DBContext db)
        {
            this.db = db;
        }

        private DocumentReceiptReportRepo documentReceiptReportRepo = DocumentReceiptReportRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentReceiptReport/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Title"] = "Document Receipt Report";

            return View();
        }

        public ActionResult Search(DocumentReceiptReport data, bool initialMode)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int pageNumber = pageSize > 0 ? (skip / pageSize) + 1 : 1;
                int recordsTotal = 0;
                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = new List<object>() };
                    return Ok(jsonData);
                }
                else
                {
                    var listData = documentReceiptReportRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = documentReceiptReportRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = 0,
                    recordsFiltered = 0,
                    recordsTotal = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        // ------------------------------------------------------------------ Tanda Terima Dokumen
        // 2026-11-19: Excel and PDF of form QCD/FR-QMS-00/006 in the Master List style
        // (DocumentReceiptFormService / QmsFormSheet) - one sheet per receiving department,
        // smallest document number first, no logo. Replaces the plain NPOI export and the
        // PdfSharp drawing. publishHistoryIds = the rows to print; none = every receipt.

        private bool HasMenuAccess() =>
            HttpContext.Session.GetString("USERNAME") != null
            && (HttpContext.Session.GetString("menuURL") ?? "").Contains("/DocumentReceiptReport/Index");

        private List<DocumentReceiptReport> LoadReceipts(List<int> publishHistoryIds)
        {
            var all = documentReceiptReportRepo.Search(new DocumentReceiptReport(), db, null, null);
            if (publishHistoryIds == null || publishHistoryIds.Count == 0) return all.ToList();
            var wanted = new HashSet<int>(publishHistoryIds);
            return all.Where(x => x.PUBLISH_HISTORY_ID.HasValue && wanted.Contains(x.PUBLISH_HISTORY_ID.Value)).ToList();
        }

        public IActionResult DownloadExcel(List<int> publishHistoryIds)
        {
            if (!HasMenuAccess()) return StatusCode(403);
            using var workbook = DocumentReceiptFormService.BuildWorkbook(LoadReceipts(publishHistoryIds));
            byte[] bytes = workbook.SaveDocument(DevExpress.Spreadsheet.DocumentFormat.Xlsx);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "TANDA-TERIMA-DOKUMEN-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        public IActionResult PrintReceipt(List<int> publishHistoryIds)
        {
            if (!HasMenuAccess()) return StatusCode(403);
            var rows = LoadReceipts(publishHistoryIds);
            if (rows.Count == 0) return BadRequest("No receipt found");
            using var workbook = DocumentReceiptFormService.BuildWorkbook(rows);
            using var stream = new MemoryStream();
            workbook.ExportToPdf(stream);
            // inline (no download name): opens in the new tab, like the Master List PDF
            return File(stream.ToArray(), "application/pdf");
        }
    }
}
