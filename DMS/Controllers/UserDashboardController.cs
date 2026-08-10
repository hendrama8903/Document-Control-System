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
    public class UserDashboardController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        private readonly IBackgroundJobClient backgroundJobClient;
        private EmailService EmailService;
        private IHubContext<NotificationsHub> _hubContext;

        public UserDashboardController(DBContext db, IWebHostEnvironment environment, IBackgroundJobClient backgroundJobClient,
             IHubContext<NotificationsHub> hubContext, IOptions<EmailConfiguration> options)
        {
            this.db = db;
            Environment = environment;
            this.backgroundJobClient = backgroundJobClient;
            EmailService = new EmailService(options, environment);
            _hubContext = hubContext;
        }

        private UserDashboardRepo UserDashboardRepo = UserDashboardRepo.Instance;
        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private DepartmentMasterRepo departmentMasterRepo = DepartmentMasterRepo.Instance;
        private SectionMasterRepo sectionMasterRepo = SectionMasterRepo.Instance;
        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
        private P4DMaintenanceRepo P4DMaintenanceRepo = P4DMaintenanceRepo.Instance;
        private DocumentLogRepo documentLogRepo = DocumentLogRepo.Instance;

        public IActionResult Index(string DOCUMENT_CODE)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                string param = "/UserDashboard/Index?DOCUMENT_CODE=" + DOCUMENT_CODE;
                return RedirectToAction("Login", "Auth", new { param });
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/UserDashboard/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Request"] = HttpContext.Session.GetString("functionList").Contains("USERDASHBOARD-REQUEST");
            ViewData["Acknowledge"] = HttpContext.Session.GetString("functionList").Contains("USERDASHBOARD-PUBLISH");


            ViewData["Title"] = "My Documents";

            return View();
        }

        public JsonResult GetByKey(DocumentControlMaintenance data)
        {
            try
            {
                DocumentControlMaintenance result = UserDashboardRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
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
                    var listData = UserDashboardRepo.Search(data, GetLoginUsername(), db, pageNumber, pageSize);
                    var dataCount = UserDashboardRepo.Search(data, GetLoginUsername(), db, null, null).Count;
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
                    list.Add(new Select2() { text = data.DEPARTMENT_NAME, id = data.DEPARTMENT_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }



        public JsonResult GetDocumentCode(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaster oDocumentMaster = new DocumentMaster();
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

                IList<DocumentMaster> dataList = documentMasterRepo.Search(oDocumentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_CODE, id = string.Concat(data.DOCUMENT_ID.ToString(), "|", data.LEVEL.ToString()) });
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

        public async Task<JsonResult> AddEditAsync(string screenMode, DocumentControlMaintenance data)
        {
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
                    result = UserDashboardRepo.Insert(data, GetLoginUsername(), db);
                    if (result.returnId != 0)
                    {
                        backgroundJobClient.Enqueue(() => SendRequestEmailAsync(result.returnId, GetLoginUsername(),
                            this.Request.Scheme, this.Request.Host.ToString(), this.Request.PathBase.ToString()));
                    }
                }
                else
                {
                    result = UserDashboardRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public void SendRequestEmailAsync(int documentCtrlId, string loginUsername, string scheme, string hostString, string pathBase)
        {
            DocumentControlMaintenance documentControlMaintenance = P4DMaintenanceRepo.Search(new DocumentControlMaintenance { DOCUMENT_CTRL_ID = documentCtrlId }, null, db, 1, 1).FirstOrDefault();

            if (documentControlMaintenance != null)
            {
                IList<UserPosition> userPositions = UserRepo.Instance.SearchPosition(new UserPosition { DOCUMENT_CONTROL_ACCESS = "1" }, db, null, null);
                if (userPositions.Count > 0)
                {
                    CultureInfo cultureInfo = new CultureInfo("id-ID"); // Budaya Indonesia
                    DateTime date = (DateTime)documentControlMaintenance.DOCUMENT_DATE;
                    string dateString = date.ToString("dddd, d MMMM yyyy", cultureInfo);

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

                    toAddresses.Distinct().ToList();
                    toUsernames.Distinct().ToList();

                    User requester = UserRepo.Instance.GetByKey(new User { USERNAME = documentControlMaintenance.CREATED_BY }, db);

                    IList<MSystem> emailTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENTCONTROL_APPROVAL_REQUEST_EMAIL_TEMPLATE" }, db, null, null);
                    string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
                         .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);
                    string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
                    string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                        .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE)
                        .Replace("{DOCUMENT_NAME}", documentControlMaintenance.DOCUMENT_NAME)
                        .Replace("{DOCUMENT_DATE}", dateString)
                        .Replace("{REVISION}", documentControlMaintenance.REVISION.ToString())
                        .Replace("{REQUESTER}", requester.FULL_NAME)
                        .Replace("{DIVISION}", documentControlMaintenance.DIVISION + " - " + documentControlMaintenance.DIVISION_NAME)
                        .Replace("{DEPARTMENT}", documentControlMaintenance.DEPARTMENT_CODE + " - " + documentControlMaintenance.DEPARTMENT_NAME);
                    string buttonLink = $"{scheme}://{hostString}{pathBase}" + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                    .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);

                    if (NotificationSettingRepo.Instance.IsEmailEnabled("DOCUMENTCONTROL_APPROVAL_REQUEST", db))
                        backgroundJobClient.Enqueue(() => EmailService.SendEmailAsync(toAddresses, subject, title, body, buttonLink));

                    IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENTCONTROL_APPROVAL_REQUEST_NOTIFICATION" }, db, null, null);

                    foreach (string toUsername in toUsernames)
                    {
                        Notification notification = new Notification
                        {
                            NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                            .Replace("{REQUESTER}", documentControlMaintenance.CREATED_BY)
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                            NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
                            .Replace("{REQUESTER}", requester.FULL_NAME)
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

        public JsonResult AcknowledgeDocument(DocumentLog data, PublishHistory history)
        {
            DBResult result = null;

            try
            {
                data.LOG_TYPE = "4";

                result = documentLogRepo.Insert(data, GetLoginUsername(), db);

                result = P4DMaintenanceRepo.UpdateDistributionPublish(history, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDepartmentByDivision(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oSystem = new DocumentMaintenance();
                if (q != "")
                    oSystem.DIVISION = '*' + q + '*';
                if (param != "")
                    oSystem.DIVISION = param;

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentMaintenance> dataList = (IList<DocumentMaintenance>)documentMaintenanceRepo.SearchDepartmentByDivision(oSystem, db, pageInt, int.Parse(pageLimit));
                //.GroupBy(x => x.SYSTEM_VALUE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DEPARTMENT_NAME, id = data.DEPARTMENT_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
        public JsonResult GetUserDocumentNo(string q, string pageLimit, string page)
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

                IList<UserDashboardDocument> dataList = UserDashboardRepo.Search(oDocumentMaster, GetLoginUsername(), db, pageInt, int.Parse(pageLimit))
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

        public JsonResult GetUserDocumentName(string q, string pageLimit, string page)
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

                IList<UserDashboardDocument> dataList = UserDashboardRepo.Search(oDocumentMaster, GetLoginUsername(), db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_CODE).Select(x => x.First()).ToList();

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


        public IActionResult DownloadDocument(string path)
        {
            string webRootPath = Environment.WebRootPath;
            string fullPath = webRootPath + path;
            string[] split = path.Split("/");
            string fileName = split[4];

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

            return File(bytes, "application/force-download", fileName);
        }

        //Export grid dokumen di UserDashboard ke Excel - kolom mengikuti grid di halaman
        //(pola sama seperti DocumentMasterlistController.DownloadExcel), scope data sama
        //seperti grid (Search discoped ke login user lewat UserDashboardRepo.Search).
        public IActionResult DownloadExcel()
        {
            IList<UserDashboardDocument> listData = UserDashboardRepo.Search(
                new DocumentControlMaintenance(), GetLoginUsername(), db, null, null);

            var memoryStream = new MemoryStream();
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Request");

            string[] headers = {
                "No", "Document No", "Document Name", "Category", "Status", "Acknowledged", "Revision",
                "Division", "Department", "Classified", "Date", "Next Review", "Item Changed", "Reason",
                "Created By", "Created Date", "Changed By", "Changed Date"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (UserDashboardDocument item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_CODE);
                row.CreateCell(2).SetCellValue(item.DOCUMENT_NAME);
                row.CreateCell(3).SetCellValue(item.CATEGORY_CODE);
                row.CreateCell(4).SetCellValue(item.DOC_STATUS_VAL);
                row.CreateCell(5).SetCellValue(item.PUBLISH_FLAG == 1 ? "YES" : "NO");
                row.CreateCell(6).SetCellValue(item.REVISION ?? 0);
                row.CreateCell(7).SetCellValue(item.DIVISION + " - " + item.DIVISION_NAME);
                row.CreateCell(8).SetCellValue(item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME);
                row.CreateCell(9).SetCellValue(item.CLASSIFIED_VAL);
                row.CreateCell(10).SetCellValue(item.DOCUMENT_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(11).SetCellValue(item.NEXT_REVIEW_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(12).SetCellValue(item.ITEM_CHANGED);
                row.CreateCell(13).SetCellValue(item.REASON);
                row.CreateCell(14).SetCellValue(item.CREATED_BY);
                row.CreateCell(15).SetCellValue(item.CREATED_DT?.ToString("dd-MM-yyyy HH:mm:ss") ?? "");
                row.CreateCell(16).SetCellValue(item.CHANGED_BY);
                row.CreateCell(17).SetCellValue(item.CHANGED_DT?.ToString("dd-MM-yyyy HH:mm:ss") ?? "");

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
                "DOCUMENT-REQUEST-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
