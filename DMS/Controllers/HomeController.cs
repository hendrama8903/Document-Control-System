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
        private P4DMaintenanceRepo p4dMaintenanceRepo = P4DMaintenanceRepo.Instance;

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
                bool isQms = GetDocumentAccessControl() == true;

                return isQms ? GetQmsDashboardSummary() : GetUserDashboardSummary(username);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Dashboard untuk user tanpa akses QMS: hanya dokumen yang DIA BUAT SENDIRI
        // di DocumentMaintenance (bukan seluruh dokumen departemen/divisi).
        private JsonResult GetUserDashboardSummary(string username)
        {
            IList<DocumentMaintenance> docs = documentMaintenanceRepo.Search(new DocumentMaintenance(), null, db, null, null)
                .Where(x => x.CREATED_BY == username)
                .ToList();

            DateTime today = DateTime.Today;

            int pendingCount = docs.Count(x => x.STATUS == "0");
            int rejectedCount = docs.Count(x => x.STATUS == "2");
            int uploadedToday = docs.Count(x => (x.CREATED_DT ?? DateTime.MinValue).Date == today);
            int publishedDocuments = docs.Count(x => x.STATUS == "5");

            DateTime reviewHorizon = today.AddDays(30);
            int dueForReview = docs.Count(x => x.NEXT_REVIEW_DATE.HasValue && x.NEXT_REVIEW_DATE.Value.Date <= reviewHorizon);

            int totalDocsForPct = Math.Max(docs.Count, 1);
            var categorySummary = docs
                .GroupBy(x => string.IsNullOrEmpty(x.DOCUMENT_NAME) ? "Uncategorized" : x.DOCUMENT_NAME)
                .Select(g => new { category = g.Key, count = g.Count(), percent = Math.Round(g.Count() * 100.0 / totalDocsForPct, 0) })
                .OrderByDescending(x => x.count)
                .ToList();

            // P4D tidak punya konsep "Level dokumen" (itu properti khusus DocumentMaintenance),
            // jadi chart kedua di dashboard user biasa pakai Level, bukan Status seperti di QMS.
            var levelSummary = docs
                .GroupBy(x => x.LEVEL_CODE.HasValue ? "Level " + x.LEVEL_CODE.Value : "Unknown")
                .Select(g => new { status = g.Key, count = g.Count(), percent = Math.Round(g.Count() * 100.0 / totalDocsForPct, 0) })
                .OrderByDescending(x => x.count)
                .ToList();

            return Json(new
            {
                status = true,
                data = new
                {
                    scope = "USER",
                    kpi = new
                    {
                        totalDocuments = new { value = docs.Count },
                        pendingApprovals = new { value = pendingCount },
                        rejectedDocuments = new { value = rejectedCount },
                        uploadedToday = new { value = uploadedToday },
                        publishedDocuments = new { value = publishedDocuments },
                        dueForReview = new { value = dueForReview }
                    },
                    categorySummary,
                    statusSummary = levelSummary,
                    distribution = new { byDepartment = new object[0], byDivision = new object[0] },
                    received = new { byDepartment = new object[0], byDivision = new object[0] }
                }
            });
        }

        // Dashboard untuk user dengan akses QMS: seluruh dokumen di P4D (Document Control),
        // bukan cuma yang dia buat sendiri.
        private JsonResult GetQmsDashboardSummary()
        {
            IList<DocumentControlMaintenance> p4dDocs = p4dMaintenanceRepo.Search(new DocumentControlMaintenance { OPERATION_TYPE = 1 }, null, db, null, null);

            DateTime today = DateTime.Today;

            int pendingCount = p4dDocs.Count(x => x.STATUS == "0");
            int rejectedCount = p4dDocs.Count(x => x.STATUS == "3");
            int registeredToday = p4dDocs.Count(x => (x.CREATED_DT ?? DateTime.MinValue).Date == today);
            int sentDocuments = p4dDocs.Count(x => x.STATUS == "5" || x.STATUS == "6");
            int receivedDocuments = p4dDocs.Count(x => x.STATUS == "2");

            int totalDocsForPct = Math.Max(p4dDocs.Count, 1);
            var departmentSummary = p4dDocs
                .GroupBy(x => string.IsNullOrEmpty(x.DEPARTMENT_NAME) ? "Unassigned" : x.DEPARTMENT_NAME)
                .Select(g => new { category = g.Key, count = g.Count(), percent = Math.Round(g.Count() * 100.0 / totalDocsForPct, 0) })
                .OrderByDescending(x => x.count)
                .ToList();

            var statusSummary = p4dDocs
                .GroupBy(x => x.STATUS_VAL ?? "Unknown")
                .Select(g => new { status = g.Key, count = g.Count(), percent = Math.Round(g.Count() * 100.0 / totalDocsForPct, 0) })
                .OrderByDescending(x => x.count)
                .ToList();

            // Distributed Documents (P4D distribution, STATUS "1" = Sent) — grouped by Department / Division
            IList<DocumentDistribution> distributions = p4dMaintenanceRepo.SearchDocumentDistribution(new DocumentDistribution { STATUS = "1" }, db, null, null);

            var distributionByDepartment = distributions
                .GroupBy(x => string.IsNullOrEmpty(x.DEPARTMENT_NAME) ? "Unassigned" : x.DEPARTMENT_NAME)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            var distributionByDivision = distributions
                .GroupBy(x => string.IsNullOrEmpty(x.DIVISION_NAME) ? "Unassigned" : x.DIVISION_NAME)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            var receivedByDivision = p4dDocs
                .Where(x => x.STATUS == "2")
                .GroupBy(x => string.IsNullOrEmpty(x.DIVISION_NAME) ? "Unassigned" : x.DIVISION_NAME)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            var receivedByDepartment = p4dDocs
                .Where(x => x.STATUS == "2")
                .GroupBy(x => string.IsNullOrEmpty(x.DEPARTMENT_NAME) ? "Unassigned" : x.DEPARTMENT_NAME)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList();

            return Json(new
            {
                status = true,
                data = new
                {
                    scope = "QMS",
                    kpi = new
                    {
                        totalDocuments = new { value = p4dDocs.Count },
                        pendingApprovals = new { value = pendingCount },
                        rejectedDocuments = new { value = rejectedCount },
                        uploadedToday = new { value = registeredToday },
                        publishedDocuments = new { value = sentDocuments },
                        dueForReview = new { value = receivedDocuments }
                    },
                    categorySummary = departmentSummary,
                    statusSummary,
                    distribution = new { byDepartment = distributionByDepartment, byDivision = distributionByDivision },
                    received = new { byDepartment = receivedByDepartment, byDivision = receivedByDivision }
                }
            });
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
