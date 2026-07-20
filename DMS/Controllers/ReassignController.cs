using System.Globalization;
using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Hubs;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Hangfire;
using MDC.Models;
using MDC.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace DMS.Controllers
{
    public class ReassignController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        private readonly IBackgroundJobClient backgroundJobClient;
        private EmailService EmailService;
        private IHubContext<NotificationsHub> _hubContext;

        public ReassignController(DBContext db, IWebHostEnvironment environment, IOptions<EmailConfiguration> options,
            IBackgroundJobClient backgroundJobClient, IHubContext<NotificationsHub> hubContext)
        {
            this.db = db;
            Environment = environment;
            EmailService = new EmailService(options, environment);
            this.backgroundJobClient = backgroundJobClient;
            _hubContext = hubContext;
        }

        private UserRepo userRepo = UserRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
        private ApprovalRepo approvalRepo = ApprovalRepo.Instance;
        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/Reassign/Index"))
                {
                    return StatusCode(403);
                }
            }

            ViewData["Title"] = "Reassign Approver";

            return View();
        }

        //Grid pemilih user (sumber: daftar approver, bukan CRUD user)
        public ActionResult Search(User data, bool initialMode)
        {
            try
            {
                if (data.USERNAME != null)
                {
                    data.USERNAME = '*' + data.USERNAME + '*';
                }
                if (data.FULL_NAME != null)
                {
                    data.FULL_NAME = '*' + data.FULL_NAME + '*';
                }

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
                    var userData = userRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = userRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = userData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        //Dropdown "New Approver" - disalin dari UserController.GetUserName supaya menu ini mandiri
        //(tidak bergantung pada hak akses /User/*)
        public JsonResult GetUserName(string q, string pageLimit, string page)
        {
            try
            {
                User oUser = new User();
                if (q != null)
                    oUser.FULL_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<User> dataList = userRepo.Search(oUser, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.FULL_NAME, id = data.USERNAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetPendingApprovals(string username)
        {
            if (!HasMenuAccess("/Reassign/Index"))
            {
                return Json(new { status = false, message = "You are not authorized to access this feature." });
            }

            try
            {
                IList<ApprovalDetail> result = approvalRepo.GetPendingByApprover(username, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult ReassignApprover(int approvalId, int workflowSeq, string oldApprover, string newApprover, string reason)
        {
            if (!HasMenuAccess("/Reassign/Index"))
            {
                return Json(new { status = false, message = "You are not authorized to access this feature." });
            }

            try
            {
                DBResult result = approvalRepo.ReassignApprover(approvalId, workflowSeq, oldApprover, newApprover, reason, GetLoginUsername(), db);

                if (result.status)
                {
                    NotifyReassignedApprover(approvalId, workflowSeq, newApprover);
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        private void NotifyReassignedApprover(int approvalId, int workflowSeq, string newApprover)
        {
            ApprovalHeader approvalHeader = approvalRepo.GetApprovalHeader(approvalId, db);

            //Task itu belum giliran aktif - approver baru akan otomatis dinotifikasi nanti saat gilirannya tiba lewat alur approve yang sudah ada
            if (approvalHeader == null || approvalHeader.CURRENT_SEQ != workflowSeq)
            {
                return;
            }

            DocumentMaintenance documentMaintenance = documentMaintenanceRepo.Search(
                new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = approvalHeader.TRANSACTION_ID }, GetLoginUsername(), db, 1, 1).FirstOrDefault();

            //Approval bukan dari modul Document Maintenance (mis. P4D) - lewati notifikasi dengan aman
            if (documentMaintenance == null)
            {
                return;
            }

            User user = UserRepo.Instance.GetByKey(new User { USERNAME = newApprover }, db);
            if (user == null)
            {
                return;
            }

            CultureInfo cultureInfo = new CultureInfo("id-ID");
            DateTime date = (DateTime)documentMaintenance.DOCUMENT_DATE;
            string dateString = date.ToString("dddd, d MMMM yyyy", cultureInfo);

            List<string> toAddresses = new List<string> { user.EMAIL };

            IList<MSystem> emailTemplate = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE" }, db, null, null);
            string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE);
            string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
            string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                .Replace("{FULL_NAME}", user.FULL_NAME)
                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE)
                .Replace("{DOCUMENT_NAME}", documentMaintenance.DOCUMENT_TRANSACTION_NAME)
                .Replace("{DOCUMENT_DATE}", dateString)
                .Replace("{REVISION}", documentMaintenance.REVISION.ToString());
            string buttonLink = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}" + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE);

            backgroundJobClient.Enqueue(() => EmailService.SendEmailAsync(toAddresses, subject, title, body, buttonLink));

            IList<MSystem> notificationTemplate = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_APPROVAL_REQUEST_NOTIFICATION" }, db, null, null);
            Notification notification = new Notification
            {
                NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE)
                .Replace("{DOCUMENT_NAME}", documentMaintenance.DOCUMENT_TRANSACTION_NAME),
                NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE)
                .Replace("{DOCUMENT_NAME}", documentMaintenance.DOCUMENT_TRANSACTION_NAME),
                NOTIFICATION_URL = notificationTemplate.Where(x => x.SYSTEM_CODE == "URL").First().SYSTEM_VALUE
                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE),
                USERNAME = user.USERNAME,
            };

            DBResult result = NotificationRepo.Instance.Insert(notification, GetLoginUsername(), db);
            if (result.status)
            {
                _hubContext.Clients.Group(user.USERNAME).SendAsync("ReceiveMessage", "New document approval request, check notification!");
            }
        }
    }
}
