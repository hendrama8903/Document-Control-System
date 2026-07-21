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
        private DocumentLogRepo documentLogRepo = DocumentLogRepo.Instance;
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

                IList<DocumentMaintenance> docs = documentMaintenanceRepo.Search(new DocumentMaintenance(), username, db, null, null);
                IList<DocumentLog> allLogs = documentLogRepo.Search(new DocumentLog(), db, null, null);

                DateTime today = DateTime.Today;

                // ---- KPI 2: Pending Approvals (org-wide) ----
                var pendingDocs = docs.Where(x => x.STATUS == "0").OrderByDescending(x => x.CREATED_DT).ToList();

                // ---- KPI 3: Rejected Documents ----
                var rejectedDocs = docs.Where(x => x.STATUS == "2").OrderByDescending(x => x.CHANGED_DT ?? x.CREATED_DT).ToList();

                // ---- KPI 5: Documents Uploaded Today ----
                int uploadedToday = docs.Count(x => (x.CREATED_DT ?? DateTime.MinValue).Date == today);

                // ---- KPI 6: Published Documents (publish actions, all time) ----
                var publishLogs = allLogs.Where(x => x.LOG_TYPE == "4").ToList();

                // ---- KPI 7: Documents Due for Review (next review date within 30 days, or overdue) ----
                DateTime reviewHorizon = today.AddDays(30);
                int dueForReview = docs.Count(x => x.NEXT_REVIEW_DATE.HasValue && x.NEXT_REVIEW_DATE.Value.Date <= reviewHorizon);

                // ---- Documents by Category (TB_M_DOCUMENT name, joined server-side into DOCUMENT_NAME) ----
                int totalDocsForPct = Math.Max(docs.Count, 1);
                var categorySummary = docs
                    .GroupBy(x => string.IsNullOrEmpty(x.DOCUMENT_NAME) ? "Uncategorized" : x.DOCUMENT_NAME)
                    .Select(g => new { category = g.Key, count = g.Count(), percent = Math.Round(g.Count() * 100.0 / totalDocsForPct, 0) })
                    .OrderByDescending(x => x.count)
                    .ToList();

                // ---- Documents by Status ----
                var statusSummary = docs
                    .GroupBy(x => x.STATUS_DISPLAY ?? "Unknown")
                    .Select(g => new { status = g.Key, count = g.Count(), percent = Math.Round(g.Count() * 100.0 / totalDocsForPct, 0) })
                    .OrderByDescending(x => x.count)
                    .ToList();

                // ---- Recently Rejected Documents ----
                var recentlyRejected = rejectedDocs
                    .Take(6)
                    .Select(x => new
                    {
                        documentCode = x.DOCUMENT_CODE,
                        documentName = x.DOCUMENT_TRANSACTION_NAME,
                        category = x.DOCUMENT_NAME,
                        filePath = x.FILE_PATH,
                        rejectedDate = x.CHANGED_DT ?? x.CREATED_DT,
                        owner = x.DOCUMENT_CREATOR
                    })
                    .ToList();

                // ---- Pending Approvals list (org-wide) ----
                var pendingApprovals = pendingDocs
                    .Take(6)
                    .Select(x => new
                    {
                        documentCode = x.DOCUMENT_CODE,
                        documentName = x.DOCUMENT_TRANSACTION_NAME,
                        category = x.DOCUMENT_NAME,
                        filePath = x.FILE_PATH,
                        submittedBy = x.DOCUMENT_CREATOR,
                        submittedDate = x.CREATED_DT
                    })
                    .ToList();

                // ---- Distributed Documents (P4D distribution, STATUS "1" = Send) — grouped by Department / Division ----
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

                // ---- Received Documents (P4D control, STATUS "2" = Received) — grouped by Division / Department ----
                IList<DocumentControlMaintenance> receivedDocs = p4dMaintenanceRepo.Search(new DocumentControlMaintenance { STATUS = "2" }, username, db, null, null);

                var receivedByDivision = receivedDocs
                    .GroupBy(x => string.IsNullOrEmpty(x.DIVISION_NAME) ? "Unassigned" : x.DIVISION_NAME)
                    .Select(g => new { name = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList();

                var receivedByDepartment = receivedDocs
                    .GroupBy(x => string.IsNullOrEmpty(x.DEPARTMENT_NAME) ? "Unassigned" : x.DEPARTMENT_NAME)
                    .Select(g => new { name = g.Key, count = g.Count() })
                    .OrderByDescending(x => x.count)
                    .ToList();

                return Json(new
                {
                    status = true,
                    data = new
                    {
                        kpi = new
                        {
                            totalDocuments = new { value = docs.Count },
                            pendingApprovals = new { value = pendingDocs.Count },
                            rejectedDocuments = new { value = rejectedDocs.Count },
                            uploadedToday = new { value = uploadedToday },
                            publishedDocuments = new { value = publishLogs.Count },
                            dueForReview = new { value = dueForReview }
                        },
                        categorySummary,
                        statusSummary,
                        recentlyRejected,
                        pendingApprovals,
                        distribution = new { byDepartment = distributionByDepartment, byDivision = distributionByDivision },
                        received = new { byDepartment = receivedByDepartment, byDivision = receivedByDivision }
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
