using DMS.Common.Controllers;
using DMS.Models;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    /// <summary>
    /// Print Request Report (menu M00008-04, 2026-11-19): approved Print Requests in the
    /// Master List layout - QTY and request date per requesting division. Screen, Excel and
    /// PDF come from PrintRequestMatrixService.
    /// </summary>
    public class PrintRequestReportController : BaseController
    {
        private readonly DBContext db;

        public PrintRequestReportController(DBContext db)
        {
            this.db = db;
        }

        private bool HasMenuAccess() =>
            HttpContext.Session.GetString("USERNAME") != null
            && (HttpContext.Session.GetString("menuURL") ?? "").Contains("/PrintRequestReport/Index");

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null) return RedirectToAction("Login", "Auth");
            if (!HasMenuAccess())
            {
                Response.StatusCode = 403;
                return View("Error403");
            }
            ViewData["Title"] = "Print Request Report";
            return View();
        }

        [HttpGet]
        public IActionResult Data(DateTime? dateFrom, DateTime? dateTo)
        {
            if (!HasMenuAccess()) return StatusCode(403);
            try
            {
                return Json(PrintRequestMatrixService.Load(db, dateFrom, dateTo));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet]
        public IActionResult Excel(DateTime? dateFrom, DateTime? dateTo, string division, string department, string sort, string dir)
        {
            if (!HasMenuAccess()) return StatusCode(403);
            using var workbook = PrintRequestMatrixService.BuildWorkbook(LoadSorted(dateFrom, dateTo, division, department, sort, dir));
            byte[] bytes = workbook.SaveDocument(DevExpress.Spreadsheet.DocumentFormat.Xlsx);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "PRINT-REQUEST-REPORT-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        [HttpGet]
        public IActionResult Pdf(DateTime? dateFrom, DateTime? dateTo, string division, string department, string sort, string dir)
        {
            if (!HasMenuAccess()) return StatusCode(403);
            using var workbook = PrintRequestMatrixService.BuildWorkbook(LoadSorted(dateFrom, dateTo, division, department, sort, dir));
            using var stream = new MemoryStream();
            workbook.ExportToPdf(stream);
            return File(stream.ToArray(), "application/pdf");
        }

        // the export follows the filter and order chosen on screen
        private PrintRequestMatrixService.Report LoadSorted(DateTime? dateFrom, DateTime? dateTo, string division, string department, string sort, string dir)
        {
            var report = PrintRequestMatrixService.Load(db, dateFrom, dateTo, division, department);
            bool descending = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase);
            foreach (var g in report.Groups) PrintRequestMatrixService.Sort(g.Rows, sort, descending);
            return report;
        }
    }
}
