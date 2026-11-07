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
using NPOI.POIFS.Crypt.Dsig;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Globalization;
using System.Text;
//Recommit DINA
namespace DMS.Controllers
{
    public class P4DMaintenanceController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        private readonly IBackgroundJobClient backgroundJobClient;
        private EmailService EmailService;
        private IHubContext<NotificationsHub> _hubContext;

        public P4DMaintenanceController(DBContext db, IWebHostEnvironment environment, IBackgroundJobClient backgroundJobClient,
             IHubContext<NotificationsHub> hubContext, IOptions<EmailConfiguration> options)
        {
            this.db = db;
            Environment = environment;
            this.backgroundJobClient = backgroundJobClient;
            EmailService = new EmailService(options, environment);
            _hubContext = hubContext;
        }

        private P4DMaintenanceRepo P4DMaintenanceRepo = P4DMaintenanceRepo.Instance;
        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private DepartmentMasterRepo departmentMasterRepo = DepartmentMasterRepo.Instance;
        private SectionMasterRepo sectionMasterRepo = SectionMasterRepo.Instance;
        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;

        public IActionResult Index(string DOCUMENT_CODE)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                string param = "/P4DMaintenance/Index?DOCUMENT_CODE=" + DOCUMENT_CODE;
                return RedirectToAction("Login", "Auth", new { param });
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/P4DMaintenance/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("P4D-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("P4D-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("P4D-DELETE");
            ViewData["Download"] = HttpContext.Session.GetString("functionList").Contains("P4D-DOWNLOAD");
            ViewData["Distribution"] = HttpContext.Session.GetString("functionList").Contains("P4D-DISTRIBUTION");
            ViewData["Receive"] = HttpContext.Session.GetString("functionList").Contains("P4D-RECEIVE");
            ViewData["UnReceive"] = HttpContext.Session.GetString("functionList").Contains("P4D-UNRECEIVE");
            ViewData["Reject"] = HttpContext.Session.GetString("functionList").Contains("P4D-REJECT");
            ViewData["Send"] = HttpContext.Session.GetString("functionList").Contains("P4D-SEND");
            ViewData["DocumentControlAccess"] = GetDocumentAccessControl();

            bool? isaccess = GetDocumentAccessControl();


            ViewData["Title"] = "Document Registration";

            return View();
        }

