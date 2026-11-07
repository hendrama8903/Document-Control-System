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
        private ExternalDocumentRepo externalDocumentRepo = ExternalDocumentRepo.Instance;

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

        // Dashboard redesign (request Hendra 2026-11-05). Setiap bagian punya aturan
        // kepemilikan sendiri - satu user bisa punya beberapa peran sekaligus (PIC,
        // approver, atasan struktural, QMS), jadi bagian-bagiannya ditumpuk, bukan
        // dipilih salah satu. SEMUA filter di server: JSON cuma berisi baris yang
        // memang hak user ini.
        //   - My Approval      : CURRENT_APPROVER = saya (lintas divisi)
        //   - My Documents     : CREATED_BY / DOCUMENT_CREATOR = saya
        //   - Overview (KPI)   : scope struktural dari TB_M_USER_POS -
        //                        Dept Head = departemennya, Div Head / EO = divisinya,
        //                        Direktur / Presdir (atau EO tanpa divisi) = seluruh
        //                        perusahaan, selain itu = dokumen saya sendiri
        //   - Upcoming Review  : dokumen dalam scope Overview; QMS = semua + External
        //   - Document Control : hanya QMS (DOCUMENT_CONTROL_ACCESS)
        public JsonResult GetDashboardSummary()
        {
            try
            {
                string username = GetLoginUsername();
                bool isQms = GetDocumentAccessControl() == true;
                DateTime today = DateTime.Today;

                bool IsMe(string value) => string.Equals(value, username, StringComparison.OrdinalIgnoreCase);

                // dokumen yang sudah dihapus/obsolete tidak dihitung di mana pun
                List<DocumentMaintenance> allDocs = documentMaintenanceRepo.Search(new DocumentMaintenance(), null, db, null, null)
                    .Where(x => x.STATUS != "3" && x.STATUS != "4")
                    .ToList();
                List<DocumentMaintenance> myDocs = allDocs.Where(x => IsMe(x.CREATED_BY) || IsMe(x.DOCUMENT_CREATOR)).ToList();

                // ---------- scope struktural ----------
                List<UserPosition> positions = UserRepo.Instance.SearchPosition(new UserPosition { USERNAME = username }, db, null, null)
                    .Where(x => IsMe(x.USERNAME))
                    .ToList();
                bool companyWide = positions.Any(x => x.POSITION_ID == 6 || x.POSITION_ID == 7 || (x.POSITION_ID == 5 && string.IsNullOrEmpty(x.DIVISION)));
                List<UserPosition> divisionHeads = positions.Where(x => (x.POSITION_ID == 4 || x.POSITION_ID == 5) && !string.IsNullOrEmpty(x.DIVISION)).ToList();
                List<UserPosition> departmentHeads = positions.Where(x => x.POSITION_ID == 3 && x.DEPARTMENT_ID != null).ToList();
                HashSet<string> scopeDivisions = new HashSet<string>(divisionHeads.Select(x => x.DIVISION), StringComparer.OrdinalIgnoreCase);
                HashSet<int> scopeDepartments = new HashSet<int>(departmentHeads.Select(x => (int)x.DEPARTMENT_ID));

                string scopeType, scopeLabel;
                List<DocumentMaintenance> scopeDocs;
                if (companyWide)
                {
                    scopeType = "COMPANY";
                    scopeLabel = "All Divisions";
                    scopeDocs = allDocs;
                }
                else if (scopeDivisions.Count > 0 || scopeDepartments.Count > 0)
                {
                    scopeType = scopeDivisions.Count > 0 ? "DIVISION" : "DEPARTMENT";
                    scopeLabel = string.Join(", ", divisionHeads.Select(x => x.DIVISION_NAME ?? x.DIVISION)
                        .Concat(departmentHeads.Select(x => x.DEPARTMENT_NAME))
                        .Where(x => !string.IsNullOrEmpty(x)).Distinct());
                    scopeDocs = allDocs.Where(x => (x.DIVISION != null && scopeDivisions.Contains(x.DIVISION))
                        || (x.DEPARTMENT_ID != null && scopeDepartments.Contains((int)x.DEPARTMENT_ID))).ToList();
                }
                else
                {
                    scopeType = "PERSONAL";
                    scopeLabel = "My Documents";
                    scopeDocs = myDocs;
                }

                User me = UserRepo.Instance.GetByKey(new User { USERNAME = username }, db);
                string positionName = positions.OrderByDescending(x => x.POSITION_ID ?? 0).Select(x => x.POSITION_NAME).FirstOrDefault();

                // ---------- My Approval & My Documents ----------
                List<DocumentMaintenance> waitingMe = allDocs
                    .Where(x => x.STATUS == "0" && IsMe(x.CURRENT_APPROVER))
                    .OrderBy(x => x.CHANGED_DT ?? x.CREATED_DT)
                    .ToList();
                var myApprovalList = waitingMe.Take(50).Select(x => new
                {
                    code = x.DOCUMENT_CODE,
                    name = x.DOCUMENT_TRANSACTION_NAME,
                    category = x.CATEGORY_CODE,
                    step = x.CURRENT_APPROVAL_SEQ,
                    total = x.TOTAL_APPROVAL_SEQ,
                    creator = x.DOCUMENT_CREATOR,
                    updated = x.CHANGED_DT ?? x.CREATED_DT
                }).ToList();

                var myInProgress = myDocs
                    .Where(x => x.STATUS == "0" || x.STATUS == "6")
                    .OrderByDescending(x => x.CHANGED_DT ?? x.CREATED_DT)
                    .Take(50)
                    .Select(x => new
                    {
                        code = x.DOCUMENT_CODE,
                        name = x.DOCUMENT_TRANSACTION_NAME,
                        status = x.STATUS,
                        step = x.CURRENT_APPROVAL_SEQ,
                        total = x.TOTAL_APPROVAL_SEQ,
                        approverName = x.CURRENT_APPROVER_NAME,
                        updated = x.CHANGED_DT ?? x.CREATED_DT
                    }).ToList();

                // ---------- Overview (scope struktural) ----------
                int scopeTotalForPct = Math.Max(scopeDocs.Count, 1);
                var byCategory = scopeDocs
                    .GroupBy(x => string.IsNullOrEmpty(x.DOCUMENT_NAME) ? "Uncategorized" : x.DOCUMENT_NAME)
                    .Select(g => new { label = g.Key, count = g.Count(), percent = Math.Round(g.Count() * 100.0 / scopeTotalForPct, 0) })
                    .OrderByDescending(x => x.count)
                    .ToList();
                var byStatus = scopeDocs
                    .GroupBy(x => x.STATUS)
                    .Select(g => new { code = g.Key, label = (g.First().STATUS_DISPLAY ?? "Unknown").Replace(" (Effective)", ""), count = g.Count(), percent = Math.Round(g.Count() * 100.0 / scopeTotalForPct, 0) })
                    .OrderByDescending(x => x.count)
                    .ToList();

                DateTime trendStart = new DateTime(today.Year, today.Month, 1).AddMonths(-5);
                var trend = Enumerable.Range(0, 6).Select(i =>
                {
                    DateTime month = trendStart.AddMonths(i);
                    return new
                    {
                        label = month.ToString("MMM yy", System.Globalization.CultureInfo.InvariantCulture),
                        count = scopeDocs.Count(x => x.CREATED_DT.HasValue && x.CREATED_DT.Value.Year == month.Year && x.CREATED_DT.Value.Month == month.Month)
                    };
                }).ToList();

                // rincian per unit di bawahnya: perusahaan -> per divisi, divisi -> per departemen
                var breakdown = scopeType == "COMPANY" || scopeType == "DIVISION"
                    ? scopeDocs
                        .GroupBy(x => scopeType == "COMPANY"
                            ? (string.IsNullOrEmpty(x.DIVISION_NAME) ? "Unassigned" : x.DIVISION_NAME)
                            : (string.IsNullOrEmpty(x.DEPARTMENT_NAME) ? "Unassigned" : x.DEPARTMENT_NAME))
                        .Select(g => new
                        {
                            name = g.Key,
                            total = g.Count(),
                            inProgress = g.Count(x => x.STATUS == "0" || x.STATUS == "6"),
                            effective = g.Count(x => x.STATUS == "1" || x.STATUS == "5"),
                            rejected = g.Count(x => x.STATUS == "2")
                        })
                        .OrderByDescending(x => x.total)
                        .Cast<object>()
                        .ToList()
                    : new List<object>();

                // ---------- Upcoming Review (<= 30 hari, termasuk yang sudah lewat) ----------
                DateTime reviewHorizon = today.AddDays(30);
                var reviewItems = (isQms ? allDocs : scopeDocs)
                    .Where(x => (x.STATUS == "1" || x.STATUS == "5") && x.NEXT_REVIEW_DATE.HasValue && x.NEXT_REVIEW_DATE.Value.Date <= reviewHorizon)
                    .Select(x => new { source = "Internal", code = x.DOCUMENT_CODE, name = x.DOCUMENT_TRANSACTION_NAME, date = x.NEXT_REVIEW_DATE.Value.Date })
                    .ToList();
                if (isQms)
                {
                    reviewItems.AddRange(externalDocumentRepo.Search(new ExternalDocument(), null, null, db, null, null)
                        .Where(x => x.NEXT_REVIEW_DATE.HasValue && x.NEXT_REVIEW_DATE.Value.Date <= reviewHorizon)
                        .Select(x => new { source = "External", code = (string)null, name = x.DOCUMENT_NAME, date = x.NEXT_REVIEW_DATE.Value.Date }));
                }
                var upcomingReview = reviewItems
                    .OrderBy(x => x.date)
                    .Take(50)
                    .Select(x => new { x.source, x.code, x.name, date = x.date, daysLeft = (int)(x.date - today).TotalDays })
                    .ToList();

                // ---------- Document Control (QMS saja) ----------
                object qms = null;
                int p4dActionCount = 0;
                if (isQms)
                {
                    IList<DocumentControlMaintenance> p4dDocs = p4dMaintenanceRepo.Search(new DocumentControlMaintenance { OPERATION_TYPE = 1 }, null, db, null, null);
                    // sama dengan getMyAction() di grid P4D: Receive / Accept / Distribute / Send
                    p4dActionCount = p4dDocs.Count(x => x.STATUS == "0" || x.STATUS == "1" || x.STATUS == "2"
                        || (x.STATUS == "6" && (x.PENDING_DISTRIBUTION_COUNT ?? 0) > 0));

                    IList<DocumentDistribution> distributions = p4dMaintenanceRepo.SearchDocumentDistribution(new DocumentDistribution { STATUS = "1" }, db, null, null);
                    List<DocumentControlMaintenance> received = p4dDocs.Where(x => x.STATUS == "2").ToList();

                    qms = new
                    {
                        kpi = new
                        {
                            total = p4dDocs.Count,
                            toReceive = p4dDocs.Count(x => x.STATUS == "0"),
                            checking = p4dDocs.Count(x => x.STATUS == "1"),
                            sent = p4dDocs.Count(x => x.STATUS == "5" || x.STATUS == "6"),
                            rejected = p4dDocs.Count(x => x.STATUS == "3")
                        },
                        distribution = new
                        {
                            byDepartment = GroupNames(distributions.Select(x => x.DEPARTMENT_NAME)),
                            byDivision = GroupNames(distributions.Select(x => x.DIVISION_NAME))
                        },
                        received = new
                        {
                            byDepartment = GroupNames(received.Select(x => x.DEPARTMENT_NAME)),
                            byDivision = GroupNames(received.Select(x => x.DIVISION_NAME))
                        }
                    };
                }

                return Json(new
                {
                    status = true,
                    data = new
                    {
                        user = new { fullName = me?.FULL_NAME ?? username, positionName, scopeType, scopeLabel, isQms },
                        actions = new
                        {
                            myApproval = waitingMe.Count,
                            p4dAction = p4dActionCount,
                            rejected = myDocs.Count(x => x.STATUS == "2"),
                            reviewDue = reviewItems.Count,
                            reviewOverdue = reviewItems.Count(x => x.date < today)
                        },
                        myApprovalList,
                        myInProgress,
                        overview = new
                        {
                            kpi = new
                            {
                                total = scopeDocs.Count,
                                inProgress = scopeDocs.Count(x => x.STATUS == "0" || x.STATUS == "6"),
                                effective = scopeDocs.Count(x => x.STATUS == "1" || x.STATUS == "5"),
                                rejected = scopeDocs.Count(x => x.STATUS == "2")
                            },
                            byCategory,
                            byStatus,
                            trend,
                            breakdown
                        },
                        upcomingReview,
                        qms
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        private static List<object> GroupNames(IEnumerable<string> names)
        {
            return names
                .Select(x => string.IsNullOrEmpty(x) ? "Unassigned" : x)
                .GroupBy(x => x)
                .OrderByDescending(g => g.Count())
                .Select(g => (object)new { name = g.Key, count = g.Count() })
                .ToList();
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
