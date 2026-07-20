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
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf.Advanced;
using NPOI.POIFS.Crypt.Dsig;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Org.BouncyCastle.Asn1.Pkcs;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PdfSharp.Charting;

namespace DMS.Controllers
{
    public class DocumentMaintenanceController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        private readonly PrintingService printingService;
        private readonly IBackgroundJobClient backgroundJobClient;
        private EmailService EmailService;
        private IHubContext<NotificationsHub> _hubContext;

        public DocumentMaintenanceController(DBContext db, IOptions<EmailConfiguration> options, IWebHostEnvironment environment,
            PrintingService printingService, IBackgroundJobClient backgroundJobClient, IHubContext<NotificationsHub> hubContext)
        {
            this.db = db;
            Environment = environment;
            EmailService = new EmailService(options, environment);
            this.printingService = printingService;
            this.backgroundJobClient = backgroundJobClient;
            _hubContext = hubContext;
        }

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private DepartmentMasterRepo departmentMasterRepo = DepartmentMasterRepo.Instance;
        private SectionMasterRepo sectionMasterRepo = SectionMasterRepo.Instance;
        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
        private WorkflowRepo workflowRepo = WorkflowRepo.Instance;
        private ApprovalRepo approvalRepo = ApprovalRepo.Instance;
        private LogMonitoringRepo logRepo = LogMonitoringRepo.Instance;
        private DocumentLogRepo documentLogRepo = DocumentLogRepo.Instance;
        private P4DMaintenanceRepo P4DMaintenanceRepo = P4DMaintenanceRepo.Instance;