        public JsonResult GetByKey(DocumentControlMaintenance data)
        {
            try
            {
                DocumentControlMaintenance result = P4DMaintenanceRepo.Search(data, null, db, 1, 1).FirstOrDefault();
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Rincian per-department untuk side panel "Acceptance Detail" (klik kolom
        // Acceptance) - sama persis dengan DocumentControlDashboardController versi
        // (request Hendra 2026-08-15, samakan format popup-nya).
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
                    data.OPERATION_TYPE = 1;

                    var listData = P4DMaintenanceRepo.Search(data, GetLoginUsername(), db, pageNumber, pageSize);
                    var dataCount = P4DMaintenanceRepo.Search(data, GetLoginUsername(), db, null, null).Count;
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

        public JsonResult GetDepartmentCode(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DepartmentMaster oDepartmentMaster = new DepartmentMaster();
                if (q != null)
                    oDepartmentMaster.DEPARTMENT_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DepartmentMaster> dataList = departmentMasterRepo.Search(oDepartmentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DEPARTMENT_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DEPARTMENT_CODE, id = data.DEPARTMENT_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetControlDocumentNo(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentControlMaintenance oDocumentMaster = new DocumentControlMaintenance();
                if (q != null)
                    oDocumentMaster.DOCUMENT_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentControlMaintenance> dataList = P4DMaintenanceRepo.Search(oDocumentMaster, null, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_CODE, id = data.DOCUMENT_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetControlDocumentNoLoginBased(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentControlMaintenance oDocumentMaster = new DocumentControlMaintenance();
                oDocumentMaster.DEPARTMENT_ID = GetLoginDepartmentId();

                if (q != null)
                    oDocumentMaster.DOCUMENT_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentControlMaintenance> dataList = P4DMaintenanceRepo.Search(oDocumentMaster, null, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_CODE, id = data.DOCUMENT_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetControlDocumentName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentControlMaintenance oDocumentMaster = new DocumentControlMaintenance();
                if (q != null)
                    oDocumentMaster.DOCUMENT_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentControlMaintenance> dataList = P4DMaintenanceRepo.Search(oDocumentMaster, null, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_NAME, id = data.DOCUMENT_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetControlDocumentNameLoginBased(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentControlMaintenance oDocumentMaster = new DocumentControlMaintenance();
                oDocumentMaster.DEPARTMENT_ID = GetLoginDepartmentId();

                if (q != null)
                    oDocumentMaster.DOCUMENT_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentControlMaintenance> dataList = P4DMaintenanceRepo.Search(oDocumentMaster, null, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_NAME, id = data.DOCUMENT_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDataByDocumentNo(DocumentMaintenance data)
        {
            try
            {
                DocumentMaintenance result = documentMaintenanceRepo.Search(data, null, db, 1, 1).FirstOrDefault();
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> AddEditAsync(string screenMode, DocumentControlMaintenance data)
        {
            JsonResult denied = screenMode == "ADD" ? DenyP4D("P4D-ADD", false) : DenyP4D("P4D-EDIT");
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;
            //string folderName = "/Upload/";
            //string webRootPath = Environment.WebRootPath;

            try
            {
                //if (Request.Form.Files.Count > 0)
                //{
                //    IFormFile file = Request.Form.Files[0];
                //    MSystem mSystem = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "DOCUMENT_MASTER" }, db);

                //    string extension = Path.GetExtension(file.FileName);
                //    string path = folderName + mSystem.SYSTEM_VALUE.Trim();
                //    string pathSave = webRootPath + folderName + mSystem.SYSTEM_VALUE.Trim();
                //    string documentname = data.DOCUMENT_NAME;
                //    documentname = documentname.Replace(" ", "_");
                //    string fileName = documentname + extension;
                //    string finalPath = pathSave + fileName;


                //    if (!Directory.Exists(pathSave))
                //    {
                //        Directory.CreateDirectory(pathSave);
                //    }

                //    if (data.FILE_PATH != null)
                //    {
                //        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                //        //Delete File
                //        if (System.IO.File.Exists(pathCheck))
                //        {
                //            System.IO.File.Delete(pathCheck);
                //        }
                //    }

                //    data.FILE_PATH = path + fileName;
                //    //Save File to Local Storage
                //    using (var stream = new FileStream(finalPath, FileMode.Create))
                //    {
                //        file.CopyTo(stream);
                //    }
                //}

                if (screenMode == "ADD")
                {
                    result = P4DMaintenanceRepo.Insert(data, GetLoginUsername(), db);
                    if (result.returnId != 0)
                    {
                        backgroundJobClient.Enqueue(() => SendApprovalEmailAsync(result.returnId, GetLoginUsername(),
                             this.Request.Scheme, this.Request.Host.ToString(), this.Request.PathBase.ToString()));
                    }
                }
                else
                {
                    result = P4DMaintenanceRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public void SendApprovalEmailAsync(int documentCtrlId, string loginUsername, string scheme, string hostString, string pathBase)
        {
            DocumentControlMaintenance documentControlMaintenance = P4DMaintenanceRepo.Search(new DocumentControlMaintenance { DOCUMENT_CTRL_ID = documentCtrlId }, loginUsername, db, 1, 1).FirstOrDefault();

            if (documentControlMaintenance != null)
            {
                IList<UserPosition> userPositions = UserRepo.Instance.SearchPosition(new UserPosition { DOCUMENT_CONTROL_ACCESS = "1" }, db, null, null);
                if (userPositions.Count > 0)
                {
                    string dateString = documentControlMaintenance.DOCUMENT_DATE?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "-";

                    List<string> toAddresses = new List<string>();
                    List<string> toUsernames = new List<string>();

                    foreach (UserPosition userPosition in userPositions)
                    {
                        User user = UserRepo.Instance.GetByKey(new User { USERNAME = userPosition.USERNAME }, db);
                        if (user != null)
                        {
                            toAddresses.Add(user.EMAIL);
                            toUsernames.Add(user.USERNAME);
                        }
                    }

                    toAddresses = toAddresses.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
                    toUsernames = toUsernames.Distinct().ToList();

                    User creator = UserRepo.Instance.GetByKey(new User { USERNAME = documentControlMaintenance.CREATED_BY }, db);

                    IList<MSystem> emailTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE" }, db, null, null);
                    string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
                         .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);
                    string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
                    string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                        .Replace("{DOCUMENT_CODE}", EncodeHtml(documentControlMaintenance.DOCUMENT_CODE))
                        .Replace("{DOCUMENT_NAME}", EncodeHtml(documentControlMaintenance.DOCUMENT_NAME))
                        .Replace("{DOCUMENT_DATE}", dateString)
                        .Replace("{REVISION}", documentControlMaintenance.REVISION.ToString())
                        .Replace("{CREATOR}", EncodeHtml(creator?.FULL_NAME ?? documentControlMaintenance.CREATED_BY))
                        .Replace("{DOCUMENT_TYPE}", EncodeHtml(GetDocumentTypeText(documentControlMaintenance.DOCUMENT_TYPE)))
                        .Replace("{CLASSIFICATION}", EncodeHtml(string.IsNullOrEmpty(documentControlMaintenance.CLASSIFIED_VAL)
                            ? "-" : documentControlMaintenance.CLASSIFIED + " - " + documentControlMaintenance.CLASSIFIED_VAL))
                        .Replace("{DIVISION}", EncodeHtml(documentControlMaintenance.DIVISION + " - " + documentControlMaintenance.DIVISION_NAME))
                        .Replace("{DEPARTMENT}", EncodeHtml(documentControlMaintenance.DEPARTMENT_CODE + " - " + documentControlMaintenance.DEPARTMENT_NAME));
                    string buttonLink = $"{scheme}://{hostString}{pathBase}" + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                        .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);

                    if (NotificationSettingRepo.Instance.IsEmailEnabled("P4D_APPROVAL_REQUEST", db))
                        backgroundJobClient.Enqueue(() => EmailService.SendEmailAsync(toAddresses, subject, title, body, buttonLink));

                    IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "P4D_APPROVAL_REQUEST_NOTIFICATION" }, db, null, null);

                    foreach(string toUsername in toUsernames)
                    {
                        Notification notification = new Notification
                        {
                            NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                            NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                            NOTIFICATION_URL = notificationTemplate.Where(x => x.SYSTEM_CODE == "URL").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                            USERNAME = toUsername,
                        };

                        DBResult result = NotificationRepo.Instance.Insert(notification, loginUsername, db);
                        if (result.status)
                        {
                            _hubContext.Clients.Group(toUsername).SendAsync("ReceiveMessage", "New document approval request, check notification!");
                        }
                    }
                }
            }
        }

        // Email progres registrasi P4D ke pendaftar (request Hendra 2026-11-05), satu
        // email per tahap QAD: "receive" (Draft -> Checking), "reject", "distributed"
        // (Send Distribution All). Accept sengaja tidak kirim email - biasanya langsung
        // disusul distribusi oleh QAD, jadi cukup email "distributed".
        // Penerima: pendaftar P4D (TB_R_CTRL_DOCUMENT.CREATED_BY) + penginput dokumen
        // sumbernya (TB_R_DOCUMENT.CREATED_BY) kalau orangnya beda.
        public void SendP4DStatusEmail(int documentCtrlId, string loginUsername, string mode, string scheme, string hostString, string pathBase, string remark)
        {
            DocumentControlMaintenance p4d = P4DMaintenanceRepo.Search(new DocumentControlMaintenance { DOCUMENT_CTRL_ID = documentCtrlId }, null, db, 1, 1).FirstOrDefault();
            if (p4d == null)
            {
                return;
            }

            DocumentMaintenance sourceDocument = p4d.DOCUMENT_TRANSACTION_ID == null ? null : documentMaintenanceRepo
                .Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = p4d.DOCUMENT_TRANSACTION_ID }, null, db, 1, 1).FirstOrDefault();

            User actor = UserRepo.Instance.GetByKey(new User { USERNAME = loginUsername }, db);
            string actorName = actor?.FULL_NAME ?? loginUsername;
            User registrant = UserRepo.Instance.GetByKey(new User { USERNAME = p4d.CREATED_BY }, db);

            string heading, message, statusText, statusColor, subjectStatus, judgement, actionLabel, nextStep;
            if (mode == "reject")
            {
                heading = "Registration Rejected";
                message = "Your document registration has been <strong>rejected by " + EncodeHtml(actorName) + "</strong> (Document Control). Please review the remark below, revise the document if needed and register it again.";
                statusText = "Rejected";
                statusColor = "#B91C1C";
                subjectStatus = "Document Registration Rejected";
                judgement = "DITOLAK oleh " + actorName;
                actionLabel = "Rejected By";
                nextStep = "-";
            }
            else if (mode == "distributed")
            {
                heading = "Document Distributed";
                message = "Your document registration is <strong>complete</strong>. The document has been distributed by " + EncodeHtml(actorName) + " (Document Control) to the target departments.";
                statusText = "Distributed";
                statusColor = "#15803D";
                subjectStatus = "Document Distributed";
                judgement = "DIDISTRIBUSIKAN oleh " + actorName;
                actionLabel = "Distributed By";
                nextStep = "Acknowledgment by the receiving departments";
            }
            else
            {
                heading = "Registration Received";
                message = "Your document registration has been <strong>received by " + EncodeHtml(actorName) + "</strong> (Document Control) and is now being checked.";
                statusText = "Checking";
                statusColor = "#C2410C";
                subjectStatus = "Document Registration Received";
                judgement = "DITERIMA oleh " + actorName + " dan sedang diperiksa";
                actionLabel = "Received By";
                nextStep = "Accept & distribution by Document Control";
            }

            string dateString = p4d.DOCUMENT_DATE?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "-";
            string classification = string.IsNullOrEmpty(p4d.CLASSIFIED_VAL) ? "-" : p4d.CLASSIFIED + " - " + p4d.CLASSIFIED_VAL;
            string flowHtml = BuildP4DFlowHtml(mode, registrant?.FULL_NAME ?? p4d.CREATED_BY, p4d.CREATED_DT, actorName);

            List<User> recipients = new[] { p4d.CREATED_BY, sourceDocument?.CREATED_BY }
                .Where(x => !string.IsNullOrEmpty(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(x => UserRepo.Instance.GetByKey(new User { USERNAME = x }, db))
                .Where(x => x != null)
                .ToList();

            IList<MSystem> emailTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "P4D_APPROVE_REJECT_EMAIL_TEMPLATE" }, db, null, null);
            IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "P4D_APPROVE_REJECT_NOTIFICATION" }, db, null, null);
            bool emailEnabled = NotificationSettingRepo.Instance.IsEmailEnabled("P4D_APPROVE_REJECT", db);

            string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
                .Replace("{SUBJECT_STATUS}", subjectStatus)
                .Replace("{DOCUMENT_CODE}", p4d.DOCUMENT_CODE);
            string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
            string buttonLink = $"{scheme}://{hostString}{pathBase}" + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                .Replace("{DOCUMENT_CODE}", p4d.DOCUMENT_CODE);

            foreach (User user in recipients)
            {
                if (emailEnabled && !string.IsNullOrEmpty(user.EMAIL))
                {
                    List<string> toAddresses = new List<string> { user.EMAIL };
                    string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                        .Replace("{HEADING}", heading)
                        .Replace("{MESSAGE}", message)
                        .Replace("{FULL_NAME}", EncodeHtml(user.FULL_NAME))
                        .Replace("{DOCUMENT_CODE}", EncodeHtml(p4d.DOCUMENT_CODE))
                        .Replace("{DOCUMENT_NAME}", EncodeHtml(p4d.DOCUMENT_NAME))
                        .Replace("{DOCUMENT_TYPE}", EncodeHtml(GetDocumentTypeText(p4d.DOCUMENT_TYPE)))
                        .Replace("{CLASSIFICATION}", EncodeHtml(classification))
                        .Replace("{DEPARTMENT}", EncodeHtml(p4d.DEPARTMENT_CODE + " - " + p4d.DEPARTMENT_NAME))
                        .Replace("{REVISION}", p4d.REVISION.ToString())
                        .Replace("{DOCUMENT_DATE}", dateString)
                        .Replace("{ACTION_LABEL}", actionLabel)
                        .Replace("{ACTION_BY}", EncodeHtml(actorName))
                        .Replace("{NEXT_STEP}", EncodeHtml(nextStep))
                        .Replace("{REMARK}", string.IsNullOrWhiteSpace(remark) ? "-" : EncodeHtml(remark))
                        .Replace("{STATUS}", statusText)
                        .Replace("{STATUS_COLOR}", statusColor)
                        .Replace("{P4D_FLOW}", flowHtml);

                    backgroundJobClient.Enqueue(() => EmailService.SendEmailAsync(toAddresses, subject, title, body, buttonLink));
                }

                Notification notification = new Notification
                {
                    NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                    .Replace("{DOCUMENT_CODE}", p4d.DOCUMENT_CODE)
                    .Replace("{JUDGEMENT}", judgement),
                    NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
                    .Replace("{DOCUMENT_CODE}", p4d.DOCUMENT_CODE),
                    NOTIFICATION_URL = notificationTemplate.Where(x => x.SYSTEM_CODE == "URL").First().SYSTEM_VALUE
                    .Replace("{DOCUMENT_CODE}", p4d.DOCUMENT_CODE),
                    USERNAME = user.USERNAME,
                };

                DBResult result = NotificationRepo.Instance.Insert(notification, loginUsername, db);
                if (result.status)
                {
                    _hubContext.Clients.Group(user.USERNAME).SendAsync("ReceiveMessage", "Document registration update, check notification!");
                }
            }
        }

        // Tahapan registrasi P4D untuk placeholder {P4D_FLOW}: Registered -> Checking
        // (Receive) -> Accepted -> Distributed. Tanggal per tahap tidak disimpan di
        // TB_R_CTRL_DOCUMENT, jadi hanya tahap Registered yang bertanggal.
        private static string BuildP4DFlowHtml(string mode, string registrantName, DateTime? registeredDate, string actorName)
        {
            // state: done / current / rejected / pending / skipped
            bool rejected = mode == "reject";
            bool distributed = mode == "distributed";
            var steps = new List<(string Label, string Who, string State)>
            {
                ("Registered", registrantName, "done"),
                ("Received & Checked", distributed ? "Document Control" : actorName, rejected ? "rejected" : "done"),
                ("Accepted", "Document Control", distributed ? "done" : rejected ? "skipped" : "current"),
                ("Distributed", distributed ? actorName : "Document Control", distributed ? "done" : rejected ? "skipped" : "pending"),
            };

            StringBuilder html = new StringBuilder();
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                string icon, color, status;
                switch (step.State)
                {
                    case "done": icon = "&#10003;"; color = "#15803D"; status = "Done"; break;
                    case "rejected": icon = "&#10007;"; color = "#B91C1C"; status = "Rejected"; break;
                    case "current": icon = "&#9679;"; color = "#C2410C"; status = "In Progress"; break;
                    case "skipped": icon = "&#9675;"; color = "#9CA3AF"; status = "Not Processed"; break;
                    default: icon = "&#9675;"; color = "#9CA3AF"; status = "Pending"; break;
                }
                string date = i == 0 ? registeredDate?.ToString("dd MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : null;
                string border = i < steps.Count - 1 ? "border-bottom:1px solid #E3E7ED;" : "";

                html.Append("<tr>")
                    .Append($"<td width=\"24\" valign=\"top\" style=\"padding:10px 0;{border}font-size:14px;line-height:20px;color:{color}\">{icon}</td>")
                    .Append($"<td valign=\"top\" style=\"padding:10px 8px 10px 0;{border}\"><div style=\"font-size:11px;line-height:14px;color:#6B7280\">Step {i + 1} &middot; {System.Net.WebUtility.HtmlEncode(step.Label)}</div>")
                    .Append($"<div style=\"font-size:14px;line-height:20px;font-weight:600;color:#1F2A37\">{System.Net.WebUtility.HtmlEncode(step.Who ?? "-")}</div></td>")
                    .Append($"<td align=\"right\" valign=\"top\" style=\"padding:10px 0;{border}white-space:nowrap\"><div style=\"font-size:12px;line-height:16px;font-weight:600;color:{color}\">{status}</div>")
                    .Append(date != null ? $"<div style=\"font-size:11px;line-height:16px;color:#6B7280\">{date}</div>" : "")
                    .Append("</td></tr>");
            }

            return "<table width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" style=\"border-collapse:separate;border:1px solid #E3E7ED;border-radius:10px;margin-top:16px\">"
                + "<tr><td style=\"padding:16px 16px 6px 16px\"><div style=\"font-size:12px;line-height:16px;font-weight:600;letter-spacing:0.3px;color:#4B5563;padding-bottom:4px\">REGISTRATION PROGRESS</div>"
                + "<table width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" border=\"0\" role=\"presentation\" style=\"border-collapse:collapse\">"
                + html
                + "</table></td></tr></table>";
        }

        private string GetDocumentTypeText(string documentType)
        {
            if (string.IsNullOrEmpty(documentType)) return "-";
            MSystem system = MSystemRepo.Instance.GetByKey(new MSystem { SYSTEM_TYPE = "DOCUMENT_TYPE", SYSTEM_CODE = documentType }, db);
            return system != null ? documentType + " - " + system.SYSTEM_VALUE : documentType;
        }

        private static string EncodeHtml(string value) => System.Net.WebUtility.HtmlEncode(value ?? "-");

        public void SendDistributionEmail(int documentTransactionId, string loginUsername, string scheme, string hostString, string pathBase)
        {
            DocumentMaintenance documentMaintenance = documentMaintenanceRepo.Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentTransactionId }, null, db, 1, 1).FirstOrDefault();

            if (documentMaintenance != null)
            {
                DocumentControlMaintenance documentControlMaintenance = P4DMaintenanceRepo.Search(new DocumentControlMaintenance { DOCUMENT_CODE = documentMaintenance.DOCUMENT_CODE }, loginUsername, db, 1, 1).FirstOrDefault();

                if (documentControlMaintenance != null)
                {
                    CultureInfo cultureInfo = new CultureInfo("id-ID"); // Budaya Indonesia
                    DateTime date = (DateTime)documentControlMaintenance.DOCUMENT_DATE;
                    string dateString = date.ToString("dddd, d MMMM yyyy", cultureInfo);
                    IList<MSystem> emailTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DISTRIBUTION_EMAIL_TEMPLATE" }, db, null, null);
                    IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DISTRIBUTION_NOTIFICATION" }, db, null, null);

                    IList<DocumentDistribution> documentDistributions = P4DMaintenanceRepo.SearchDocumentDistribution(
                        new DocumentDistribution { DOCUMENT_TRANSACTION_ID = documentTransactionId }, db, null, null);
                    if (documentDistributions.Count > 0)
                    {
                        List<string> alreadySentUsername = new List<string>();
                        List<string> alreadySentAddresses = new List<string>();

                        foreach (DocumentDistribution documentDistribution in documentDistributions)
                        {
                            List<string> toAddresses = new List<string>();
                            List<string> toUsername = new List<string>();

                            IList<UserPosition> userPositions = UserRepo.Instance.SearchPosition(new UserPosition { DIVISION = documentDistribution.DIVISION }, db, null, null)
                                .Where(x => x.DEPARTMENT_ID == null).ToList();
                            if (userPositions.Count > 0)
                            {
                                foreach (UserPosition userPosition in userPositions)
                                {
                                    User user = UserRepo.Instance.GetByKey(new User { USERNAME = userPosition.USERNAME }, db);
                                    if (user != null)
                                    {
                                        toAddresses.Add(user.EMAIL);
                                        toUsername.Add(user.USERNAME);
                                    }
                                }

                                toAddresses = toAddresses.Distinct().ToList();
                                toUsername = toUsername.Distinct().ToList();

                                toAddresses.RemoveAll(item => alreadySentAddresses.Contains(item));
                                toUsername.RemoveAll(item => alreadySentUsername.Contains(item));

                                alreadySentAddresses.AddRange(toAddresses);
                                alreadySentUsername.AddRange(toUsername);

                                distributionEmail(emailTemplate, documentControlMaintenance, toAddresses, documentDistribution, dateString, scheme, hostString, pathBase);
                                distributionNotif(notificationTemplate, documentControlMaintenance, toUsername, loginUsername);
                            }

                            toAddresses.Clear();
                            toUsername.Clear();

                            userPositions = UserRepo.Instance.SearchPosition(new UserPosition { DEPARTMENT_ID = int.Parse(documentDistribution.DEPARTMENT_ID) }, db, null, null);
                            if (userPositions.Count > 0)
                            {
                                foreach (UserPosition userPosition in userPositions)
                                {
                                    User user = UserRepo.Instance.GetByKey(new User { USERNAME = userPosition.USERNAME }, db);
                                    if (user != null)
                                    {
                                        toAddresses.Add(user.EMAIL);
                                        toUsername.Add(user.USERNAME);
                                    }
                                }

                                toAddresses = toAddresses.Distinct().ToList();
                                toUsername = toUsername.Distinct().ToList();

                                distributionEmail(emailTemplate, documentControlMaintenance, toAddresses, documentDistribution, dateString, scheme, hostString, pathBase);
                                distributionNotif(notificationTemplate, documentControlMaintenance, toUsername, loginUsername);
                            }
                        }
                    }
                }
            }
        }

        public void distributionEmail(IList<MSystem> emailTemplate,
            DocumentControlMaintenance documentControlMaintenance, List<string> toAddresses,
            DocumentDistribution documentDistribution, string dateString, string scheme, string hostString, string pathBase)
        {
            User creator = UserRepo.Instance.GetByKey(new User { USERNAME = documentControlMaintenance.CREATED_BY }, db);

            string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
     .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);
            string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
            string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                .Replace("{DEPARTMENT}", documentDistribution.DEPARTMENT_CODE + " - " + documentDistribution.DEPARTMENT_NAME)
                .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE)
                .Replace("{DOCUMENT_NAME}", documentControlMaintenance.DOCUMENT_NAME)
                .Replace("{DOCUMENT_DATE}", dateString)
                .Replace("{REVISION}", documentControlMaintenance.REVISION.ToString())
                .Replace("{CREATOR}", creator.FULL_NAME)
                .Replace("{CREATOR_DIVISION}", documentControlMaintenance.DIVISION + " - " + documentControlMaintenance.DIVISION_NAME)
                .Replace("{CREATOR_DEPARTMENT}", documentControlMaintenance.DEPARTMENT_CODE + " - " + documentControlMaintenance.DEPARTMENT_NAME);
            string buttonLink = $"{scheme}://{hostString}{pathBase}" + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);

            if (NotificationSettingRepo.Instance.IsEmailEnabled("DISTRIBUTION", db))
                backgroundJobClient.Enqueue(() => EmailService.SendEmailAsync(toAddresses, subject, title, body, buttonLink));
        }

        public void distributionNotif(IList<MSystem> notificationTemplate, DocumentControlMaintenance documentControlMaintenance,
            List<string> toUsername, string loginUsername)
        {

            if (toUsername.Count > 0)
            {
                foreach (string username in toUsername)
                {
                    Notification notification = new Notification
                    {
                        NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                        NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                        NOTIFICATION_URL = notificationTemplate.Where(x => x.SYSTEM_CODE == "URL").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                        USERNAME = username,
                    };

                    DBResult result = NotificationRepo.Instance.Insert(notification, loginUsername, db);
                    if (result.status)
                    {
                        _hubContext.Clients.Group(username).SendAsync("ReceiveMessage", "New document distribution, check notification!");
                    }
                }
            }
        }

        // Sama dengan syarat tombolnya di _GridView: selain izin fungsi, sebagian aksi hanya
        // untuk jabatan Document Control. Null berarti diizinkan.
        JsonResult DenyP4D(string functionId, bool documentControlOnly = true)
        {
            JsonResult denied = DenyWithoutFunction(functionId);
            if (denied != null || !documentControlOnly || GetDocumentAccessControl() == true)
            {
                return denied;
            }
            return Json(new { status = false, message = "You are not authorized to perform this action." });
        }

        [HttpPost]
        public JsonResult Delete(DocumentControlMaintenance data, string path)
        {
            JsonResult denied = DenyP4D("P4D-DELETE", false);
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;

            try
            {
                result = P4DMaintenanceRepo.Delete(data, GetLoginUsername(), db);

                // SENGAJA TIDAK menghapus file fisik di sini (dulu ada, dihapus
                // 2026-08-16). TB_R_CTRL_DOCUMENT.FILE_PATH BUKAN file milik P4D
                // sendiri - sp_P4DMaintenance_Insert menyalin FILE_PATH apa
                // adanya dari TB_R_DOCUMENT saat registrasi P4D dibuat, jadi
                // path-nya menunjuk ke file SUMBER yang sama dengan yang dipakai
                // DocumentMaintenance/CopyRequest/PrintTrack dkk. Delete P4D di
                // sini cuma soft-delete baris registrasi (Draft/Rejected saja,
                // lihat sp_P4DMaintenance_Delete) - dokumen sumbernya sendiri
                // bisa saja masih Approved/Published dan aktif dipakai di tempat
                // lain, jadi menghapus file fisiknya di sini akan merusak modul
                // lain yang sama sekali tidak terlibat dengan aksi delete P4D
                // ini. Penghapusan file fisik dokumen HANYA boleh terjadi lewat
                // modul yang benar-benar memilikinya (DocumentMaintenance), yang
                // sudah punya guard status sendiri.
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult ApproveReject(DocumentControlMaintenance data)
        {
            JsonResult denied = DenyP4D("Y".Equals(data.IS_APPROVED) ? "P4D-RECEIVE" : "P4D-REJECT");
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;

            try
            {
                result = P4DMaintenanceRepo.ApproveReject(data, GetLoginUsername(), db);
                // Receive (Y) maupun Reject (N) dikabari ke pendaftar (request Hendra 2026-11-05)
                if (result.status && data.DOCUMENT_CTRL_ID != null)
                {
                    string mode = "Y".Equals(data.IS_APPROVED) ? "receive" : "reject";
                    backgroundJobClient.Enqueue(() => SendP4DStatusEmail((int)data.DOCUMENT_CTRL_ID, GetLoginUsername(), mode,
                        this.Request.Scheme, this.Request.Host.ToString(), this.Request.PathBase.ToString(), mode == "reject" ? data.REMARKS : null));
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UnReceive(DocumentControlMaintenance data)
        {
            JsonResult denied = DenyP4D("P4D-UNRECEIVE");
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;

            try
            {
                result = P4DMaintenanceRepo.UnReceive(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Accept(DocumentControlMaintenance data)
        {
            JsonResult denied = DenyP4D("P4D-RECEIVE");
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;

            try
            {
                result = P4DMaintenanceRepo.Accept(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult Download()
        {
            DataTable dt = new DataTable();
            /*
              { "data": "DOCUMENT_CODE", "name": "DOCUMENT_CODE" },
                { "data": "DOCUMENT_NAME", "name": "DOCUMENT_NAME" },
                { "data": "DIVISION", "name": "DIVISION" },
                { "data": "DEPARTMENT_NAME", "name": "DEPARTMENT_NAME" },
                { "data": "DOCUMENT_TYPE", "name": "DOCUMENT_TYPE" },
                { "data": "ITEM_CHANGED", "name": "ITEM_CHANGED" },
                { "data": "REASON", "name": "REASON" },
                { "data": "STATUS", "name": "STATUS" },
                { "data": "DOCUMENT_DATE", "name": "DOCUMENT_DATE" },
                { "data": "DISTRIBUTION", "name": "DISTRIBUTION" },
             */
            dt.Columns.Add("Document Code", typeof(string));
            dt.Columns.Add("Document Name", typeof(string));
            dt.Columns.Add("Division", typeof(string));
            dt.Columns.Add("Department", typeof(string));
            dt.Columns.Add("Document Type", typeof(string));
            dt.Columns.Add("Item Changed", typeof(string));
            dt.Columns.Add("Reason", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Document Date", typeof(string));

            DocumentControlMaintenance data = new DocumentControlMaintenance();
            var listData = P4DMaintenanceRepo.Search(data, null, db, 1, 0);

            DataRow row = dt.NewRow();
            row["Document Code"] = "Document Code";
            row["Document Name"] = "Document Name";
            row["Division"] = "Division";
            row["Department"] = "Department";
            row["Document Type"] = "Document Type";
            row["Item Changed"] = "Item Changed";
            row["Reason"] = "Reason";
            row["Status"] = "Status";
            row["Document Date"] = "Document Date";
            dt.Rows.Add(row);

            foreach (var item in listData)
            {
                row = dt.NewRow();
                row["Document Code"] = item.DOCUMENT_CODE.ToString();
                row["Document Name"] = item.DOCUMENT_NAME.ToString();
                row["Division"] = item.DIVISION.ToString();
                row["Department"] = item.DEPARTMENT_CODE.ToString();
                row["Document Type"] = item.DOCUMENT_TYPE.ToString();
                row["Item Changed"] = item.ITEM_CHANGED.ToString();
                row["Reason"] = item.REASON.ToString();
                row["Status"] = item.STATUS.ToString();
                row["Document Date"] = item.DOCUMENT_DATE.ToString();
                dt.Rows.Add(row);
            }

            string webRootPath = Environment.WebRootPath;

            using (FileStream stream = new FileStream(string.Concat(webRootPath, @"\Download\DOCUMENT\TemplateUpload.xlsx"), FileMode.Create, FileAccess.Write))
            {
                IWorkbook wb = new XSSFWorkbook();
                ISheet sheet = wb.CreateSheet("Sheet1");
                ICreationHelper cH = wb.GetCreationHelper();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    IRow rowExcel = sheet.CreateRow(i);
                    for (int j = 0; j < dt.Columns.Count; j++)
                    {
                        NPOI.SS.UserModel.ICell cell = rowExcel.CreateCell(j);
                        cell.SetCellValue(cH.CreateRichTextString(dt.Rows[i].ItemArray[j].ToString()));
                    }
                }
                wb.Write(stream);
            }
            byte[] bytes = System.IO.File.ReadAllBytes(string.Concat(webRootPath, @"\Download\DOCUMENT\TemplateUpload.xlsx"));
            string fileName = "P4DDocumentListDownload.xlsx";

            return File(bytes, "application/force-download", fileName);

        }

        public ActionResult SearchDocumentApproval(DocumentDistribution data)
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
                var listData = P4DMaintenanceRepo.SearchDocumentDistribution(data, db, pageNumber, pageSize);
                var dataCount = P4DMaintenanceRepo.SearchDocumentDistribution(data, db, null, null).Count;
                recordsTotal = dataCount;
                var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<JsonResult> AddEditDistributionAsync([FromBody] IList<DocumentDistribution> newDistributions)
        {
            JsonResult denied = DenyP4D("P4D-DISTRIBUTION", false);
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;
            //string folderName = "/Upload/";
            //string webRootPath = Environment.WebRootPath;

            try
            {
                DocumentControlMaintenance documentControl = P4DMaintenanceRepo.Instance.Search(
                    new DocumentControlMaintenance { DOCUMENT_TRANSACTION_ID = newDistributions[0].DOCUMENT_TRANSACTION_ID }, null, db, 1, 1).FirstOrDefault();

                if (documentControl != null && (documentControl.STATUS == "0" || documentControl.STATUS == "3" || documentControl.STATUS == "4"))
                {
                    return Json(new { status = false, message = "ERROR: Document must be received first before it can be distributed" });
                }

                var existDistribution = P4DMaintenanceRepo.Instance.SearchDocumentDistribution(
                    new DocumentDistribution { DOCUMENT_TRANSACTION_ID = newDistributions[0].DOCUMENT_TRANSACTION_ID }, db, null, null);

                var tobeAddedDistribution = newDistributions.Where(obj1 => !existDistribution.Any(obj2 => obj2.DEPARTMENT_ID == obj1.DEPARTMENT_ID)).ToList();
                var tobeDeletedDistribution = existDistribution.Where(obj2 => !newDistributions.Any(obj1 => obj1.DEPARTMENT_ID == obj2.DEPARTMENT_ID)).ToList();

                if (tobeAddedDistribution.Count() > 0)
                {
                    foreach (var distribution in tobeAddedDistribution)
                    {
                        result = P4DMaintenanceRepo.InsertDistribution(distribution, GetLoginUsername(), db);
                        if (!result.status)
                        {
                            return Json(new { status = false, message = result.message });
                        }
                    }
                }

                if (tobeDeletedDistribution.Count() > 0)
                {
                    foreach (var distribution in tobeDeletedDistribution)
                    {
                        result = P4DMaintenanceRepo.DeleteDistribution(distribution, GetLoginUsername(), db);
                        if (!result.status)
                        {
                            return Json(new { status = false, message = result.message });
                        }
                    }
                }

                return Json(new { status = true, message = "Data Saved Successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetByKeyDistribution(DocumentDistribution data)
        {
            try
            {
                DocumentDistribution result = P4DMaintenanceRepo.GetByKeyDistribution(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteDistribution(DocumentDistribution data, string path)
        {
            JsonResult denied = DenyP4D("P4D-DISTRIBUTION", false);
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                result = P4DMaintenanceRepo.DeleteDistribution(data, GetLoginUsername(), db);

                //if (result.status)
                //{
                //    if (data.FILE_PATH != null)
                //    {
                //        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                //        //Delete File
                //        if (System.IO.File.Exists(pathCheck))
                //        {
                //            System.IO.File.Delete(pathCheck);
                //        }
                //    }
                //}

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteDistributionMultiple(List<int> distributionIds)
        {
            JsonResult denied = DenyP4D("P4D-DISTRIBUTION", false);
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                foreach (var distributionId in distributionIds)
                {
                    result = P4DMaintenanceRepo.DeleteDistribution(new DocumentDistribution { DISTRIBUTION_ID = distributionId }, GetLoginUsername(), db);
                }

                return Json(new { status = true, message = "Data Deleted Successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SendDistribution(DocumentDistribution data, string path)
        {
            JsonResult denied = DenyP4D("P4D-SEND");
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                result = P4DMaintenanceRepo.SendDistribution(data, GetLoginUsername(), db);

                //if (result.status)
                //{
                //    if (data.FILE_PATH != null)
                //    {
                //        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                //        //Delete File
                //        if (System.IO.File.Exists(pathCheck))
                //        {
                //            System.IO.File.Delete(pathCheck);
                //        }
                //    }
                //}

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SendDistributionAll(DocumentDistribution data, string path)
        {
            JsonResult denied = DenyP4D("P4D-SEND");
            if (denied != null)
            {
                return denied;
            }

            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                result = P4DMaintenanceRepo.SendDistributionAll(data, GetLoginUsername(), db);
                if (result.status)
                {
                    DocumentControlMaintenance p4d = P4DMaintenanceRepo.Search(new DocumentControlMaintenance { DOCUMENT_TRANSACTION_ID = data.DOCUMENT_TRANSACTION_ID }, null, db, 1, 1).FirstOrDefault();
                    if (p4d?.DOCUMENT_CTRL_ID != null)
                    {
                        backgroundJobClient.Enqueue(() => SendP4DStatusEmail((int)p4d.DOCUMENT_CTRL_ID, GetLoginUsername(), "distributed",
                            this.Request.Scheme, this.Request.Host.ToString(), this.Request.PathBase.ToString(), null));
                    }

                    backgroundJobClient.Enqueue(() => SendDistributionEmail((int)data.DOCUMENT_TRANSACTION_ID, GetLoginUsername(),
                        this.Request.Scheme, this.Request.Host.ToString(), this.Request.PathBase.ToString()));
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDocumentTransactionSentByDepartment(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentControlMaintenance oDocumentMaintenance = new DocumentControlMaintenance();
                oDocumentMaintenance.STATUS = "2,5";

                if (q != "")
                    oDocumentMaintenance.DOCUMENT_CODE = '*' + q + '*';
                if (param != "")
                    oDocumentMaintenance.DEPARTMENT_ID = int.Parse(param);

                int result, pageInt, skip;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 0;
                }

                skip = pageInt * int.Parse(pageLimit);

                IList<DocumentControlMaintenance> dataList = P4DMaintenanceRepo.Search(oDocumentMaintenance, null, db, null, null)
                    .GroupBy(x => new { x.DOCUMENT_CODE }).Select(x => x.FirstOrDefault()).Skip(skip).Take(int.Parse(pageLimit)).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_CODE, id = data.DOCUMENT_TRANSACTION_ID.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
