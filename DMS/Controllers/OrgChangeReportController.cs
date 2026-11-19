using DMS.Common.Controllers;
using DMS.Models;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    /// <summary>
    /// Reports > Organization Change History (menu M00008-05, 2026-11-19): the valid changes of
    /// the Organization Change register, as audit evidence (QMS answer no. 8). Read only.
    /// </summary>
    public class OrgChangeReportController : BaseController
    {
        public const string MenuUrl = "/OrgChangeReport/Index";
        private readonly DBContext db;

        public OrgChangeReportController(DBContext db)
        {
            this.db = db;
        }

        private bool Allowed() => GetLoginUsername() != null && HasMenuAccess(MenuUrl);

        public IActionResult Index()
        {
            if (GetLoginUsername() == null) return Redirect("/Auth/Login");
            if (!Allowed())
            {
                Response.StatusCode = 403;
                return View("Error403");
            }
            ViewData["Title"] = "Organization Change History";
            return View();
        }

        // valid changes whose effective date is in the period; one without a date is always listed
        private List<OrgChangeService.Change> Load(DateTime? from, DateTime? to) =>
            OrgChangeService.List(db, false)
                .Where(c => c.EffectiveDate == null
                    || ((from == null || c.EffectiveDate.Value.Date >= from.Value.Date) && (to == null || c.EffectiveDate.Value.Date <= to.Value.Date)))
                .OrderBy(c => c.EffectiveDate ?? DateTime.MaxValue).ThenBy(c => c.Id)
                .ToList();

        [HttpGet]
        public IActionResult Data(DateTime? dateFrom, DateTime? dateTo)
        {
            if (!Allowed()) return StatusCode(403);
            var rows = Load(dateFrom, dateTo).Select(c => new
            {
                c.Id,
                EffectiveDate = c.EffectiveDate,
                c.TypeLabel,
                c.LevelLabel,
                Old = OrgChangeReportService.OldText(c),
                New = OrgChangeReportService.NewText(c),
                c.HrReference,
                c.HasAttachment,
                c.Description,
                State = OrgChangeReportService.StateText(c),
                AnyActiveOld = c.Impacts.Any(i => !i.CodeKept && i.MasterState == "Active"),
                c.CreatedBy,
                c.CreatedDt
            });
            return Json(new { status = true, data = rows });
        }

        [HttpGet]
        public IActionResult Excel(DateTime? dateFrom, DateTime? dateTo)
        {
            if (!Allowed()) return StatusCode(403);
            using var wb = OrgChangeReportService.BuildWorkbook(Load(dateFrom, dateTo), dateFrom, dateTo);
            return File(wb.SaveDocument(DevExpress.Spreadsheet.DocumentFormat.Xlsx),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ORGANIZATION-CHANGE-HISTORY-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        [HttpGet]
        public IActionResult Pdf(DateTime? dateFrom, DateTime? dateTo)
        {
            if (!Allowed()) return StatusCode(403);
            using var wb = OrgChangeReportService.BuildWorkbook(Load(dateFrom, dateTo), dateFrom, dateTo);
            using var stream = new MemoryStream();
            wb.ExportToPdf(stream);
            return File(stream.ToArray(), "application/pdf");
        }
    }
}