        public IActionResult Index(string DOCUMENT_CODE)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                string param = "/DocumentMaintenance/Index?DOCUMENT_CODE=" + DOCUMENT_CODE;
                return RedirectToAction("Login", "Auth", new { param });
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentMaintenance/Index"))
                {
                    return StatusCode(403);
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-DELETE");
            ViewData["Download"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-DOWNLOAD");
            ViewData["Approve"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-APPROVE");
            ViewData["Delete-FilePath"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-DELETE-FILEPATH");


            ViewData["Title"] = "Documents";

            return View();
        }

        public JsonResult GetByKey(DocumentMaintenance data)
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

        public ActionResult Search(DocumentMaintenance data, bool initialMode)
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
                    var listData = documentMaintenanceRepo.Search(data, GetLoginUsername(), db, pageNumber, pageSize);
                    var dataCount = documentMaintenanceRepo.Search(data, GetLoginUsername(), db, null, null).Count;
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

        public ActionResult SearchHistory(DocumentHistory data, bool initialMode)
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
                    var listData = documentMaintenanceRepo.SearchHistory(data, GetLoginUsername(), db, pageNumber, pageSize);
                    var dataCount = documentMaintenanceRepo.SearchHistory(data, GetLoginUsername(), db, null, null).Count;
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

        public ActionResult SearchDocumentLog(DocumentLog data)
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
                var listData = documentLogRepo.Search(data, db, pageNumber, pageSize);
                var dataCount = documentLogRepo.Search(data, db, null, null).Count;
                recordsTotal = dataCount;
                var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult CountTotalLog(DocumentLog data)
        {
            try
            {
                int totalView = documentLogRepo.Search(new DocumentLog { DOCUMENT_TRANSACTION_ID = data.DOCUMENT_TRANSACTION_ID, LOG_TYPE = "1" }, db, null, null).Count();
                int totalPrint = documentLogRepo.Search(new DocumentLog { DOCUMENT_TRANSACTION_ID = data.DOCUMENT_TRANSACTION_ID, LOG_TYPE = "2" }, db, null, null).Count();
                int totalDownload = documentLogRepo.Search(new DocumentLog { DOCUMENT_TRANSACTION_ID = data.DOCUMENT_TRANSACTION_ID, LOG_TYPE = "3" }, db, null, null).Count();
                int totalPublished = documentLogRepo.Search(new DocumentLog { DOCUMENT_TRANSACTION_ID = data.DOCUMENT_TRANSACTION_ID, LOG_TYPE = "4" }, db, null, null).Count();

                return Json(new { status = true, totalView = totalView, totalPrint = totalPrint, totalDownload = totalDownload, totalPublished = totalPublished });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
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
                    list.Add(new Select2() { text = data.DEPARTMENT_CODE, id = data.DEPARTMENT_ID.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDocumentNo(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oDocumentMaintenance = new DocumentMaintenance();
                if (q != null)
                    oDocumentMaintenance.DOCUMENT_CODE = '*' + q + '*';

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

                IList<DocumentMaintenance> dataList = documentMaintenanceRepo.Search(oDocumentMaintenance, null, db, null, null)
                    .GroupBy(x => new { x.DOCUMENT_CODE }).Select(x => x.FirstOrDefault()).Skip(skip).Take(int.Parse(pageLimit)).ToList();

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

        public JsonResult GetDocumentNoLoginBased(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oDocumentMaintenance = new DocumentMaintenance();
                oDocumentMaintenance.DEPARTMENT_ID = GetLoginDepartmentId();

                if (q != null)
                    oDocumentMaintenance.DOCUMENT_CODE = '*' + q + '*';

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

                IList<DocumentMaintenance> dataList = documentMaintenanceRepo.Search(oDocumentMaintenance, null, db, null, null)
                    .GroupBy(x => new { x.DOCUMENT_CODE }).Select(x => x.FirstOrDefault()).Skip(skip).Take(int.Parse(pageLimit)).ToList();

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

        public JsonResult GetDocumentName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oDocumentMaintenance = new DocumentMaintenance();

                if (q != null)
                    oDocumentMaintenance.DOCUMENT_TRANSACTION_NAME = '*' + q + '*';

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

                IList<DocumentMaintenance> dataList = documentMaintenanceRepo.Search(oDocumentMaintenance, null, db, null, null)
                    .GroupBy(x => new { x.DOCUMENT_CODE }).Select(x => x.FirstOrDefault()).Skip(skip).Take(int.Parse(pageLimit)).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_TRANSACTION_NAME, id = data.DOCUMENT_TRANSACTION_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDocumentNameLoginBased(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oDocumentMaintenance = new DocumentMaintenance();
                oDocumentMaintenance.DEPARTMENT_ID = GetLoginDepartmentId();

                if (q != null)
                    oDocumentMaintenance.DOCUMENT_TRANSACTION_NAME = '*' + q + '*';

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

                IList<DocumentMaintenance> dataList = documentMaintenanceRepo.Search(oDocumentMaintenance, null, db, null, null)
                    .GroupBy(x => new { x.DOCUMENT_CODE }).Select(x => x.FirstOrDefault()).Skip(skip).Take(int.Parse(pageLimit)).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_TRANSACTION_NAME, id = data.DOCUMENT_TRANSACTION_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDocumentCodeLoginBased(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oDocumentMaintenance = new DocumentMaintenance();
                oDocumentMaintenance.DEPARTMENT_ID = GetLoginDepartmentId();
                oDocumentMaintenance.NOT_EXIST_FLAG = "1";
                oDocumentMaintenance.STATUS = "1";

                if (q != null)
                    oDocumentMaintenance.DOCUMENT_CODE = '*' + q + '*';

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

                IList<DocumentMaintenance> dataList = documentMaintenanceRepo.Search(oDocumentMaintenance, null, db, null, null)
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

        public JsonResult GetDocumentNoAllowedRevision(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oDocumentMaintenance = new DocumentMaintenance();
                oDocumentMaintenance.DEPARTMENT_ID = GetLoginDepartmentId();
                oDocumentMaintenance.REVISION_ALLOWAL_FLAG = "1";

                if (q != null)
                    oDocumentMaintenance.DOCUMENT_CODE = '*' + q + '*';

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

                IList<DocumentMaintenance> dataList = documentMaintenanceRepo.Search(oDocumentMaintenance, GetLoginUsername(), db, null, null)
                    .GroupBy(x => new { x.DOCUMENT_CODE }).Select(x => x.FirstOrDefault()).Skip(skip).Take(int.Parse(pageLimit)).ToList();

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

        public JsonResult GetDocumentCodeByLevel(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaster oDocumentMaster = new DocumentMaster();
                if (q != null)
                    oDocumentMaster.DOCUMENT_CODE = '*' + q + '*';

                if (param != null)
                    oDocumentMaster.LEVEL = int.Parse(param);

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
        public JsonResult GenerateDocumentNo()
        {
            try
            {
                DocumentMaintenance result = documentMaintenanceRepo.GenerateDocumentNo(db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetLevelByDocumentCode(DocumentMaster data)
        {
            try
            {
                DocumentMaster result = documentMasterRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
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

        public JsonResult GetDocumentNoApprovedByDepartment(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaintenance oDocumentMaintenance = new DocumentMaintenance();
                oDocumentMaintenance.STATUS = "1";

                if (q != "")
                    oDocumentMaintenance.DEPARTMENT_CODE = '*' + q + '*';
                if (param != "")
                    oDocumentMaintenance.DEPARTMENT_CODE = param;

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

                IList<DocumentMaintenance> dataList = documentMaintenanceRepo.Search(oDocumentMaintenance, null, db, null, null)
                    .GroupBy(x => new { x.DOCUMENT_CODE }).Select(x => x.FirstOrDefault()).Skip(skip).Take(int.Parse(pageLimit)).ToList();

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

        public async Task<JsonResult> AddEditAsync(string screenMode, DocumentMaintenance data, string REMARK)
        {
            DBResult result = null;
            string folderName = "/Upload/";
            string webRootPath = Environment.WebRootPath;

            db.Database.BeginTransaction();

            try
            {
                if (Request.Form.Files.Count > 0)
                {
                    IFormFile file = Request.Form.Files[0];

                    // Validasi dini: pastikan konfigurasi template pengesahan tersedia
                    // untuk orientasi sheet file yang di-upload. Tanpa ini, dokumen
                    // tetap tersimpan tapi PDF pengesahannya kosong tanpa peringatan.
                    string templateValidationError = ValidateTemplateConfiguration(file, data.DOCUMENT_ID);
                    if (templateValidationError != null)
                    {
                        db.Database.RollbackTransaction();
                        return Json(new { status = false, message = templateValidationError });
                    }

                    MSystem mSystem = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "DOCUMENT_TRANSACTION" }, db);

                    string extension = Path.GetExtension(file.FileName);
                    string path = folderName + mSystem.SYSTEM_VALUE.Trim();
                    string pathSave = webRootPath + folderName + mSystem.SYSTEM_VALUE.Trim();
                    string documentname = data.DOCUMENT_TRANSACTION_NAME;
                    documentname = documentname.Replace(" ", "_").Replace("/", "_");
                    string fileName = documentname + "-" + DateTime.Now.ToFileTime() + extension;
                    string finalPath = pathSave + fileName;
                    string pathCheck = "";


                    if (!Directory.Exists(pathSave))
                    {
                        Directory.CreateDirectory(pathSave);
                    }


                    data.FILE_PATH = path + fileName;

                    //Save File to Local Storage
                    using (var stream = new FileStream(finalPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                }


                if (screenMode == "ADD")
                {
                    result = documentMaintenanceRepo.Insert(data, GetLoginUsername(), db);

                    if (!result.status)
                    {
                        return Json(new { status = false, message = result.message });
                    }

                    if (result.returnId != 0)
                    {
                        SendApprovalEmailAsync(result.returnId);
                    }
                }
                else
                {
                    data.STATUS = "0";
                    result = documentMaintenanceRepo.Update(data, GetLoginUsername(), REMARK, db);


                    // ✅ TAMBAHKAN INI: hapus cache PDF lama supaya generate ulang saat dibuka
                    if (result.status && data.FILE_PATH != null)
                    {
                        string[] splitCache = data.FILE_PATH.Split("/");
                        if (splitCache.Length > 4)
                        {
                            string oldFileName = splitCache[4];
                            string oldExt = GetFileExtension(oldFileName);
                            string cachedPdf = webRootPath + "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                                + oldFileName.Replace("." + oldExt, ".pdf");

                            if (System.IO.File.Exists(cachedPdf))
                                System.IO.File.Delete(cachedPdf);
                        }
                    }
                    // ✅ SAMPAI SINI

                    if (data.DOCUMENT_TRANSACTION_ID != 0)
                    {
                        SendApprovalEmailAsync((int) data.DOCUMENT_TRANSACTION_ID);
                    }
                }

                db.Database.CommitTransaction();
                return Json(result);
            }
            catch (Exception ex)
            {
                db.Database.RollbackTransaction();
                return Json(new { status = false, message = ex.Message });
            }
        }

        public void SendApprovalEmailAsync(int documentTransactionId)
        {
            DocumentMaintenance documentMaintenance = documentMaintenanceRepo.Search(
                new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentTransactionId }, GetLoginUsername(), db, 1, 1).FirstOrDefault();

            if (documentMaintenance != null)
            {
                ApprovalHeader approvalHeader = ApprovalRepo.Instance.GetApprovalHeader((int)documentMaintenance.APPROVAL_ID, db);

                if (approvalHeader != null)
                {
                    IList<ApprovalDetail> approvalDetails = ApprovalRepo.Instance.GetApprovalDetail((int)documentMaintenance.APPROVAL_ID, db, null, null);
                    ApprovalDetail approvalDetail = approvalDetails.Where(x => x.WORKFLOW_SEQ == approvalHeader.CURRENT_SEQ).FirstOrDefault();

                    if (approvalDetail != null)
                    {
                        User user = UserRepo.Instance.GetByKey(new User { USERNAME = approvalDetail.APPROVER }, db);
                        if (user != null)
                        {
                            CultureInfo cultureInfo = new CultureInfo("id-ID"); // Budaya Indonesia
                            DateTime date = (DateTime)documentMaintenance.DOCUMENT_DATE;
                            string dateString = date.ToString("dddd, d MMMM yyyy", cultureInfo);

                            List<string> toAddresses = new List<string>();

                            toAddresses.Add(user.EMAIL);

                            IList<MSystem> emailTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE" }, db, null, null);
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

                            IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_APPROVAL_REQUEST_NOTIFICATION" }, db, null, null);
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
            }
        }

        public void SendApproveRejectEmail(int documentTransactionId, string mode, string remark)
        {
            string judgement = "DITOLAK";
            if (mode == "approve")
            {
                judgement = "DITERIMA";
            }

            DocumentMaintenance documentMaintenance = documentMaintenanceRepo.Search(
                new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentTransactionId }, GetLoginUsername(), db, 1, 1).FirstOrDefault();

            if (documentMaintenance != null)
            {
                User user = UserRepo.Instance.GetByKey(new User { USERNAME = documentMaintenance.CREATED_BY }, db);
                if (user != null)
                {
                    CultureInfo cultureInfo = new CultureInfo("id-ID"); // Budaya Indonesia
                    DateTime date = (DateTime)documentMaintenance.DOCUMENT_DATE;
                    string dateString = date.ToString("dddd, d MMMM yyyy", cultureInfo);

                    List<string> toAddresses = new List<string>();

                    toAddresses.Add(user.EMAIL);

                    IList<MSystem> emailTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_APPROVE_REJECT_EMAIL_TEMPLATE" }, db, null, null);
                    string subject = emailTemplate.Where(x => x.SYSTEM_CODE == "SUBJECT").First().SYSTEM_VALUE
                         .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE);
                    string title = emailTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE;
                    string body = emailTemplate.Where(x => x.SYSTEM_CODE == "BODY").First().SYSTEM_VALUE
                        .Replace("{FULL_NAME}", user.FULL_NAME)
                        .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE)
                        .Replace("{DOCUMENT_NAME}", documentMaintenance.DOCUMENT_TRANSACTION_NAME)
                        .Replace("{DOCUMENT_DATE}", dateString)
                        .Replace("{REVISION}", documentMaintenance.REVISION.ToString())
                        .Replace("{REMARK}", remark)
                        .Replace("{JUDGEMENT}", judgement);
                    string buttonLink = $"{this.Request.Scheme}://{this.Request.Host}{this.Request.PathBase}" + emailTemplate.Where(x => x.SYSTEM_CODE == "BUTTON_LINK").First().SYSTEM_VALUE
                        .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE);

                    backgroundJobClient.Enqueue(() => EmailService.SendEmailAsync(toAddresses, subject, title, body, buttonLink));

                    IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "DOCUMENT_APPROVE_REJECT_NOTIFICATION" }, db, null, null);
                    Notification notification = new Notification
                    {
                        NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
                        .Replace("{JUDGEMENT}", judgement)
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
                        _hubContext.Clients.Group(user.USERNAME).SendAsync("ReceiveMessage", "Document approval update, check notification!");
                    }
                }
            }
        }

        public JsonResult Delete(DocumentMaintenance data, string path)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                result = documentMaintenanceRepo.Delete(data, GetLoginUsername(), db);

                if (result.status)
                {
                    if (data.FILE_PATH != null)
                    {
                        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                        //Delete File
                        if (System.IO.File.Exists(pathCheck))
                        {
                            System.IO.File.Delete(pathCheck);
                        }
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadReport()
        {
            DocumentMaintenance documentMaintenanceParam = new();
            string webRootPath = Environment.WebRootPath;
            string fileName = @"Document/DocumentMaintenance/DocumentMaintenance-Template.xlsx";
            var memoryStream = new MemoryStream();

            string DOCUMENT_NO = HttpContext.Request.Query["DOCUMENT_CODE"].ToString();
            if (DOCUMENT_NO != "null")
            {
                documentMaintenanceParam.DOCUMENT_CODE = DOCUMENT_NO;
            }

            string DOCUMENT_NAME = HttpContext.Request.Query["DOCUMENT_NAME"].ToString();
            if (DOCUMENT_NAME != "null")
            {
                documentMaintenanceParam.DOCUMENT_NAME = DOCUMENT_NAME;
            }

            string DOCUMENT_YEAR = HttpContext.Request.Query["DOCUMENT_YEAR"].ToString();
            if (DOCUMENT_YEAR != "null")
            {
                documentMaintenanceParam.DOCUMENT_YEAR = DOCUMENT_YEAR;
            }

            IList<DocumentMaintenance> documentMaintenances = documentMaintenanceRepo.Search(documentMaintenanceParam, null, db, null, null);

            var fs = new FileStream(Path.Combine(webRootPath, fileName), FileMode.Open, FileAccess.Read);
            XSSFWorkbook oHSSFWorkbook = new XSSFWorkbook(fs);

            ICellStyle styleContentLeft = CellStyleLeft(oHSSFWorkbook);
            ICellStyle styleContentCenter = CellStyleCenter(oHSSFWorkbook);

            ISheet oISheet = oHSSFWorkbook.GetSheet("Document Data");

            int i = 1;
            int row = 8;
            foreach (DocumentMaintenance item in documentMaintenances)
            {
                IRow oIRow = oISheet.CreateRow(row);
                NPOI.SS.UserModel.ICell oICell = oIRow.CreateCell(0);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(i);
                oICell.CellStyle = styleContentCenter;

                oICell = oIRow.CreateCell(1);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.DOCUMENT_CODE);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(2);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.DOCUMENT_TRANSACTION_NAME);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(3);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.DIVISION + " - " + item.DIVISION_NAME);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(4);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(5);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.CLASSIFIED_VAL);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(6);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.STATUS_DISPLAY);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(7);
                oICell.SetCellType(CellType.Numeric);
                oICell.SetCellValue(item.REVISION.ToString());
                oICell.CellStyle = styleContentCenter;

                oICell = oIRow.CreateCell(8);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.ITEM_CHANGED);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(9);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.REASON);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(10);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.REFERENCE_NO);
                oICell.CellStyle = styleContentLeft;

                oICell = oIRow.CreateCell(11);
                oICell.SetCellType(CellType.String);
                oICell.SetCellValue(item.SOURCE);
                oICell.CellStyle = styleContentLeft;

                i++;
                row++;
            }

            oHSSFWorkbook.Write(memoryStream);

            memoryStream.Position = 0;
            return File(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "DOCUMENT-MAINTENANCE-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        public IActionResult DownloadAttachment(DocumentMaintenance documentMaintenance, string type)
        {
            DBResult result;

            try
            {
                DocumentLog documentLog = new DocumentLog
                {
                    DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID,
                    LOG_TYPE = "3"
                };

                result = insertDocumentLog(documentLog, type);
                if (!result.status)
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    ViewBag.ErrorMessage = result.message;
                    return View("Error500");
                }

                string webRootPath = Environment.WebRootPath;
                string fullPath = webRootPath + documentMaintenance.FILE_PATH;

                if (!System.IO.File.Exists(fullPath))
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    ViewBag.ErrorMessage = "File Not Found";
                    return View("Error500");
                }

                string[] split = documentMaintenance.FILE_PATH.Split("/");
                string fileName = split[4];

                byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

                return File(bytes, "application/force-download", fileName);
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ViewBag.ErrorMessage = ex.Message;
                return View("Error500");
            }
        }

        //Original Code
        //public DBResult insertDocumentLog(DocumentLog documentLog, string type)
        //{
        //    DBResult result = documentLogRepo.Insert(documentLog, GetLoginUsername(), db);

        //    if (type != "1")
        //    {
        //        MSystem logType = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "LOG_TYPE", SYSTEM_CODE = documentLog.LOG_TYPE }, db);
        //        if (logType != null)
        //        {
        //            string logTypeValue = logType.SYSTEM_VALUE + "ed";
        //            IList<User> documentControlAccessUsers = UserRepo.Instance.Search(new User { DOCUMENT_CONTROL_ACCESS = "1" }, db, null, null);
        //            DocumentMaintenance documentMaintenance = documentMaintenanceRepo.Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentLog.DOCUMENT_TRANSACTION_ID }, null, db, 1, 1).FirstOrDefault();

        //            if (documentMaintenance != null)
        //            {
        //                string notifText = "Document " + documentMaintenance.DOCUMENT_CODE + " is " + logTypeValue + " by " + GetLoginUsername();
        //                IList<MSystem> notificationTemplate = MSystemRepo.Instance.Search(new MSystem { SYSTEM_TYPE = "LOG_NOTIFICATION" }, db, null, null);

        //                Notification notification = new Notification
        //                {
        //                    NOTIFICATION_TEXT = notificationTemplate.Where(x => x.SYSTEM_CODE == "TEXT").First().SYSTEM_VALUE
        //                    .Replace("{TEXT}", notifText),
        //                    NOTIFICATION_TITLE = notificationTemplate.Where(x => x.SYSTEM_CODE == "TITLE").First().SYSTEM_VALUE
        //                    .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE),
        //                    NOTIFICATION_URL = notificationTemplate.Where(x => x.SYSTEM_CODE == "URL").First().SYSTEM_VALUE
        //                    .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE),
        //                };

        //                documentControlAccessUsers = documentControlAccessUsers.Where(obj => obj.USERNAME != GetLoginUsername()).ToList();

        //                foreach (User user in documentControlAccessUsers)
        //                {
        //                    notification.USERNAME = user.USERNAME;
        //                    DBResult results = NotificationRepo.Instance.Insert(notification, GetLoginUsername(), db);
        //                    if (results.status)
        //                    {
        //                        _hubContext.Clients.Group(user.USERNAME).SendAsync("ReceiveMessage", notifText);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    return result;
        //}
        //End Original Code

        //Start custom
        public DBResult insertDocumentLog(DocumentLog documentLog, string type)
        {
            DBResult result = documentLogRepo.Insert(documentLog, GetLoginUsername(), db);

            if (type != "1")
            {
                MSystem logType = mSystemRepo.GetByKey(
                    new MSystem { SYSTEM_TYPE = "LOG_TYPE", SYSTEM_CODE = documentLog.LOG_TYPE }, db);

                if (logType != null)
                {
                    string logTypeValue = logType.SYSTEM_VALUE + "ed";
                    IList<User> documentControlAccessUsers = UserRepo.Instance
                        .Search(new User { DOCUMENT_CONTROL_ACCESS = "1" }, db, null, null);
                    DocumentMaintenance documentMaintenance = documentMaintenanceRepo
                        .Search(new DocumentMaintenance
                        {
                            DOCUMENT_TRANSACTION_ID = documentLog.DOCUMENT_TRANSACTION_ID
                        }, null, db, 1, 1).FirstOrDefault();

                    if (documentMaintenance != null)
                    {
                        string notifText = "Document " + documentMaintenance.DOCUMENT_CODE
                            + " is " + logTypeValue + " by " + GetLoginUsername();

                        IList<MSystem> notificationTemplate = MSystemRepo.Instance
                            .Search(new MSystem { SYSTEM_TYPE = "LOG_NOTIFICATION" }, db, null, null);

                        // ✅ Ganti .First() dengan .FirstOrDefault() + null check
                        var textTemplate = notificationTemplate
                            .FirstOrDefault(x => x.SYSTEM_CODE == "TEXT");
                        var titleTemplate = notificationTemplate
                            .FirstOrDefault(x => x.SYSTEM_CODE == "TITLE");
                        var urlTemplate = notificationTemplate
                            .FirstOrDefault(x => x.SYSTEM_CODE == "URL");

                        // ✅ Lanjut hanya kalau semua template ada
                        if (textTemplate == null || titleTemplate == null || urlTemplate == null)
                            return result;

                        Notification notification = new Notification
                        {
                            NOTIFICATION_TEXT = textTemplate.SYSTEM_VALUE
                                .Replace("{TEXT}", notifText),
                            NOTIFICATION_TITLE = titleTemplate.SYSTEM_VALUE
                                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE),
                            NOTIFICATION_URL = urlTemplate.SYSTEM_VALUE
                                .Replace("{DOCUMENT_CODE}", documentMaintenance.DOCUMENT_CODE),
                        };

                        documentControlAccessUsers = documentControlAccessUsers
                            .Where(obj => obj.USERNAME != GetLoginUsername()).ToList();

                        foreach (User user in documentControlAccessUsers)
                        {
                            notification.USERNAME = user.USERNAME;
                            DBResult results = NotificationRepo.Instance
                                .Insert(notification, GetLoginUsername(), db);
                            if (results.status)
                            {
                                _hubContext.Clients.Group(user.USERNAME)
                                    .SendAsync("ReceiveMessage", notifText);
                            }
                        }
                    }
                }
            }

            return result;
        }
        //end custom

        public IActionResult ViewAttachment(DocumentMaintenance documentMaintenance, string type)
        {
            DBResult result;

            try
            {
                DocumentLog documentLog = new DocumentLog
                {
                    DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID,
                    LOG_TYPE = "1"
                };

                result = insertDocumentLog(documentLog, type);
                if (!result.status)
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    ViewBag.ErrorMessage = result.message;
                    return View("Error500");
                }

                string webRootPath = Environment.WebRootPath;
                string fullPath = webRootPath + documentMaintenance.FILE_PATH;

                if (!System.IO.File.Exists(fullPath))
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    ViewBag.ErrorMessage = "File Not Found";
                    return View("Error500");
                }

                // Obsolete-control (Jul 2026): dokumen yang sudah disupersede tidak lagi
                // punya baris live di TB_R_DOCUMENT (lihat sp_DocumentMaintenance_SupersedeRevision) -
                // kalau tidak ketemu di sana, ini dokumen lama/obsolete. Selalu tampilkan
                // watermark OBSOLETE untuk itu, terlepas dari parameter `type`.
                // loginUser sengaja null - ini cek eksistensi murni, BUKAN pencarian
                // ter-scope divisi/department viewer (Search akan filter berdasarkan
                // TB_M_USER_POS milik loginUser kalau diisi, yang bisa membuat dokumen
                // current dari divisi lain salah dianggap obsolete).
                bool isObsolete = documentMaintenanceRepo
                    .Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID }, null, db, 1, 1)
                    .FirstOrDefault() == null;
                string watermarkText = isObsolete ? "OBSOLETE" : "CONTROLLED COPY";
                bool shouldWatermark = isObsolete || type == "3";

                string[] split = documentMaintenance.FILE_PATH.Split("/");
                string fileName = split[4];
                string extension = GetFileExtension(fileName);
                string pengesahanModifiedfileNames, finalPath;

                if (extension.Equals("pdf"))
                {
                    string outputFileName = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_" + fileName;
                    string outputFullPath = webRootPath + outputFileName;

                    if (System.IO.File.Exists(outputFullPath))
                    {
                        System.IO.File.Delete(outputFullPath);
                    }

                    // Copy the source file to the destination folder
                    System.IO.File.Copy(fullPath, outputFullPath);


                    pengesahanModifiedfileNames = outputFileName;
                }
                else
                {

                    // ✅ TAMBAHKAN INI: cek cache dulu
                    string cachedPdfRelative = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                        + fileName.Replace("." + extension, ".pdf");
                    string cachedPdfFullPath = webRootPath + cachedPdfRelative;

                    if (System.IO.File.Exists(cachedPdfFullPath))
                    {
                        string servePath = cachedPdfRelative;

                        if (shouldWatermark)
                        {
                            string watermarkedRelative = cachedPdfRelative.Replace(".pdf", "_wm.pdf");
                            string watermarkedFullPath = webRootPath + watermarkedRelative;
                            System.IO.File.Copy(cachedPdfFullPath, watermarkedFullPath, overwrite: true);
                            AddWatermark(watermarkedFullPath, watermarkedFullPath, watermarkText);
                            servePath = watermarkedRelative;
                        }

                        ViewData["Title"] = "Document Preview";
                        ViewData["FilePath"] = servePath; // ← pakai servePath, bukan cachedPdfRelative
                        return View("~/Views/Preview/PDFPreview.cshtml");
                    }
                    // ✅ SAMPAI SINI

                    pengesahanModifiedfileNames = pengesahanModifiedfileName(webRootPath, documentMaintenance.FILE_PATH, documentMaintenance);
                    string pengesahanModifiedFullPath = webRootPath + pengesahanModifiedfileNames;
                    if (pengesahanModifiedfileNames == null)
                    {
                        ViewBag.ErrorMessage = "Error when modifying pengesahan header";
                        return Json(new { status = false, message = ViewBag.ErrorMessage });
                    }

                    split = pengesahanModifiedfileNames.Split("/");
                    fileName = split[4];

                    extension = GetFileExtension(fileName);

                    result = ConvertToPdf(pengesahanModifiedFullPath, pengesahanModifiedFullPath);
                    if (!result.status)
                    {
                        Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        ViewBag.ErrorMessage = result.message;
                        return View("Error500");
                    }

                    if (System.IO.File.Exists(pengesahanModifiedFullPath))
                    {
                        System.IO.File.Delete(pengesahanModifiedFullPath);
                    }
                }
                // Ganti bagian ini:
                // finalPath = webRootPath + pengesahanModifiedfileNames.Replace(extension, "pdf");
                // if (type == "3") AddWatermark(finalPath, finalPath, "CONTROLLED COPY");

                string convertedRelative = pengesahanModifiedfileNames.Replace(extension, "pdf");
                string convertedFullPath = webRootPath + convertedRelative;
                string serveRelative = convertedRelative;

                if (shouldWatermark)
                {
                    string watermarkedRelative = convertedRelative.Replace(".pdf", "_wm.pdf");
                    string watermarkedFullPath = webRootPath + watermarkedRelative;
                    System.IO.File.Copy(convertedFullPath, watermarkedFullPath, overwrite: true);
                    AddWatermark(watermarkedFullPath, watermarkedFullPath, watermarkText);
                    serveRelative = watermarkedRelative;
                }

                ViewData["Title"] = "Document Preview";
                ViewData["FilePath"] = serveRelative;
                return View("~/Views/Preview/PDFPreview.cshtml");
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ViewBag.ErrorMessage = ex.Message;
                return View("Error500");
            }
        }

        public class CustomFontResolver : IFontResolver
        {
            public byte[] GetFont(string faceName)
            {
                // In this example, we assume Arial font is in the Windows Fonts directory
                string fontFilePath = @"C:\Windows\Fonts\arial.ttf";

                // Read the font file and return the font data
                return System.IO.File.ReadAllBytes(fontFilePath);
            }

            public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                // For simplicity, we'll assume Arial is always regular, and styles are based on parameters
                return new FontResolverInfo("Arial", isBold, isItalic);
            }
        }

        public void AddWatermark(string inputFilePath, string outputFilePath, string watermarkText)
        {
            if (PdfSharp.Fonts.GlobalFontSettings.FontResolver is not CustomFontResolver)
            {
                PdfSharp.Fonts.GlobalFontSettings.FontResolver = new CustomFontResolver();
            }
            // Open the PDF document in modify mode
            PdfDocument document = PdfReader.Open(inputFilePath, PdfDocumentOpenMode.Modify);
            document.Version = 17;
            // Iterate through each page and add the watermark
            foreach (PdfPage page in document.Pages)
            {
                // Variation 1: Draw a watermark as a text string.

                // Get an XGraphics object for drawing beneath the existing content.
                var gfx = XGraphics.FromPdfPage(page);

                // Get the size (in points) of the text.
                XFont font = new XFont("Arial", 50);
                var size = gfx.MeasureString(watermarkText, font);

                // Define a rotation transformation at the center of the page.
                gfx.TranslateTransform(page.Width / 2, page.Height / 2);
                gfx.RotateTransform(-Math.Atan(page.Height / page.Width) * 180 / Math.PI);
                gfx.TranslateTransform(-page.Width / 2, -page.Height / 2);

                // Create a string format.
                var format = new XStringFormat();
                format.Alignment = XStringAlignment.Near;
                format.LineAlignment = XLineAlignment.Near;

                // Create a dimmed red brush.
                XBrush brush = new XSolidBrush(XColor.FromArgb(128, 255, 0, 0));

                // Draw the string.
                gfx.DrawString(watermarkText, font, brush,
                    new XPoint((page.Width - size.Width) / 2, (page.Height - size.Height) / 2),
                    format);
            }

            // Save the modified document to the output file
            document.Save(outputFilePath);
        }
        public string pengesahanModifiedfileName(string webRootPath, string filePath, DocumentMaintenance documentMaintenanceParam)
        {
            // ── 1. Ambil data document maintenance ──────────────────────────────────
            DocumentMaintenance documentMaintenance = documentMaintenanceRepo
                .Search(documentMaintenanceParam, null, db, 1, 1)
                .FirstOrDefault();

            if (documentMaintenance == null)
            {
                documentMaintenance = documentMaintenanceRepo
                    .SearchHistoryToMaintenance(documentMaintenanceParam, GetLoginUsername(), db, 1, 1)
                    .FirstOrDefault();
            }

            if (documentMaintenance == null) return null;

            // ── 2. Siapkan path & workbook ───────────────────────────────────────────
            IList<ApprovalDetail> approvalDetails =
                approvalRepo.GetApprovalDetail((int)documentMaintenance.APPROVAL_ID, db, null, null);

            string[] split = filePath.Split("/");
            string fileName = split[4];
            string extension = GetFileExtension(fileName);
            string fullPath = webRootPath + filePath;
            string outputFileName = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_" + fileName;
            string outputFullPath = webRootPath + outputFileName;

            IWorkbook workbook;
            if (extension.Equals("xlsx"))
            {
                byte[] originalBytes = System.IO.File.ReadAllBytes(fullPath);
                byte[] cleanedBytes = CleanPrintTitlesInMemory(originalBytes);
                workbook = new XSSFWorkbook(new System.IO.MemoryStream(cleanedBytes));
            }
            else
            {
                using (FileStream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                    workbook = new HSSFWorkbook(fileStream);
            }

            // ── 3. Ambil template ────────────────────────────────────────────────────
            IList<ExcelTemplateMaster> excelTemplateMasters = documentMasterRepo
                .SearchTemplate(new ExcelTemplateMaster { DOCUMENT_ID = documentMaintenance.DOCUMENT_ID }, db);

            if (excelTemplateMasters.Count == 0) return null;

            // ── 4. Iterasi setiap sheet ──────────────────────────────────────────────
            int sheetPosition = 0;
            int selectedSheetPosition = 0;

            // Definisikan field yang HANYA boleh ditulis di COVER sheet (sheetPosition=0)
            // Sheet lain sudah punya formula =COVER!xxx sehingga otomatis update
            var coverOnlyFields = new HashSet<string>
            {
                "DOCUMENT_CODE",
                "DOCUMENT_TRANSACTION_NAME",
                "DOCUMENT_REVISION_0_DATE",
                "REVISION",
                "DOCUMENT_DATE"
            };

            foreach (ISheet oISheet in workbook)
            {
                IPrintSetup printSetup = oISheet.PrintSetup;
                int orientation = printSetup.Landscape ? 1 : 0;

                IList<ExcelTemplateMaster> excelTemplateMastersBySheet =
                    excelTemplateMasters.Where(x => x.SHEET_ORIENTATION == orientation).ToList();

                bool hasSheetPositionCheck = excelTemplateMastersBySheet.Any(x => x.CHECK_SHEET_POSITION == 1);
                if (hasSheetPositionCheck)
                {
                    bool sheetPositionMatched = excelTemplateMastersBySheet.Any(x => x.SHEET_POSITION == sheetPosition);
                    if (sheetPositionMatched)
                        selectedSheetPosition = sheetPosition;

                    excelTemplateMastersBySheet = excelTemplateMastersBySheet
                        .Where(x => x.SHEET_POSITION == selectedSheetPosition)
                        .ToList();
                }

                var type2Templates = excelTemplateMastersBySheet
                 .Where(x => x.TYPE == 2 && x.FIELD_NAME.Equals("DIGITAL_SIGN"))
                 .OrderBy(x => x.TEMPLATE_ID)   // ✅ urutan insert yang menentukan, bukan posisi visual
                 .ToList();

                int type2Index = 0;

                foreach (ExcelTemplateMaster template in excelTemplateMastersBySheet)
                {
    
                    if (coverOnlyFields.Contains(template.FIELD_NAME) && sheetPosition != 0)
                    {
                        continue; 
                    }

                    if (template.TYPE == 1)
                    {
                        PropertyInfo propertyInfo = documentMaintenance.GetType().GetProperty(template.FIELD_NAME);

                        if (propertyInfo == null) continue;

                        object propValue = propertyInfo.GetValue(documentMaintenance);
                        if (propValue == null) continue;

                        IRow oIRow = SafeGetRow(oISheet, (int)template.ROW);
                        ICell oICell = SafeGetCell(oIRow, (int)template.COL);
                        SetCellValueBlackFont(workbook, oICell, propValue.ToString());

                    }

                    if (template.TYPE == 2)
                    {
                        int targetSheet = template.SHEET_POSITION ?? 0;
                        if (targetSheet != sheetPosition) continue;

                        if (approvalDetails.Count == 0) continue;
                        if (!template.FIELD_NAME.Equals("DIGITAL_SIGN")) continue;

                        // Approver dipetakan ke kotak berdasarkan WORKFLOW_SEQ, bukan urutan
                        // iterasi — supaya langkah yang dilewati saat pembuatan workflow
                        // (mis. section tanpa Section Head) menyisakan kotaknya kosong
                        IList<ApprovalDetail> targetApprovers = type2Templates.Count > 1
                            ? approvalDetails.Where(x => x.WORKFLOW_SEQ == type2Index + 1).ToList()
                            : approvalDetails.OrderBy(x => x.WORKFLOW_SEQ).ToList();

                        if (type2Templates.Count > 1 && type2Index == 0)
                        {
                            User creator = UserRepo.Instance.GetByKey(
                                new User { USERNAME = documentMaintenance.CREATED_BY }, db);
                            if (creator != null)
                            {
                                int creatorCol = (int)template.COL;
                                int creatorNameRow = (int)template.ROW + (int)template.MERGE_CELL_ROW + 1;

                                WriteNameCellSafely(oISheet, creatorNameRow, creatorCol, (int)template.MERGE_CELL_COL, creator.FULL_NAME);

                                if (!string.IsNullOrEmpty(creator.FILE_PATH))
                                {
                                    string creatorSignPath = webRootPath + creator.FILE_PATH;
                                    if (System.IO.File.Exists(creatorSignPath))
                                    {
                                        byte[] creatorBytes = System.IO.File.ReadAllBytes(creatorSignPath);
                                        int creatorPicIndex = workbook.AddPicture(creatorBytes, PictureType.PNG);
                                        XSSFClientAnchor creatorAnchor = new XSSFClientAnchor
                                        {
                                            Row1 = (int)template.ROW + 1,
                                            Col1 = creatorCol
                                        };
                                        XSSFDrawing creatorPatriarch = (XSSFDrawing)oISheet.CreateDrawingPatriarch();
                                        XSSFPicture creatorPicture = (XSSFPicture)creatorPatriarch.CreatePicture(creatorAnchor, creatorPicIndex);
                                        creatorPicture.Resize((double)template.MERGE_CELL_COL, (double)template.MERGE_CELL_ROW);
                                    }
                                }
                            }
                            type2Index++;
                            continue;
                        }

                        if (!targetApprovers.Any())
                        {
                            type2Index++;
                            continue;
                        }

                        foreach (ApprovalDetail approvalDetail in targetApprovers)
                        {
                            int colIndex = (int)template.COL;
                            if (type2Templates.Count == 1 && approvalDetail.WORKFLOW_SEQ != null)
                            {
                                colIndex -= ((int)approvalDetail.WORKFLOW_SEQ - 1) * (int)template.MERGE_CELL_COL;
                            }

                            User user = UserRepo.Instance.GetByKey(
                                new User { USERNAME = approvalDetail.APPROVER }, db);

                            if (user == null)
                            {
                                continue;
                            }

                            // ✅ HAPUS label writing — label sudah ada sebagai teks statis di Excel
                            // labelCell.SetCellValue(approvalDetail.LABEL); ← tidak perlu

                            // Tulis hanya NAMA approver
                            int nameRowIndex = (int)template.ROW + (int)template.MERGE_CELL_ROW + 1;
                            IRow nameRow = SafeGetRow(oISheet, nameRowIndex);
                            ICell nameCell = SafeGetCell(nameRow, colIndex);
                            SetCellValueBlackFont(workbook, nameCell, user.FULL_NAME);

                            if ("1".Equals(approvalDetail.STATUS))
                            {
                                string signFullPath = webRootPath + user.FILE_PATH;
                                if (System.IO.File.Exists(signFullPath))
                                {
                                    byte[] bytes = System.IO.File.ReadAllBytes(signFullPath);
                                    int pictureIndex = workbook.AddPicture(bytes, PictureType.PNG);
                                    int signRowStart = (int)template.ROW + 1;

                                    if (extension.Equals("xlsx"))
                                    {
                                        XSSFClientAnchor anchor = new XSSFClientAnchor
                                        {
                                            Row1 = signRowStart,
                                            Col1 = colIndex
                                        };
                                        XSSFDrawing patriarch = (XSSFDrawing)oISheet.CreateDrawingPatriarch();
                                        XSSFPicture picture = (XSSFPicture)patriarch.CreatePicture(anchor, pictureIndex);
                                        picture.Resize((double)template.MERGE_CELL_COL, (double)template.MERGE_CELL_ROW);
                                    }
                                    else
                                    {
                                        HSSFClientAnchor anchor = new HSSFClientAnchor
                                        {
                                            Row1 = signRowStart,
                                            Col1 = colIndex
                                        };
                                        HSSFPatriarch patriarch = oISheet.CreateDrawingPatriarch() as HSSFPatriarch;
                                        HSSFPicture picture = patriarch?.CreatePicture(anchor, pictureIndex) as HSSFPicture;
                                        picture?.Resize((double)template.MERGE_CELL_COL, (double)template.MERGE_CELL_ROW);
                                    }
                                }
                            }
                        }

                        type2Index++;
                    }


                    if (template.TYPE == 3)
                    {
                        IRow oIRow = SafeGetRow(oISheet, (int)template.ROW);
                        ICell oICell = SafeGetCell(oIRow, (int)template.COL);
                        SetCellValueBlackFont(workbook, oICell, "-"); // default

                        if (template.FIELD_NAME.Equals("DOCUMENT_REVISION_0_DATE"))
                        {
                            PropertyInfo propertyInfo = documentMaintenance.GetType()
                                .GetProperty(template.FIELD_NAME);
                            if (propertyInfo == null) continue;
                            object propValue = propertyInfo.GetValue(documentMaintenance);
                            if (propValue == null) continue;

                            string formattedDate = ParseAndFormatDate(propValue.ToString());
                            oIRow = SafeGetRow(oISheet, (int)template.ROW);
                            oICell = SafeGetCell(oIRow, (int)template.COL);
                            SetCellValueBlackFont(workbook, oICell, formattedDate); // ← aktifkan ini
                        }

                        if (template.FIELD_NAME.Equals("DOCUMENT_DATE"))
                        {
                            PropertyInfo propertyInfo = documentMaintenance.GetType().GetProperty(template.FIELD_NAME);
                            PropertyInfo revisionPropertyInfo = documentMaintenance.GetType().GetProperty("REVISION");
                            if (propertyInfo == null || revisionPropertyInfo == null) continue;

                            object propValue = propertyInfo.GetValue(documentMaintenance);
                            object revisionValue = revisionPropertyInfo.GetValue(documentMaintenance);
                            if (propValue == null || revisionValue == null || revisionValue.ToString() == "0") continue;

                            string formattedDate = ParseAndFormatDate(propValue.ToString());
                            oIRow = SafeGetRow(oISheet, (int)template.ROW);
                            oICell = SafeGetCell(oIRow, (int)template.COL);
                            SetCellValueBlackFont(workbook, oICell, formattedDate); // ← aktifkan ini
                        }
                    }
                }

                sheetPosition++;
            }

            // ── 6. Evaluate formula & simpan file ───────────────────────────────────
            if (extension.Equals("xlsx"))
                XSSFFormulaEvaluator.EvaluateAllFormulaCells(workbook);
            else
                HSSFFormulaEvaluator.EvaluateAllFormulaCells(workbook);

            // ✅ Bekukan hasil formula (mis. formula lintas-sheet "=COVER!xxx")
            // jadi nilai statis, supaya LibreOffice tidak perlu recalculate ulang
            // saat convert ke PDF (headless conversion tidak selalu recalculate).
            FlattenFormulasToValues(workbook);

            if (extension.Equals("xlsx"))
            {
                byte[] npoiOutputBytes;
                using (var msNpoiOut = new System.IO.MemoryStream())
                {
                    workbook.Write(msNpoiOut);
                    npoiOutputBytes = msNpoiOut.ToArray();
                }

                // Bersihkan Print_Titles dari hasil NPOI
                byte[] finalBytes = CleanPrintTitlesInMemory(npoiOutputBytes);

                using (FileStream fileStream = new FileStream(outputFullPath, FileMode.Create, FileAccess.Write))
                {
                    fileStream.Write(finalBytes, 0, finalBytes.Length);
                }
            }
            else
            {
                using (FileStream fileStream = new FileStream(outputFullPath, FileMode.Create, FileAccess.Write))
                {
                    workbook.Write(fileStream);
                }
            }

            return outputFileName;
        }

        // ✅ Helper: bersihkan Print_Titles dari xlsx di memory
        private byte[] CleanPrintTitlesInMemory(byte[] inputBytes)
        {
            using (var msIn = new System.IO.MemoryStream(inputBytes))
            using (var msOut = new System.IO.MemoryStream())
            {
                using (var zipIn = new System.IO.Compression.ZipArchive(
                    msIn, System.IO.Compression.ZipArchiveMode.Read))
                using (var zipOut = new System.IO.Compression.ZipArchive(
                    msOut, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (var entry in zipIn.Entries)
                    {
                        if (entry.Length == 0 && entry.FullName.EndsWith("/"))
                            continue;

                        if (string.Equals(entry.FullName, "xl/workbook.xml",
                            StringComparison.OrdinalIgnoreCase))
                        {
                            string xmlContent;
                            using (var reader = new System.IO.StreamReader(
                                entry.Open(), System.Text.Encoding.UTF8))
                            {
                                xmlContent = reader.ReadToEnd();
                            }

                            string cleaned = System.Text.RegularExpressions.Regex.Replace(
                                xmlContent,
                                @"<definedName\s[^>]*name=""_xlnm\.Print_Titles""[^>]*/?>(?:.*?</definedName>)?",
                                string.Empty,
                                System.Text.RegularExpressions.RegexOptions.Singleline
                            );

                            var outEntry = zipOut.CreateEntry(entry.FullName);
                            using (var outStream = outEntry.Open())
                            using (var writer = new System.IO.StreamWriter(
                                outStream, System.Text.Encoding.UTF8))
                            {
                                writer.Write(cleaned);
                            }
                        }
                        else
                        {
                            var outEntry = zipOut.CreateEntry(entry.FullName);
                            using (var inStream = entry.Open())
                            using (var outStream = outEntry.Open())
                            {
                                inStream.CopyTo(outStream);
                            }
                        }
                    }
                }

                return msOut.ToArray();
            }
        }

        // ── Helper Methods ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ganti semua cell berformula (termasuk formula lintas-sheet seperti
        /// "=COVER!H5") dengan nilai hasil hitungnya. Dipanggil setelah
        /// EvaluateAllFormulaCells supaya LibreOffice tidak perlu recalculate
        /// ulang saat convert ke PDF.
        /// </summary>
        private void FlattenFormulasToValues(IWorkbook workbook)
        {
            for (int s = 0; s < workbook.NumberOfSheets; s++)
            {
                ISheet sheet = workbook.GetSheetAt(s);
                foreach (IRow row in sheet)
                {
                    foreach (ICell cell in row)
                    {
                        if (cell.CellType != CellType.Formula) continue;

                        switch (cell.CachedFormulaResultType)
                        {
                            case CellType.Numeric:
                                double numVal = cell.NumericCellValue;
                                cell.SetCellType(CellType.Blank);
                                cell.SetCellValue(numVal);
                                break;
                            case CellType.String:
                                string strVal = cell.StringCellValue;
                                cell.SetCellType(CellType.Blank);
                                cell.SetCellValue(strVal);
                                break;
                            case CellType.Boolean:
                                bool boolVal = cell.BooleanCellValue;
                                cell.SetCellType(CellType.Blank);
                                cell.SetCellValue(boolVal);
                                break;
                            default:
                                cell.SetCellType(CellType.Blank);
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Validasi file Excel yang di-upload terhadap konfigurasi TB_M_EXCEL_TEMPLATE.
        /// Mengembalikan pesan error yang jelas jika konfigurasi tidak lengkap
        /// (supaya ditolak di awal, bukan menghasilkan PDF pengesahan kosong),
        /// atau null jika valid / tidak relevan (bukan file Excel).
        /// </summary>
        private string ValidateTemplateConfiguration(IFormFile file, int? documentId)
        {
            string extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".xls" && extension != ".xlsx") return null;
            if (documentId == null) return null;

            ISheet coverSheet;
            try
            {
                using (var stream = file.OpenReadStream())
                {
                    IWorkbook workbook = extension.Equals(".xlsx")
                        ? new XSSFWorkbook(stream)
                        : (IWorkbook)new HSSFWorkbook(stream);
                    coverSheet = workbook.GetSheetAt(0);
                }
            }
            catch (Exception)
            {
                return "File Excel tidak dapat dibaca. Pastikan file tidak rusak dan formatnya sesuai (" + extension + ").";
            }

            int orientation = coverSheet.PrintSetup.Landscape ? 1 : 0;
            string orientationName = orientation == 1 ? "Landscape" : "Portrait";

            IList<ExcelTemplateMaster> templates = documentMasterRepo
                .SearchTemplate(new ExcelTemplateMaster { DOCUMENT_ID = documentId }, db);

            if (templates.Count == 0)
            {
                return "Konfigurasi template Excel untuk jenis dokumen ini belum terdaftar, "
                    + "sehingga data pengesahan tidak akan tercetak di PDF. "
                    + "Hubungi administrator untuk melengkapi konfigurasi template terlebih dahulu.";
            }

            var matchingOrientation = templates.Where(x => x.SHEET_ORIENTATION == orientation).ToList();
            if (matchingOrientation.Count == 0)
            {
                var availableOrientations = string.Join(", ", templates
                    .Select(x => x.SHEET_ORIENTATION == 1 ? "Landscape" : "Portrait")
                    .Distinct());

                return "Sheet \"" + coverSheet.SheetName + "\" berorientasi " + orientationName
                    + ", tetapi konfigurasi template jenis dokumen ini hanya tersedia untuk orientasi "
                    + availableOrientations + ". "
                    + "Gunakan template dengan orientasi yang sesuai, atau minta administrator melengkapi konfigurasinya.";
            }

            bool anyDigitalSign = templates.Any(x => "DIGITAL_SIGN".Equals(x.FIELD_NAME));
            bool matchingDigitalSign = matchingOrientation.Any(x => "DIGITAL_SIGN".Equals(x.FIELD_NAME));
            if (anyDigitalSign && !matchingDigitalSign)
            {
                return "Konfigurasi kolom tanda tangan (DIGITAL_SIGN) untuk orientasi " + orientationName
                    + " belum terdaftar pada jenis dokumen ini, sehingga kolom PIC akan kosong di PDF. "
                    + "Minta administrator melengkapi konfigurasinya terlebih dahulu.";
            }

            return null;
        }

        /// <summary>
        /// Ambil row yang sudah ada, atau buat baru jika null.
        /// NPOI mengembalikan null untuk row yang sepenuhnya kosong di Excel.
        /// </summary>
        private IRow SafeGetRow(ISheet sheet, int rowIndex)
            => sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);

        /// <summary>
        /// Ambil cell yang sudah ada, atau buat baru jika null.
        /// NPOI mengembalikan null untuk cell yang belum pernah dibuat di Excel.
        /// </summary>
        private ICell SafeGetCell(IRow row, int colIndex)
            => row.GetCell(colIndex) ?? row.CreateCell(colIndex);

        /// <summary>
        /// Tulis nilai ke cell dan paksa warna font jadi hitam.
        /// Style di-clone dulu supaya cell lain yang berbagi style/font
        /// yang sama (mis. teks merah di body dokumen) tidak ikut berubah.
        /// </summary>
        private void SetCellValueBlackFont(IWorkbook workbook, ICell cell, string value)
        {
            cell.SetCellValue(value);

            IFont currentFont = workbook.GetFontAt(cell.CellStyle.FontIndex);

            // 32767 (0x7FFF) = warna "Automatic" pada HSSF, tampil hitam
            if (currentFont.Color == IndexedColors.Black.Index || currentFont.Color == 32767)
                return;

            IFont blackFont = workbook.CreateFont();
            blackFont.FontName = currentFont.FontName;
            blackFont.FontHeight = currentFont.FontHeight;
            blackFont.IsBold = currentFont.IsBold;
            blackFont.IsItalic = currentFont.IsItalic;
            blackFont.IsStrikeout = currentFont.IsStrikeout;
            blackFont.Underline = currentFont.Underline;
            blackFont.TypeOffset = currentFont.TypeOffset;
            blackFont.Charset = currentFont.Charset;
            blackFont.Color = IndexedColors.Black.Index;

            ICellStyle blackStyle = workbook.CreateCellStyle();
            blackStyle.CloneStyleFrom(cell.CellStyle);
            blackStyle.SetFont(blackFont);
            cell.CellStyle = blackStyle;
        }

        /// <summary>
        /// Parse string tanggal dari database dan format ke "dd-MMM-yy" dalam Bahasa Indonesia.
        /// </summary>
        private string ParseAndFormatDate(string rawDate)
        {
            DateTime date = DateTime.ParseExact(
                rawDate,
                "M/d/yyyy h:mm:ss tt",
                CultureInfo.InvariantCulture);

            return date.ToString("dd-MMM-yy", new CultureInfo("id-ID"));
        }

        //public string pengesahanModifiedfileName(string webRootPath, string filePath, DocumentMaintenance documentMaintenanceParam)
        //{
        //    DocumentMaintenance documentMaintenance = documentMaintenanceRepo.Search(documentMaintenanceParam, null, db, 1, 1).FirstOrDefault();
        //    if (documentMaintenance == null)
        //    {
        //        documentMaintenance = documentMaintenanceRepo.SearchHistoryToMaintenance(documentMaintenanceParam, GetLoginUsername(), db, 1, 1).FirstOrDefault();
        //    }

        //    if (documentMaintenance != null)
        //    {
        //        IList<ApprovalDetail> approvalDetails = approvalRepo.GetApprovalDetail((int)documentMaintenance.APPROVAL_ID, db, null, null);

        //        string[] split = filePath.Split("/");
        //        string fileName = split[4];
        //        string extension = GetFileExtension(fileName);
        //        string fullPath = webRootPath + filePath;
        //        string outputFileName = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_" + fileName;
        //        string outputFullPath = webRootPath + outputFileName;

        //        IWorkbook workbook;
        //        using (FileStream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
        //        {
        //            if (extension.Equals("xlsx"))
        //            {
        //                workbook = new XSSFWorkbook(fileStream);
        //            }
        //            else
        //            {
        //                workbook = new HSSFWorkbook(fileStream);
        //            }
        //        }

        //        IList<ExcelTemplateMaster> excelTemplateMasters = documentMasterRepo.SearchTemplate(new ExcelTemplateMaster { DOCUMENT_ID = documentMaintenance.DOCUMENT_ID }, db);

        //        if (excelTemplateMasters.Count() > 0)
        //        {
        //            int sheetPosition = 0;
        //            int selectedSheetPosition = 0;
        //            foreach (ISheet oISheet in workbook)
        //            {
        //                // Get the PrintSetup object
        //                IPrintSetup printSetup = oISheet.PrintSetup;

        //                // Check the orientation
        //                int orientation = printSetup.Landscape ? 1 : 0; //1 = landscape, 0 = potrait

        //                IList<ExcelTemplateMaster> excelTemplateMastersBySheet = excelTemplateMasters.Where(x => x.SHEET_ORIENTATION == orientation).ToList();

        //                int CHECK_SHEET_POSITION = excelTemplateMastersBySheet.Where(x => x.CHECK_SHEET_POSITION == 1).Count();
        //                if (CHECK_SHEET_POSITION > 0)
        //                {
        //                    int SHEET_POSITION = excelTemplateMastersBySheet.Where(x => x.SHEET_POSITION == sheetPosition).Count();
        //                    if (SHEET_POSITION > 0)
        //                    {
        //                        selectedSheetPosition = sheetPosition;
        //                    }

        //                    excelTemplateMastersBySheet = excelTemplateMastersBySheet.Where(x => x.SHEET_POSITION == selectedSheetPosition).ToList();
        //                }

        //                foreach (ExcelTemplateMaster template in excelTemplateMastersBySheet)
        //                {
        //                    if (oISheet != null)
        //                    {
        //                        IRow oIRow;
        //                        NPOI.SS.UserModel.ICell oICell;

        //                        if (template.TYPE == 1)
        //                        {
        //                            PropertyInfo propertyInfo = documentMaintenance.GetType().GetProperty(template.FIELD_NAME);
        //                            if (propertyInfo.GetValue(documentMaintenance) != null)
        //                            {
        //                                oIRow = oISheet.GetRow((int)template.ROW);
        //                                oICell = oIRow.GetCell((int)template.COL);
        //                                oICell.SetCellValue(propertyInfo.GetValue(documentMaintenance).ToString());
        //                            }
        //                        }

        //                        if (template.TYPE == 2)
        //                        {
        //                            if (approvalDetails.Count() > 0)
        //                            {
        //                                if (template.FIELD_NAME.Equals("DIGITAL_SIGN"))
        //                                {
        //                                    int i = (int)template.COL;
        //                                    foreach (ApprovalDetail approvalDetail in approvalDetails)
        //                                    {
        //                                        User user = UserRepo.Instance.GetByKey(new User { USERNAME = approvalDetail.APPROVER }, db);
        //                                        if (user != null)
        //                                        {
        //                                            oIRow = oISheet.GetRow((int)template.ROW);
        //                                            oICell = oIRow.GetCell(i);
        //                                            oICell.SetCellValue(approvalDetail.LABEL);

        //                                            oIRow = oISheet.GetRow((int)((int)template.ROW + template.MERGE_CELL_ROW + 1));
        //                                            oICell = oIRow.GetCell(i);
        //                                            oICell.SetCellValue(user.FULL_NAME);

        //                                            if (approvalDetail.STATUS != null && approvalDetail.STATUS.Equals("1"))
        //                                            {
        //                                                string signFullPath = webRootPath + user.FILE_PATH;

        //                                                if (System.IO.File.Exists(signFullPath))
        //                                                {
        //                                                    byte[] bytes = System.IO.File.ReadAllBytes(signFullPath);
        //                                                    int pictureIndex = workbook.AddPicture(bytes, NPOI.SS.UserModel.PictureType.PNG);

        //                                                    if (extension.Equals("xlsx"))
        //                                                    {
        //                                                        XSSFClientAnchor anchor = new XSSFClientAnchor();
        //                                                        anchor.Row1 = (int)template.ROW + 1;
        //                                                        anchor.Col1 = i;

        //                                                        XSSFDrawing patriarch = (XSSFDrawing)oISheet.CreateDrawingPatriarch();
        //                                                        XSSFPicture picture = (XSSFPicture)patriarch.CreatePicture(anchor, pictureIndex);

        //                                                        // Resize the picture to fit within the column width
        //                                                        picture.Resize((double)template.MERGE_CELL_COL, (double)template.MERGE_CELL_ROW);
        //                                                    }
        //                                                    else
        //                                                    {
        //                                                        HSSFClientAnchor anchor = new HSSFClientAnchor();
        //                                                        anchor.Row1 = (int)template.ROW + 1;
        //                                                        anchor.Col1 = i;

        //                                                        HSSFPatriarch patriarch = oISheet.CreateDrawingPatriarch() as HSSFPatriarch;
        //                                                        HSSFPicture picture = patriarch.CreatePicture(anchor, pictureIndex) as HSSFPicture;

        //                                                        // Resize the picture to fit within the column width
        //                                                        picture.Resize((double)template.MERGE_CELL_COL, (double)template.MERGE_CELL_ROW);
        //                                                    }
        //                                                }
        //                                            }
        //                                        }

        //                                        i = (int)(i - template.MERGE_CELL_COL);
        //                                    }
        //                                }
        //                            }
        //                        }

        //                        if (template.TYPE == 3)
        //                        {
        //                            oIRow = oISheet.GetRow((int)template.ROW);
        //                            oICell = oIRow.GetCell((int)template.COL);
        //                            oICell.SetCellValue("-");

        //                            if (template.FIELD_NAME.Equals("DOCUMENT_REVISION_0_DATE"))
        //                            {
        //                                PropertyInfo propertyInfo = documentMaintenance.GetType().GetProperty(template.FIELD_NAME);
        //                                if (propertyInfo.GetValue(documentMaintenance) != null)
        //                                {
        //                                    DateTime date = DateTime.ParseExact(propertyInfo.GetValue(documentMaintenance).ToString(), "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
        //                                    CultureInfo culture = new CultureInfo("id-ID"); // Indonesian culture
        //                                    string formattedDate = date.ToString("dd-MMM-yy", culture);

        //                                    oIRow = oISheet.GetRow((int)template.ROW);
        //                                    oICell = oIRow.GetCell((int)template.COL);
        //                                    oICell.SetCellValue(formattedDate);
        //                                }
        //                            }

        //                            if (template.FIELD_NAME.Equals("DOCUMENT_DATE"))
        //                            {
        //                                PropertyInfo propertyInfo = documentMaintenance.GetType().GetProperty(template.FIELD_NAME);
        //                                PropertyInfo revisionPropertyInfo = documentMaintenance.GetType().GetProperty("REVISION");
        //                                if (propertyInfo.GetValue(documentMaintenance) != null)
        //                                {
        //                                    if (revisionPropertyInfo.GetValue(documentMaintenance) != null)
        //                                    {
        //                                        if (revisionPropertyInfo.GetValue(documentMaintenance).ToString() != "0")
        //                                        {
        //                                            DateTime date = DateTime.ParseExact(propertyInfo.GetValue(documentMaintenance).ToString(), "M/d/yyyy h:mm:ss tt", CultureInfo.InvariantCulture);
        //                                            CultureInfo culture = new CultureInfo("id-ID"); // Indonesian culture
        //                                            string formattedDate = date.ToString("dd-MMM-yy", culture);

        //                                            oIRow = oISheet.GetRow((int)template.ROW)?? oISheet.CreateRow((int)template.ROW);
        //                                            //oIRow = oISheet.GetRow((int)template.ROW);
        //                                            //oICell = oIRow.GetCell((int)template.COL);
        //                                            oICell = oIRow.GetCell((int)template.COL)?? oIRow.CreateCell((int)template.COL);
        //                                            oICell.SetCellValue(formattedDate);
        //                                        }
        //                                    }
        //                                }
        //                            }
        //                        }
        //                    }
        //                }

        //                sheetPosition++;
        //            }

        //            if (extension.Equals("xlsx"))
        //            {
        //                XSSFFormulaEvaluator.EvaluateAllFormulaCells(workbook);
        //            }
        //            else
        //            {
        //                HSSFFormulaEvaluator.EvaluateAllFormulaCells(workbook);
        //            }

        //            using (FileStream fileStream = new FileStream(outputFullPath, FileMode.Create, FileAccess.Write))
        //            {
        //                workbook.Write(fileStream);
        //            }

        //            return outputFileName;
        //        }
        //    }

        //    return null;
        //}

        static string GetFileExtension(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return null;

            // Get the file extension using Path.GetExtension
            string extension = Path.GetExtension(filename);

            // Check if the extension contains a dot and remove it if present
            if (!string.IsNullOrEmpty(extension) && extension[0] == '.')
                extension = extension.Substring(1);

            return extension;
        }

        //public JsonResult printAttachment(DocumentMaintenance documentMaintenance, string type)
        //{
        //    DBResult result;

        //    try
        //    {
        //        DocumentLog documentLog = new DocumentLog
        //        {
        //            DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID,
        //            LOG_TYPE = "2"
        //        };

        //        result = insertDocumentLog(documentLog, type);
        //        if (!result.status)
        //        {
        //            Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        //            ViewBag.ErrorMessage = result.message;
        //            return Json(new { status = false, message = ViewBag.ErrorMessage });
        //        }

        //        string webRootPath = Environment.WebRootPath;
        //        string fullPath = webRootPath + documentMaintenance.FILE_PATH;

        //        if (!System.IO.File.Exists(fullPath))
        //        {
        //            ViewBag.ErrorMessage = "File Not Found";
        //            return Json(new { status = false, message = ViewBag.ErrorMessage });
        //        }

        //        string[] split = documentMaintenance.FILE_PATH.Split("/");
        //        string fileName = split[4];
        //        string extension = GetFileExtension(fileName);
        //        string pengesahanModifiedfileNames, finalPath;

        //        if (extension.Equals("pdf"))
        //        {
        //            string outputFileName = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_" + fileName;
        //            string outputFullPath = webRootPath + outputFileName;

        //            if (System.IO.File.Exists(outputFullPath))
        //            {
        //                System.IO.File.Delete(outputFullPath);
        //            }

        //            // Copy the source file to the destination folder
        //            System.IO.File.Copy(fullPath, outputFullPath);


        //            pengesahanModifiedfileNames = outputFileName;
        //        } 
        //        else
        //        {
        //            // cek cache dulu
        //            string cachedPdfRelative = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
        //                + fileName.Replace("." + extension, ".pdf");
        //            string cachedPdfFullPath = webRootPath + cachedPdfRelative;

        //            if (System.IO.File.Exists(cachedPdfFullPath))
        //            {
        //                // Cache hit 
        //                if (type == "3")
        //                    AddWatermark(cachedPdfFullPath, cachedPdfFullPath, "CONTROLLED COPY");

        //                return Json(new { status = true, data = cachedPdfRelative });
        //            }
        //            // end

        //            pengesahanModifiedfileNames = pengesahanModifiedfileName(webRootPath, documentMaintenance.FILE_PATH, documentMaintenance);
        //            string pengesahanModifiedFullPath = webRootPath + pengesahanModifiedfileNames;
        //            if (pengesahanModifiedfileNames == null)
        //            {
        //                ViewBag.ErrorMessage = "Error when modifying pengesahan header";
        //                return Json(new { status = false, message = ViewBag.ErrorMessage });
        //            }

        //            split = pengesahanModifiedfileNames.Split("/");
        //            fileName = split[4];
        //            extension = GetFileExtension(fileName);

        //            result = ConvertToPdf(pengesahanModifiedFullPath, pengesahanModifiedFullPath);
        //            if (!result.status)
        //            {
        //                ViewBag.ErrorMessage = result.message;
        //                return Json(new { status = false, message = ViewBag.ErrorMessage });
        //            }

        //            if (System.IO.File.Exists(pengesahanModifiedFullPath))
        //            {
        //                System.IO.File.Delete(pengesahanModifiedFullPath);
        //            }

        //            pengesahanModifiedfileNames = pengesahanModifiedfileNames.Replace(extension, "pdf");
        //        }

        //        finalPath = webRootPath + pengesahanModifiedfileNames;

        //        if (type == "3")
        //        {
        //            AddWatermark(finalPath, finalPath, "CONTROLLED COPY");
        //        }

        //        return Json(new { status = true, data = pengesahanModifiedfileNames });
        //    }
        //    catch (Exception ex)
        //    {
        //        ViewBag.ErrorMessage = ex.Message;
        //        return Json(new { status = false, message = ViewBag.ErrorMessage });
        //    }
        //}

        //update approval - Hafidz Ezio
        public JsonResult GetApprovalHeader(int approvalId)
        {
            try
            {
                var result = approvalRepo.GetApprovalHeader(approvalId, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetApprovalDetail(int approvalId)
        {
            try
            {
                var result = approvalRepo.GetApprovalDetail(approvalId, db, null, null);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetApprovalHistoryDetail(ApprovalDetail data)
        {
            try
            {
                var result = approvalRepo.GetApprovalHistoryDetail(data, db, null, null);
                result = result.Where(x => x.REMARK != null).ToList();
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> ApproveRejectAsync(int approvalId, int workflowSeq, string approver, string remark, string mode, DocumentMaintenance documentMaintenance)
        {
            DBResult result;
            string webRootPath = Environment.WebRootPath;

            string module = "Document Maintenance";
            string location = "DocumentMaintenance/ApproveReject";
            string function = "Approve Approval";

            if (function != "approve")
            {
                function = "Reject Approval";
            }

            LogHeader logH = new LogHeader();
            logH.MODULE = module;
            logH.FUNCTION = function;

            long processid = logRepo.StartLog(logH, location, GetLoginUsername(), db);

            try
            {
                db.Database.BeginTransaction();

                if (mode == "approve")
                {
                    IList<ApprovalDetail> approvalDetails = approvalRepo.GetApprovalDetail(approvalId, db, null, null);

                    if (approvalDetails.Count > 0)
                    {
                        var max_seq = approvalDetails.Where(x => x.APPROVAL_ID == approvalId).Max(x => x.WORKFLOW_SEQ);

                        if (max_seq == workflowSeq)
                        {
                            documentMaintenance.STATUS = "1";
                            result = documentMaintenanceRepo.UpdateStatus(documentMaintenance, GetLoginUsername(), db);
                            if (!result.status)
                            {
                                db.Database.RollbackTransaction();
                                logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);

                                return Json(new { status = false, message = result.message });
                            }

                            result = approvalRepo.Approve(approvalId, workflowSeq, approver, remark, GetLoginUsername(), db);

                            if (!result.status)
                            {
                                db.Database.RollbackTransaction();
                                logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);

                                return Json(new { status = false, message = result.message });
                            }
                            else
                            {
                                // Obsolete-control (Jul 2026): revisi ini baru saja selesai disetujui
                                // (langkah approval terakhir) - arsipkan revisi sebelumnya yang masih
                                // berstatus Approved sebagai OBSOLETE. Kalau tidak ada revisi
                                // sebelumnya (dokumen baru), SP tidak melakukan apa-apa.
                                // loginUser sengaja null di kedua Search di bawah - approver yang
                                // memproses approval ini belum tentu satu divisi/department dengan
                                // dokumennya, dan Search men-scope hasil berdasarkan TB_M_USER_POS
                                // loginUser kalau diisi (bisa membuat lookup ini salah kembalikan
                                // null padahal dokumennya ada).
                                DocumentMaintenance currentDocument = documentMaintenanceRepo
                                    .Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID }, null, db, 1, 1)
                                    .FirstOrDefault();

                                DocumentMaintenance previousRevision = currentDocument == null ? null : documentMaintenanceRepo
                                    .Search(new DocumentMaintenance { DOCUMENT_CODE = currentDocument.DOCUMENT_CODE, STATUS = "1" }, null, db, 1, 10)
                                    .FirstOrDefault(x => x.DOCUMENT_TRANSACTION_ID != documentMaintenance.DOCUMENT_TRANSACTION_ID);

                                documentMaintenanceRepo.SupersedePreviousRevision(documentMaintenance, GetLoginUsername(), db);

                                if (previousRevision != null)
                                {
                                    //Cache PDF revisi lama jangan sampai terus disajikan sebagai
                                    //"CONTROLLED COPY" - hapus supaya request berikutnya regenerate
                                    //dengan watermark OBSOLETE (lihat ViewAttachment).
                                    DeleteDocumentCache(previousRevision, webRootPath);
                                }

                                DeleteDocumentCache(documentMaintenance, webRootPath);
                                SendApproveRejectEmail((int)documentMaintenance.DOCUMENT_TRANSACTION_ID, mode, remark);
                                logRepo.WriteLog(processid, "1", "INF", result.message, location, GetLoginUsername(), db);
                            }
                        }
                        else
                        {
                            result = approvalRepo.Approve(approvalId, workflowSeq, approver, remark, GetLoginUsername(), db);

                            if (!result.status)
                            {
                                db.Database.RollbackTransaction();
                                logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);

                                return Json(new { status = false, message = result.message });
                            }
                            else
                            {
                                DeleteDocumentCache(documentMaintenance, webRootPath);
                                SendApprovalEmailAsync((int)documentMaintenance.DOCUMENT_TRANSACTION_ID);
                                logRepo.WriteLog(processid, "1", "INF", result.message, location, GetLoginUsername(), db);
                            }
                        }
                    }
                    else
                    {
                        db.Database.RollbackTransaction();
                        logRepo.WriteLog(processid, "3", "ERR", "Approval data not found", location, GetLoginUsername(), db);

                        return Json(new { status = false, message = "Approval data not found" });
                    }

                    db.Database.CommitTransaction();
                    logRepo.WriteLog(processid, "2", "INF", result.message, location, GetLoginUsername(), db);

                    return Json(result);
                }
                else
                {
                    documentMaintenance.STATUS = "2";
                    result = documentMaintenanceRepo.UpdateStatus(documentMaintenance, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        db.Database.RollbackTransaction();
                        logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);

                        return Json(new { status = false, message = result.message });
                    }
                    else
                    {
                        logRepo.WriteLog(processid, "1", "INF", result.message, location, GetLoginUsername(), db);
                    }

                    result = approvalRepo.Reject(approvalId, workflowSeq, approver, remark, GetLoginUsername(), db);

                    if (!result.status)
                    {
                        db.Database.RollbackTransaction();
                        logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);

                        return Json(new { status = false, message = result.message });
                    }
                    DeleteDocumentCache(documentMaintenance, webRootPath);
                    SendApproveRejectEmail((int)documentMaintenance.DOCUMENT_TRANSACTION_ID, mode, remark);

                    db.Database.CommitTransaction();
                    logRepo.WriteLog(processid, "2", "INF", result.message, location, GetLoginUsername(), db);

                    return Json(result);
                }
            }
            catch (Exception ex)
            {
                db.Database.RollbackTransaction();
                logRepo.WriteLog(processid, "4", "ERR", ex.Message, location, GetLoginUsername(), db);

                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult DownloadPengesahan(DocumentMaintenance documentMaintenanceParam)
        {
            string webRootPath = Environment.WebRootPath;
            string fileName = @"Document/DocumentMaintenance/Pengesahan-Template.xlsx";
            string existingPdfPath = Path.Combine(webRootPath, fileName);
            string outputExcelFile = Path.Combine(webRootPath, @"Document/DocumentMaintenance/Pengesahan-Template-Output.xlsx");
            string outputPdfFile = Path.Combine(webRootPath, @"Document/DocumentMaintenance/Pengesahan-Template-Output.pdf");
            var memoryStream = new MemoryStream();

            IList<ApprovalDetail> approvalDetails = approvalRepo.GetApprovalDetail((int)documentMaintenanceParam.APPROVAL_ID, db, null, null);
            DocumentMaintenance documentMaintenance = documentMaintenanceRepo.Search(documentMaintenanceParam, null, db, 1, 1).FirstOrDefault();

            IWorkbook workbook;
            using (FileStream fileStream = new FileStream(existingPdfPath, FileMode.Open, FileAccess.Read))
            {
                workbook = new XSSFWorkbook(fileStream);
            }

            ICellStyle style = workbook.CreateCellStyle();
            //style.WrapText = true;
            ISheet sheet = workbook.GetSheetAt(0);

            float calculatedRowHeight = sheet.DefaultRowHeightInPoints * sheet.DefaultRowHeight / 256;
            float paddedRowHeight = calculatedRowHeight + 10.0f;

            IRow row = sheet.GetRow(2) ?? sheet.CreateRow(2);
            NPOI.SS.UserModel.ICell cell = row.GetCell(2) ?? row.CreateCell(2);
            cell.SetCellValue(documentMaintenance.DOCUMENT_TRANSACTION_NAME);

            row = sheet.GetRow(0) ?? sheet.CreateRow(0);
            cell = row.GetCell(2) ?? row.CreateCell(2);
            cell.SetCellValue(documentMaintenance.DOCUMENT_NAME);

            row = sheet.GetRow(6) ?? sheet.CreateRow(6);
            cell = row.GetCell(3) ?? row.CreateCell(3);
            cell.SetCellValue(documentMaintenance.DEPARTMENT_NAME);
            row.HeightInPoints = paddedRowHeight;

            row = sheet.GetRow(7) ?? sheet.CreateRow(7);
            cell = row.GetCell(3) ?? row.CreateCell(3);
            cell.SetCellValue(documentMaintenance.SECTION_NAME);
            row.HeightInPoints = paddedRowHeight;

            row = sheet.GetRow(7) ?? sheet.CreateRow(7);
            cell = row.GetCell(3) ?? row.CreateCell(3);
            cell.SetCellValue(documentMaintenance.SECTION_NAME);

            row = sheet.GetRow(5) ?? sheet.CreateRow(5);
            cell = row.GetCell(6) ?? row.CreateCell(6);
            cell.SetCellValue(documentMaintenance.DOCUMENT_CODE);

            int i = 4;
            foreach (ApprovalDetail approvalDetail in approvalDetails)
            {
                row = sheet.GetRow(0) ?? sheet.CreateRow(0);
                cell = row.GetCell(i) ?? row.CreateCell(i);
                cell.SetCellValue(approvalDetail.LABEL);

                row = sheet.GetRow(4) ?? sheet.CreateRow(4);
                cell = row.GetCell(i) ?? row.CreateCell(i);
                cell.SetCellValue(approvalDetail.APPROVER);

                string anotherFullPath = webRootPath + approvalDetail.FILE_PATH;

                if (System.IO.File.Exists(anotherFullPath))
                {
                    byte[] anotherBytes = System.IO.File.ReadAllBytes(anotherFullPath);
                    int anotherPictureIndex = workbook.AddPicture(anotherBytes, NPOI.SS.UserModel.PictureType.PNG);

                    XSSFClientAnchor anotherAnchor = new XSSFClientAnchor();
                    anotherAnchor.Row1 = 1;
                    anotherAnchor.Col1 = i;

                    XSSFDrawing anotherDrawing = (XSSFDrawing)sheet.CreateDrawingPatriarch();
                    XSSFPicture anotherPicture = (XSSFPicture)anotherDrawing.CreatePicture(anotherAnchor, anotherPictureIndex);

                    // Resize the picture to fit within the column width
                    anotherPicture.Resize(1.3, 3.0);
                }

                i = i + 2;
            }

            using (FileStream fileStream = new FileStream(outputExcelFile, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fileStream);
            }

            DBResult result = ConvertToPdf(outputExcelFile, outputPdfFile);
            if (!result.status)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ViewBag.ErrorMessage = result.message;
                return View("Error500");
            }

            return File(System.IO.File.ReadAllBytes(outputPdfFile), "application/pdf", "Pengesahan-Template.pdf");
        }

        static System.Security.SecureString SecureStringPassword(string password)
        {
            System.Security.SecureString securePassword = new System.Security.SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            return securePassword;
        }




        //Start Original Code
        public DBResult ConvertToPdf(string inputPath, string outputPath)
        {
            try
            {
                // Command to execute LibreOffice in headless mode and convert Word to PDF
                string command = $"--headless -env:UserInstallation=file:///C:/wwwroot/DMSWorkDir --convert-to pdf --outdir \"{System.IO.Path.GetDirectoryName(outputPath)}\" \"{inputPath}\"";

                // Execute the command
                using (var process = new Process())
                {
                    //process.StartInfo.Verb = "runas";
                    process.StartInfo.FileName = "C:/Program Files/LibreOffice/program/soffice.exe";
                    process.StartInfo.Arguments = command;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    string error = "ERROR";
                    string output = " OUTPUT";

                    process.Start();

                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();

                    // Wait for the process to exit
                    process.WaitForExit();
                }

                return new DBResult(true, "File Converted");
            }
            catch (Exception ex)
            {
                return new DBResult(false, ex.Message);
            }
        }
        //End Original Code


        //custom by hendra
        [HttpGet]
        public JsonResult GetPdfPageCount(string filePath)
        {
            try
            {
                string webRootPath = Environment.WebRootPath;
                string[] split = filePath.Split("/");
                string fileName = split[4];
                string extension = GetFileExtension(fileName);

                // Cek cache PDF dulu
                string cachedPdf = webRootPath
                    + "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                    + fileName.Replace("." + extension, ".pdf");

                string pdfPath = System.IO.File.Exists(cachedPdf)
                    ? cachedPdf
                    : webRootPath + filePath;

                if (!System.IO.File.Exists(pdfPath))
                    return Json(new { status = false, pageCount = 0 });

                using (var doc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.ReadOnly))
                {
                    return Json(new { status = true, pageCount = doc.PageCount });
                }
            }
            catch (Exception ex)
            {
                return Json(new { status = false, pageCount = 0, message = ex.Message });
            }
        }

        public JsonResult printAttachment(DocumentMaintenance documentMaintenance,string type, int printCopy = 1)
        {
            DBResult result;
            try
            {
                result = insertDocumentLog(new DocumentLog
                {
                    DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID,
                    LOG_TYPE = "2"
                }, type);

                if (!result.status)
                    return Json(new { status = false, message = result.message });

                string webRootPath = Environment.WebRootPath;
                string fullPath = webRootPath + documentMaintenance.FILE_PATH;

                if (!System.IO.File.Exists(fullPath))
                    return Json(new { status = false, message = "File Not Found" });

                string[] split = documentMaintenance.FILE_PATH.Split("/");
                string fileName = split[4];
                string extension = GetFileExtension(fileName);
                string pengesahanModifiedfileNames;

                if (extension.Equals("pdf"))
                {
                    string outputFileName = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_" + fileName;
                    string outputFullPath = webRootPath + outputFileName;
                    if (System.IO.File.Exists(outputFullPath))
                        System.IO.File.Delete(outputFullPath);
                    System.IO.File.Copy(fullPath, outputFullPath);
                    pengesahanModifiedfileNames = outputFileName;
                }
                else
                {
                    // Cek cache
                    string cachedPdfRelative = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                        + fileName.Replace("." + extension, ".pdf");
                    string cachedPdfFullPath = webRootPath + cachedPdfRelative;

                    if (System.IO.File.Exists(cachedPdfFullPath))
                    {
                        if (type == "3")
                            AddWatermark(cachedPdfFullPath, cachedPdfFullPath, "CONTROLLED COPY");

                        pengesahanModifiedfileNames = cachedPdfRelative;
                    }
                    else
                    {
                        pengesahanModifiedfileNames = pengesahanModifiedfileName(webRootPath, documentMaintenance.FILE_PATH, documentMaintenance);

                        if (pengesahanModifiedfileNames == null)
                            return Json(new { status = false, message = "Error modifying header" });

                        string modifiedFullPath = webRootPath + pengesahanModifiedfileNames;
                        split = pengesahanModifiedfileNames.Split("/");
                        fileName = split[4];
                        extension = GetFileExtension(fileName);

                        result = ConvertToPdf(modifiedFullPath, modifiedFullPath);
                        if (!result.status)
                            return Json(new { status = false, message = result.message });

                        if (System.IO.File.Exists(modifiedFullPath))
                            System.IO.File.Delete(modifiedFullPath);

                        pengesahanModifiedfileNames = pengesahanModifiedfileNames
                            .Replace("." + extension, ".pdf");

                        string pdfFullPath = webRootPath + pengesahanModifiedfileNames;
                        if (type == "3")
                            AddWatermark(pdfFullPath, pdfFullPath, "CONTROLLED COPY");
                    }
                }

                // ── Duplikasi halaman sesuai copy + watermark "COPY X of N" ──
                string sourcePdfPath = webRootPath + pengesahanModifiedfileNames;
                string finalRelativePath = pengesahanModifiedfileNames;

                if (printCopy >= 1 && System.IO.File.Exists(sourcePdfPath))
                {
                    // Kalau hanya 1 copy, tetap tambah watermark "COPY 1 of 1"
                    string multiCopyPath = sourcePdfPath.Replace(".pdf", $"_x{printCopy}.pdf");
                    string multiCopyRelative = pengesahanModifiedfileNames
                        .Replace(".pdf", $"_x{printCopy}.pdf");

                    // Setup font resolver kalau belum
                    if (PdfSharp.Fonts.GlobalFontSettings.FontResolver is not CustomFontResolver)
                        PdfSharp.Fonts.GlobalFontSettings.FontResolver = new CustomFontResolver();

                    using (PdfDocument outputDoc = new PdfDocument())
                    using (PdfDocument inputDoc = PdfReader.Open(
                        sourcePdfPath, PdfDocumentOpenMode.Import))
                    {
                        for (int c = 0; c < printCopy; c++)
                        {
                            for (int p = 0; p < inputDoc.PageCount; p++)
                            {
                                // Import halaman
                                PdfPage page = outputDoc.AddPage(inputDoc.Pages[p]);

                                // Gambar watermark "COPY X of N" di setiap halaman
                                using (XGraphics gfx = XGraphics.FromPdfPage(page))
                                {
                                    // ── Teks watermark diagonal (merah transparan) ──
                                    string watermarkText = $"COPY {c + 1} of {printCopy}";
                                    XFont watermarkFont = new XFont("Arial", 60);
                                    XBrush watermarkBrush = new XSolidBrush(
                                        XColor.FromArgb(60, 255, 0, 0)); // merah transparan

                                    var watermarkSize = gfx.MeasureString(watermarkText, watermarkFont);

                                    gfx.Save();
                                    gfx.TranslateTransform(page.Width / 2, page.Height / 2);
                                    gfx.RotateTransform(
                                        -Math.Atan(page.Height / page.Width) * 180 / Math.PI);
                                    gfx.TranslateTransform(-page.Width / 2, -page.Height / 2);

                                    gfx.DrawString(
                                        watermarkText,
                                        watermarkFont,
                                        watermarkBrush,
                                        new XPoint(
                                            (page.Width - watermarkSize.Width) / 2,
                                            (page.Height - watermarkSize.Height) / 2),
                                        XStringFormats.Default);

                                    gfx.Restore();

                                    // ── Label "COPY X of N" di pojok kanan atas ──
                                    XFont labelFont = new XFont("Arial", 10);
                                    XBrush labelBrush = new XSolidBrush(XColor.FromArgb(255, 200, 0, 0));
                                    XBrush bgBrush = new XSolidBrush(XColor.FromArgb(255, 255, 235, 235));

                                    string labelText = $"COPY {c + 1} of {printCopy}";
                                    var labelSize = gfx.MeasureString(labelText, labelFont);

                                    double padding = 4;
                                    double labelX = page.Width - labelSize.Width - padding * 2 - 10;
                                    double labelY = 8;

                                    // Background label
                                    gfx.DrawRectangle(
                                        bgBrush,
                                        new XRect(labelX - padding, labelY - padding,
                                                  labelSize.Width + padding * 2,
                                                  labelSize.Height + padding * 2));

                                    // Border label
                                    gfx.DrawRectangle(
                                        new XPen(XColor.FromArgb(255, 200, 0, 0), 0.5),
                                        new XRect(labelX - padding, labelY - padding,
                                                  labelSize.Width + padding * 2,
                                                  labelSize.Height + padding * 2));

                                    // Teks label
                                    gfx.DrawString(
                                        labelText,
                                        labelFont,
                                        labelBrush,
                                        new XPoint(labelX, labelY + labelSize.Height),
                                        XStringFormats.Default);
                                }
                            }
                        }

                        outputDoc.Save(multiCopyPath);
                    }

                    finalRelativePath = multiCopyRelative;
                }

                return Json(new { status = true, data = finalRelativePath });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        //fungsi tambahan untuk menghapus cache PDF dan watermark ketika dokumen di-approve, agar perubahan terbaru bisa terlihat saat diunduh atau dicetak
        private void DeleteDocumentCache(DocumentMaintenance documentMaintenance, string webRootPath)
        {
            try
            {
                string filePath = documentMaintenance?.FILE_PATH;

                // ✅ FILE_PATH null → ambil dari DB
                if (string.IsNullOrEmpty(filePath))
                {
                    if (documentMaintenance?.DOCUMENT_TRANSACTION_ID == null) return;

                    var docFromDb = documentMaintenanceRepo
                        .Search(new DocumentMaintenance
                        {
                            DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID
                        }, null, db, 1, 1)
                        .FirstOrDefault();

                    filePath = docFromDb?.FILE_PATH;
                }

                if (string.IsNullOrEmpty(filePath)) return;

                string[] split = filePath.Split("/");
                if (split.Length <= 4) return;

                string fileName = split[4];
                string ext = GetFileExtension(fileName);

                // Hapus cache PDF
                string cachedPdf = webRootPath + "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                    + fileName.Replace("." + ext, ".pdf");

                if (System.IO.File.Exists(cachedPdf))
                {
                    System.IO.File.Delete(cachedPdf);
                    Console.WriteLine($"[INFO] Cache deleted: {cachedPdf}");
                }

                // Hapus cache watermark
                string cachedWm = cachedPdf.Replace(".pdf", "_wm.pdf");
                if (System.IO.File.Exists(cachedWm))
                    System.IO.File.Delete(cachedWm);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] DeleteDocumentCache error: {ex.Message}");
            }
        }

        private bool TryGetExistingMergedRegion(ISheet sheet, int row, int col, out NPOI.SS.Util.CellRangeAddress existingRegion)
        {
            for (int i = 0; i < sheet.NumMergedRegions; i++)
            {
                var region = sheet.GetMergedRegion(i);
                if (region.IsInRange(row, col))
                {
                    existingRegion = region;
                    return true;
                }
            }
            existingRegion = null;
            return false;
        }

        private void WriteNameCellSafely(ISheet sheet, int row, int col, int mergeCellCol, string value)
        {
            if (TryGetExistingMergedRegion(sheet, row, col, out var existing))
            {
                IRow r = SafeGetRow(sheet, existing.FirstRow);
                ICell c = SafeGetCell(r, existing.FirstColumn);
                SetCellValueBlackFont(sheet.Workbook, c, value);
            }
            else
            {
                IRow r = SafeGetRow(sheet, row);
                ICell c = SafeGetCell(r, col);
                SetCellValueBlackFont(sheet.Workbook, c, value);
                sheet.AddMergedRegion(new NPOI.SS.Util.CellRangeAddress(
                    row, row, col, col + mergeCellCol - 1));
            }
        }
    }



    
}
