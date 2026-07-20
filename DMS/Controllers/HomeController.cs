using DMS.Common.Controllers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DMS.Controllers
{
    public class HomeController : BaseController
    {
        DBContext db;
        private readonly ILogger<HomeController> _logger;

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private UserDashboardRepo userDashboardRepo = UserDashboardRepo.Instance;
        private UserRepo userRepo = UserRepo.Instance;

        public HomeController(DBContext db, ILogger<HomeController> logger)
        {
            this.db = db;
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth", new { param = "/Home/Index" });
            }

            ViewData["Title"] = "Dashboard";

            return View();
        }

        public JsonResult GetDashboardSummary()
        {
            try
            {
                string username = GetLoginUsername();

                IList<DocumentMaintenance> docs = documentMaintenanceRepo.Search(new DocumentMaintenance(), username, db, null, null);
                IList<DocumentControlMaintenance> ctrlDocs = userDashboardRepo.Search(new DocumentControlMaintenance(), username, db, null, null);
                int totalUsers = userRepo.Search(new User(), db, null, null).Count;

                var statusSummary = docs
                    .GroupBy(x => x.STATUS_DISPLAY ?? "Unknown")
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList();

                var divisionSummary = docs
                    .GroupBy(x => x.DIVISION != null ? x.DIVISION + (x.DIVISION_NAME != null ? " - " + x.DIVISION_NAME : "") : "Unassigned")
                    .Select(g => new { division = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList();

                var recentDocuments = docs
                    .OrderByDescending(x => x.CHANGED_DT ?? x.CREATED_DT)
                    .Take(5)
                    .Select(x => new
                    {
                        documentCode = x.DOCUMENT_CODE,
                        documentName = x.DOCUMENT_TRANSACTION_NAME,
                        status = x.STATUS_DISPLAY,
                        statusCode = x.STATUS,
                        changedDate = x.CHANGED_DT ?? x.CREATED_DT
                    })
                    .ToList();

                var ctrlStatusSummary = ctrlDocs
                    .GroupBy(x => x.STATUS_VAL ?? "Unknown")
                    .Select(g => new { status = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList();

                return Json(new
                {
                    status = true,
                    data = new
                    {
                        totalDocuments = docs.Count,
                        totalApproved = docs.Count(x => x.STATUS == "1"),
                        totalNew = docs.Count(x => x.STATUS == "0"),
                        totalRejected = docs.Count(x => x.STATUS == "2"),
                        totalUsers = totalUsers,
                        totalDocumentControl = ctrlDocs.Count,
                        statusSummary,
                        divisionSummary,
                        recentDocuments,
                        ctrlStatusSummary
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
