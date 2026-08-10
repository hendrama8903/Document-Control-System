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
    public class DocumentControlDashboardController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        private readonly IBackgroundJobClient backgroundJobClient;
        private EmailService EmailService;
        private IHubContext<NotificationsHub> _hubContext;

        public DocumentControlDashboardController(DBContext db, IWebHostEnvironment environment, IBackgroundJobClient backgroundJobClient,
             IHubContext<NotificationsHub> hubContext, IOptions<EmailConfiguration> options)
        {
            this.db = db;
            Environment = environment;
            this.backgroundJobClient = backgroundJobClient;
            EmailService = new EmailService(options, environment);
            _hubContext = hubContext;
        }

        private DocumentControlDashboardRepo DocumentControlDashboardRepo = DocumentControlDashboardRepo.Instance;
        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private DepartmentMasterRepo departmentMasterRepo = DepartmentMasterRepo.Instance;
        private SectionMasterRepo sectionMasterRepo = SectionMasterRepo.Instance;
        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
        private P4DMaintenanceRepo P4DMaintenanceRepo = P4DMaintenanceRepo.Instance;

        public IActionResult Index(string DOCUMENT_CODE)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                string param = "/DocumentControlDashboard/Index?DOCUMENT_CODE=" + DOCUMENT_CODE;
                return RedirectToAction("Login", "Auth", new { param });
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentControlDashboard/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Send"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTCONTROLDASHBOARD-SEND");


            ViewData["Title"] = "Document Control";

            return View();
        }

        public JsonResult GetByKey(DocumentControlMaintenance data)
        {
            try
            {
                DocumentControlMaintenance result = DocumentControlDashboardRepo.GetByKey(data, db);
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
                    // Deliberately no OPERATION_TYPE filter here (unlike the old
                    // Distribution Approval behaviour of forcing =2/"Request" only):
                    // this page now covers the whole Document Control register -
                    // both user-requested entries (OPERATION_TYPE=2) and normal P4D
                    // registrations (OPERATION_TYPE=1), across every status, replacing
                    // the now-removed DocumentArchive module.
                    var listData = DocumentControlDashboardRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = DocumentControlDashboardRepo.Search(data, db, null, null).Count;
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
                    result = DocumentControlDashboardRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = DocumentControlDashboardRepo.Update(data, GetLoginUsername(), db);
                }
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

        public JsonResult GetDocumentByDepartment(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oSystem = new DocumentMaintenance();
                if (q != "")
                    oSystem.DEPARTMENT_CODE = '*' + q + '*';
                if (param != "")
                    oSystem.DEPARTMENT_CODE = param;

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentMaintenance> dataList = (IList<DocumentMaintenance>)documentMaintenanceRepo.Search(oSystem, null, db, pageInt, int.Parse(pageLimit));
                //.GroupBy(x => x.SYSTEM_VALUE).Select(x => x.First()).ToList();

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

        public IActionResult DownloadDocument(string path)
        {
            string webRootPath = Environment.WebRootPath;
            string fullPath = webRootPath + path;
            string[] split = path.Split("/");
            string fileName = split[4];

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

            return File(bytes, "application/force-download", fileName);
        }

        public IActionResult DownloadExcel()
        {
            IList<DocumentControlDashboardDocument> listData = DocumentControlDashboardRepo.Search(
                new DocumentControlMaintenance(), db, null, null);

            var memoryStream = new MemoryStream();
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Control");

            string[] headers = {
                "No", "Document No", "Document Name", "Category", "Revision", "Status",
                "Effective Date", "Next Review", "Document Owner", "Acknowledged",
                "Department", "Classification", "Last Updated"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (DocumentControlDashboardDocument item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_CODE);
                row.CreateCell(2).SetCellValue(item.DOCUMENT_NAME);
                row.CreateCell(3).SetCellValue(item.CATEGORY_CODE);
                row.CreateCell(4).SetCellValue(item.REVISION ?? 0);
                row.CreateCell(5).SetCellValue(item.DOC_STATUS_VAL);
                row.CreateCell(6).SetCellValue(item.DOCUMENT_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(7).SetCellValue(item.NEXT_REVIEW_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(8).SetCellValue(item.OWNER_FULL_NAME);
                row.CreateCell(9).SetCellValue((item.ACK_DONE ?? 0) + "/" + (item.ACK_TOTAL ?? 0));
                row.CreateCell(10).SetCellValue(item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME);
                row.CreateCell(11).SetCellValue(item.CLASSIFIED_VAL);
                row.CreateCell(12).SetCellValue((item.CHANGED_DT ?? item.CREATED_DT)?.ToString("dd-MM-yyyy HH:mm") ?? "");

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
                "DOCUMENT-CONTROL-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        public JsonResult SendDocument(DocumentControlMaintenance data)
        {
            DBResult result = null;

            try
            {
                result = DocumentControlDashboardRepo.SendDocument(data, GetLoginUsername(), db);
                if (result.status)
                {
                    backgroundJobClient.Enqueue(() => SendApproveRejectEmail((int)data.DOCUMENT_CTRL_ID, GetLoginUsername(),
                        "DITERIMA", this.Request.Scheme, this.Request.Host.ToString(), this.Request.PathBase.ToString(), data.REMARKS));
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public void SendApproveRejectEmail(int documentCtrlId, string loginUsername, string judgement, string scheme, string hostString, string pathBase, string remark)
        {
            DocumentControlMaintenance documentControlMaintenance = P4DMaintenanceRepo.Search(new DocumentControlMaintenance { DOCUMENT_CTRL_ID = documentCtrlId }, null, db, 1, 1).FirstOrDefault();

            if (documentControlMaintenance != null)
            {
                User user = UserRepo.Instance.GetByKey(new User { USERNAME = documentControlMaintenance.CREATED_BY }, db);
                if (user != null)
                    if (user != null)
                    {
                        CultureInfo cultureInfo = new CultureInfo("id-ID"); // Budaya Indonesia
                        DateTime date = (DateTime)documentControlMaintenance.DOCUMENT_DATE;
                        string dateString = date.ToString("dddd, d MMMM yyyy", cultureInfo);

                        List<string> toAddresses = new List<string>();
                        toAddresses.Add(user.EMAIL);

                        IList<MSystem> emailTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENTCONTROL_APPROVE_REJECT_EMAIL_TEMPLATE" }, db, null, null);
                        string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
                             .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);
                        string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
                        string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                            .Replace("{FULL_NAME}", user.FULL_NAME)
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE)
                            .Replace("{DOCUMENT_NAME}", documentControlMaintenance.DOCUMENT_NAME)
                            .Replace("{DOCUMENT_DATE}", dateString)
                            .Replace("{REVISION}", documentControlMaintenance.REVISION.ToString())
                            .Replace("{JUDGEMENT}", judgement)
                            .Replace("{REMARK}", remark);
                        string buttonLink = $"{scheme}://{hostString}{pathBase}" + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                        .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE);

                        if (NotificationSettingRepo.Instance.IsEmailEnabled("DOCUMENTCONTROL_APPROVE_REJECT", db))
                            backgroundJobClient.Enqueue(() => EmailService.SendEmailAsync(toAddresses, subject, title, body, buttonLink));

                        IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENTCONTROL_APPROVE_REJECT_NOTIFICATION" }, db, null, null);

                        Notification notification = new Notification
                        {
                            NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE)
                            .Replace("{JUDGEMENT}", judgement),
                            NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                            NOTIFICATION_URL = notificationTemplate.Where(x => x.SYSTEM_CODE == "URL").First().SYSTEM_VALUE
                            .Replace("{DOCUMENT_CODE}", documentControlMaintenance.DOCUMENT_CODE),
                            USERNAME = user.USERNAME,
                        };

                        DBResult result = NotificationRepo.Instance.Insert(notification, loginUsername, db);
                        if (result.status)
                        {

                            _hubContext.Clients.Group(user.USERNAME).SendAsync("ReceiveMessage", "Document approval update, check notification!");
                        }
                    }
            }
        }
    }
}
