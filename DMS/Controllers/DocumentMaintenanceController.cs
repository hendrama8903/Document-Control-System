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
using Microsoft.EntityFrameworkCore;
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
        private P4DMaintenanceRepo p4DMaintenanceRepo = P4DMaintenanceRepo.Instance;
        private DepartmentMasterRepo departmentMasterRepo = DepartmentMasterRepo.Instance;
        private DivisionMasterRepo divisionMasterRepo = DivisionMasterRepo.Instance;
        private SectionMasterRepo sectionMasterRepo = SectionMasterRepo.Instance;
        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
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
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-DELETE");
            ViewData["Download"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-DOWNLOAD");
            ViewData["Approve"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-APPROVE");
            ViewData["Delete-FilePath"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENT-DELETE-FILEPATH");


            ViewData["Title"] = "Document Preparation";

            // Kode dokumen yang punya master template pengesahan (wwwroot/document/Template/{CODE}.xls
            // atau .xlsx) - dipakai toolbar utk menampilkan tombol "Download Template" hanya utk kode
            // yang memang punya filenya. Sama seperti DocumentMasterController.Index().
            string templateFolder = System.IO.Path.Combine(Environment.WebRootPath, "document", "Template");
            var templateCodes = System.IO.Directory.Exists(templateFolder)
                ? System.IO.Directory.GetFiles(templateFolder, "*.xls")
                    .Concat(System.IO.Directory.GetFiles(templateFolder, "*.xlsx"))
                    .Select(f => System.IO.Path.GetFileNameWithoutExtension(f))
                    .Distinct()
                    .ToList()
                : new List<string>();
            ViewData["TemplateCodes"] = System.Text.Json.JsonSerializer.Serialize(templateCodes);

            return View();
        }

        // Daftar divisi utk checkbox "Related Division" (fitur SPR/SIPOCOR Level 2,
        // request Hendra 2026-08-20) - baca langsung dari TB_M_DIVISION (Division
        // Master), BUKAN dari TB_M_SYSTEM SYSTEM_TYPE='DIVISION' yang dipakai
        // dropdown Division di form ini - dua sumber itu ternyata sudah tidak
        // sinkron (TB_M_SYSTEM masih bawa kode lama ASY/BDY/BPD/LCD/PPP yang
        // sudah tidak ada di Division Master, dan sebaliknya tidak punya
        // PCE/PID/PMA yang sudah ada di Division Master).
        public JsonResult GetRelatedDivisionOptions()
        {
            try
            {
                IList<DivisionMaster> divisions = divisionMasterRepo
                    .Search(new DivisionMaster(), db, null, null)
                    .OrderBy(x => x.DIVISION_CODE)
                    .ToList();

                var list = divisions.Select(x => new { code = x.DIVISION_CODE, name = x.DIVISION_NAME }).ToList();
                return Json(new { status = true, data = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Detail per template (nama/deskripsi kategori, format, ukuran file) utk popup "Template Center"
        // di toolbar Document Preparation - dipanggil on-demand saat tombol Template diklik, bukan
        // saat Index() supaya tidak query DocumentMaster tiap load halaman.
        public JsonResult GetTemplateList()
        {
            try
            {
                string templateFolder = System.IO.Path.Combine(Environment.WebRootPath, "document", "Template");
                var files = System.IO.Directory.Exists(templateFolder)
                    ? System.IO.Directory.GetFiles(templateFolder, "*.xls")
                        .Concat(System.IO.Directory.GetFiles(templateFolder, "*.xlsx"))
                        .GroupBy(f => System.IO.Path.GetFileNameWithoutExtension(f))
                        .Select(g => g.First())
                        .ToList()
                    : new List<string>();

                var nameByCode = documentMasterRepo.Search(new DocumentMaster(), db, null, null)
                    .GroupBy(x => x.DOCUMENT_CODE)
                    .ToDictionary(g => g.Key, g => g.First().DOCUMENT_NAME);

                var list = files.Select(f =>
                {
                    string code = System.IO.Path.GetFileNameWithoutExtension(f);
                    var info = new System.IO.FileInfo(f);
                    return new
                    {
                        code = code,
                        name = nameByCode.ContainsKey(code) ? nameByCode[code] : code,
                        format = info.Extension.TrimStart('.').ToUpper(),
                        sizeKb = Math.Ceiling(info.Length / 1024.0)
                    };
                }).OrderBy(x => x.code).ToList();

                return Json(new { status = true, data = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
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
                    // Dokumen hasil Legacy Document Import sudah jadi dokumen master
                    // (hardcopy berstempel MASTER) - disembunyikan dari grid Index ini
                    // (request Hendra 2026-08-28). Dipaksa di sini, bukan dari input
                    // client, supaya lookup lain yang reuse documentMaintenanceRepo.Search()
                    // (preview, download, approval, dst.) tidak ikut kena.
                    data.EXCLUDE_MIGRATION_FLAG = "1";

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

        public JsonResult GetManualNoPrefix(int? levelCode, string division, int? departmentId, string sectionCode,
            int? documentId, string processCode, string companyCode, DateTime? documentDate)
        {
            try
            {
                string prefix = documentMaintenanceRepo.GetManualNoPrefix(
                    levelCode, division, departmentId, sectionCode, documentId, processCode, companyCode, documentDate, db);

                if (string.IsNullOrEmpty(prefix))
                {
                    return Json(new { status = false, message = "Belum bisa membentuk prefix nomor - lengkapi field yang wajib diisi terlebih dahulu." });
                }

                return Json(new { status = true, prefix = prefix });
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

            // Path file lama (sebelum di-overwrite di bawah kalau ada file baru di-upload) -
            // dipakai untuk membersihkan file fisik + cache PDF lama saat file diganti di Edit.
            string previousFilePath = data.FILE_PATH;

            void DeleteCachedPdfFor(string filePath)
            {
                if (filePath == null) return;

                string[] splitCache = filePath.Split("/");
                if (splitCache.Length > 4)
                {
                    string cacheFileName = splitCache[4];
                    string cacheExt = GetFileExtension(cacheFileName);
                    string cachedPdf = webRootPath + "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                        + cacheFileName.Replace("." + cacheExt, ".pdf");

                    if (System.IO.File.Exists(cachedPdf))
                        System.IO.File.Delete(cachedPdf);
                }
            }

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

                    // Hapus cache PDF lama supaya generate ulang saat dibuka
                    if (result.status)
                    {
                        DeleteCachedPdfFor(data.FILE_PATH);

                        bool fileReplaced = previousFilePath != null && previousFilePath != data.FILE_PATH;
                        if (fileReplaced)
                        {
                            // File lama sudah digantikan file baru dan tidak lagi direferensikan
                            // di DB - bersihkan cache-nya juga plus file fisiknya, supaya tidak
                            // menumpuk jadi sampah permanen di disk (dokumen yang masih
                            // draft/pending belum punya riwayat revisi resmi untuk file ini).
                            DeleteCachedPdfFor(previousFilePath);

                            string oldPhysicalPath = webRootPath + previousFilePath.Trim();
                            if (System.IO.File.Exists(oldPhysicalPath))
                                System.IO.File.Delete(oldPhysicalPath);
                        }
                    }

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

                            if (NotificationSettingRepo.Instance.IsEmailEnabled("DOCUMENT_APPROVAL_REQUEST", db))
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

                    if (NotificationSettingRepo.Instance.IsEmailEnabled("DOCUMENT_APPROVE_REJECT", db))
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
                DocumentMaintenance currentDocument = documentMaintenanceRepo
                    .Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID }, null, db, 1, 1)
                    .FirstOrDefault();
                bool isObsolete = currentDocument == null;

                // Stempel gambar (bukan teks) berdasarkan STATUS dokumen, bukan siapa yang
                // login - request user 2026-08-09. DOC_STATUS: 1=Approved (Document
                // Preparation selesai), 5=Published (Effective, di-set
                // sp_P4DMaintenance_UpdateDistributionPublish begitu SEMUA department sudah
                // acknowledge distribusinya). Stempel MASTER (cap_master.png, pojok
                // kiri-atas) BUKAN muncul begitu Document Preparation selesai (STATUS=1) -
                // itu baru approval internal pembuatan dokumennya. MASTER baru muncul
                // setelah dokumen diproses lewat P4D dan di-approve/receive oleh QMS
                // (lihat IsReceivedByQms), atau kalau sudah lebih jauh lagi yaitu
                // STATUS=5/Published (request user 2026-08-11). Stempel CONTROLLED COPY
                // (cap_controlledcopy.png, pojok kanan-bawah) DITAMBAHKAN LAGI (bukan
                // menggantikan) begitu distribusi ke semua dept sudah di-acknowledge semua -
                // jadi keduanya bisa tampil sekaligus di posisi beda.
                bool isFullyAcknowledged = !isObsolete && currentDocument.STATUS == "5";
                bool isMasterStamped = !isObsolete && (isFullyAcknowledged || IsReceivedByQms(currentDocument.DOCUMENT_TRANSACTION_ID));

                // CONTROLLED COPY cuma relevan buat perspektif "saya user biasa yang
                // menerima copy terkendali" - staff dokumen kontrol (type 1/2: Document
                // Preparation, P4D, Document Control Dashboard) cukup lihat stempel MASTER,
                // karena bagi mereka file ini adalah master, bukan copy yang diterima.
                // Hanya viewer type 3 (UserDashboard, end user) yang lihat CONTROLLED COPY
                // (request Hendra 2026-08-14).
                bool isEndUserView = type == "3";

                string masterStampPath = null;
                string controlledCopyStampPath = null;
                string obsoleteStampPath = null;

                if (isObsolete)
                {
                    // Stempel gambar OBSOLETE (cap_obsolete.png) - request user 2026-08-10,
                    // menggantikan watermark teks diagonal supaya konsisten secara visual
                    // dengan stempel MASTER/CONTROLLED COPY di bawah.
                    obsoleteStampPath = webRootPath + "/images/cap_obsolete.png";
                }
                else
                {
                    if (isMasterStamped) masterStampPath = webRootPath + "/images/cap_master.png";
                    if (isFullyAcknowledged && isEndUserView) controlledCopyStampPath = webRootPath + "/images/cap_controlledcopy.png";
                }

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

                        if (masterStampPath != null || controlledCopyStampPath != null || obsoleteStampPath != null)
                        {
                            string watermarkedRelative = cachedPdfRelative.Replace(".pdf", "_wm.pdf");
                            string watermarkedFullPath = webRootPath + watermarkedRelative;
                            System.IO.File.Copy(cachedPdfFullPath, watermarkedFullPath, overwrite: true);
                            AddImageStamps(watermarkedFullPath, watermarkedFullPath, masterStampPath, controlledCopyStampPath, obsoleteStampPath);
                            servePath = watermarkedRelative;
                        }

                        ViewData["Title"] = "Document Preview";
                        // Query-string cache-buster - file-nya SELALU di-regenerate ulang di
                        // atas (overwrite: true) tiap request, tapi nama filenya tetap sama,
                        // jadi tanpa ini browser bisa nyangkut nge-serve versi PDF lama dari
                        // HTTP cache-nya sendiri walau isi file di server sudah beda (bug lama,
                        // ketauan pas nambahin stempel gambar - request user 2026-08-09).
                        ViewData["FilePath"] = servePath + "?v=" + DateTime.UtcNow.Ticks;
                        return View("~/Views/Preview/PDFPreview.cshtml");
                    }
                    // ✅ SAMPAI SINI

                    // Tulis field/tanda tangan pengesahan lewat DevExpress langsung dan
                    // export ke PDF dari workbook yang sama di memory - TIDAK lagi lewat
                    // NPOI simpan-ke-file lalu DevExpress baca-ulang (lihat
                    // GeneratePengesahanPdf untuk alasannya: kombinasi itu terbukti bisa
                    // menghasilkan PDF 0 halaman untuk workbook kompleks, Aug 2026).
                    string pengesahanPdfRelative = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                        + System.IO.Path.GetFileNameWithoutExtension(fileName) + ".pdf";
                    string pengesahanPdfFullPath = webRootPath + pengesahanPdfRelative;

                    result = GeneratePengesahanPdf(webRootPath, documentMaintenance.FILE_PATH, documentMaintenance, pengesahanPdfFullPath);
                    if (!result.status)
                    {
                        // Fallback ke Excel viewer kalau convert tetap gagal (Aug 2026).
                        return RedirectToAction("ExcelViewerPreview", new { filePath = documentMaintenance.FILE_PATH });
                    }

                    pengesahanModifiedfileNames = pengesahanPdfRelative;
                    extension = "pdf";
                }
                // Ganti bagian ini:
                // finalPath = webRootPath + pengesahanModifiedfileNames.Replace(extension, "pdf");
                // if (type == "3") AddWatermark(finalPath, finalPath, "CONTROLLED COPY");

                string convertedRelative = pengesahanModifiedfileNames.Replace(extension, "pdf");
                string convertedFullPath = webRootPath + convertedRelative;
                string serveRelative = convertedRelative;

                if (masterStampPath != null || controlledCopyStampPath != null || obsoleteStampPath != null)
                {
                    string watermarkedRelative = convertedRelative.Replace(".pdf", "_wm.pdf");
                    string watermarkedFullPath = webRootPath + watermarkedRelative;
                    System.IO.File.Copy(convertedFullPath, watermarkedFullPath, overwrite: true);
                    AddImageStamps(watermarkedFullPath, watermarkedFullPath, masterStampPath, controlledCopyStampPath, obsoleteStampPath);
                    serveRelative = watermarkedRelative;
                }

                ViewData["Title"] = "Document Preview";
                // Cache-buster - lihat komentar di cabang cached-PDF di atas.
                ViewData["FilePath"] = serveRelative + "?v=" + DateTime.UtcNow.Ticks;
                return View("~/Views/Preview/PDFPreview.cshtml");
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ViewBag.ErrorMessage = ex.Message;
                return View("Error500");
            }
        }

        // Contoh integrasi DevExpress.AspNetCore.Spreadsheet: tampilkan file Excel
        // apa adanya di browser (bukan hasil convert ke PDF). filePath = path relatif
        // terhadap wwwroot, sama seperti yang dipakai ViewAttachment.
        [HttpGet]
        public IActionResult SpreadsheetPreview(string filePath)
        {
            string webRootPath = Environment.WebRootPath;
            string fullPath = webRootPath + filePath;

            if (!System.IO.File.Exists(fullPath))
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ViewBag.ErrorMessage = "File Not Found";
                return View("Error500");
            }

            ViewData["Title"] = "Spreadsheet Preview";
            ViewData["PhysicalPath"] = fullPath;
            ViewData["WorkDirectory"] = System.IO.Path.Combine(webRootPath, "Upload", "ATTACHMENT", "DOCUMENT_TEMP");

            return View("~/Views/Preview/SpreadsheetPreview.cshtml");
        }

        // Handler wajib untuk widget Spreadsheet (DocumentRequestHandlerUrl) -
        // dipanggil widget via AJAX untuk operasi dokumen (buka/print/dll).
        [AcceptVerbs("GET", "POST")]
        public IActionResult SpreadsheetRequest()
        {
            return DevExpress.AspNetCore.Spreadsheet.SpreadsheetRequestProcessor.GetResponse(HttpContext);
        }

        // Viewer Excel pakai library pihak ketiga yang sudah ter-bundle di
        // wwwroot/lib/spreadsheet-viewer/ (Views/Preview/ExcelPreview.cshtml).
        // filePath = path relatif terhadap wwwroot, sama seperti ViewAttachment/PDFPreview.
        [HttpGet]
        public IActionResult ExcelViewerPreview(string filePath)
        {
            string webRootPath = Environment.WebRootPath;
            string fullPath = webRootPath + filePath;

            if (!System.IO.File.Exists(fullPath))
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ViewBag.ErrorMessage = "File Not Found";
                return View("Error500");
            }

            // Bundled spreadsheet-viewer JS lib cuma paham format OOXML (.xlsx) -
            // file .xls lama (binary/HSSF) gagal dibuka dengan error "Sorry, this
            // workbook file format is not supported yet." Convert dulu ke .xlsx
            // pakai DevExpress Spreadsheet Document API (library yang sama dipakai
            // ConvertToPdf untuk baca .xls), hasilnya di-cache di DOCUMENT_TEMP
            // seperti pola cache PDF yang sudah ada di ViewAttachment.
            string extension = GetFileExtension(System.IO.Path.GetFileName(fullPath));
            if (extension != null && extension.Equals("xls", StringComparison.OrdinalIgnoreCase))
            {
                string convertedRelative = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                    + System.IO.Path.GetFileNameWithoutExtension(fullPath) + ".xlsx";
                string convertedFullPath = webRootPath + convertedRelative;

                if (!System.IO.File.Exists(convertedFullPath))
                {
                    using (var workbook = new DevExpress.Spreadsheet.Workbook())
                    {
                        workbook.LoadDocument(fullPath);
                        workbook.SaveDocument(convertedFullPath, DevExpress.Spreadsheet.DocumentFormat.Xlsx);
                    }
                }

                filePath = convertedRelative;
            }

            ViewData["Title"] = "Excel Viewer Preview";
            ViewData["FilePath"] = filePath;

            return View("~/Views/Preview/ExcelPreview.cshtml");
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

        // Stempel gambar di pojok kanan-bawah tiap halaman (luar footer), sesuai
        // Posisi sesuai contoh stempel fisik asli (request user 2026-08-10):
        // MASTER (cap_master.png) di pojok KIRI-ATAS, menindih area header/title
        // block dokumen. CONTROLLED COPY (cap_controlledcopy.png) di TENGAH-BAWAH
        // halaman. Beda dari AddWatermark: ini gambar asli (opacity penuh, kayak
        // stempel tinta beneran), bukan teks transparan diagonal.
        //
        // OBSOLETE (cap_obsolete.png) pakai slot POSISI YANG SAMA dengan CONTROLLED
        // COPY (tengah-bawah) - menggantikan, bukan menumpuk. Dokumen obsolete
        // tidak pernah berstatus approved/published sekaligus (lihat pemanggil),
        // jadi controlledCopyImage dan obsoleteImage tidak akan pernah dua-duanya
        // terisi di saat bersamaan.
        public void AddImageStamps(string inputFilePath, string outputFilePath, string masterImagePath, string controlledCopyImagePath, string obsoleteImagePath = null)
        {
            const double stampWidth = 130; // points
            const double margin = 20;

            PdfDocument document = PdfReader.Open(inputFilePath, PdfDocumentOpenMode.Modify);
            document.Version = 17;

            XImage masterImage = masterImagePath != null && System.IO.File.Exists(masterImagePath)
                ? XImage.FromFile(masterImagePath) : null;
            XImage controlledCopyImage = controlledCopyImagePath != null && System.IO.File.Exists(controlledCopyImagePath)
                ? XImage.FromFile(controlledCopyImagePath) : null;
            XImage obsoleteImage = obsoleteImagePath != null && System.IO.File.Exists(obsoleteImagePath)
                ? XImage.FromFile(obsoleteImagePath) : null;
            XImage bottomImage = controlledCopyImage ?? obsoleteImage;

            try
            {
                foreach (PdfPage page in document.Pages)
                {
                    using XGraphics gfx = XGraphics.FromPdfPage(page);

                    if (masterImage != null)
                    {
                        double h = stampWidth * masterImage.PixelHeight / masterImage.PixelWidth;
                        gfx.DrawImage(masterImage, margin, margin, stampWidth, h);
                    }

                    if (bottomImage != null)
                    {
                        double h = stampWidth * bottomImage.PixelHeight / bottomImage.PixelWidth;
                        double x = (page.Width - stampWidth) / 2;
                        gfx.DrawImage(bottomImage, x, page.Height - h - margin, stampWidth, h);
                    }
                }

                document.Save(outputFilePath);
            }
            finally
            {
                masterImage?.Dispose();
                controlledCopyImage?.Dispose();
                obsoleteImage?.Dispose();
            }
        }

        /// <summary>
        /// Tulis field pengesahan (data dokumen, nama/tanda tangan creator &amp;
        /// approver) langsung lewat DevExpress Spreadsheet API, lalu export ke PDF
        /// dari workbook yang sama di memory - satu session, tanpa simpan-ke-file
        /// lalu baca-ulang pakai library lain.
        ///
        /// Sebelumnya field-field ini ditulis pakai NPOI, disimpan ke .xlsx, baru
        /// file itu dibaca lagi oleh DevExpress untuk di-export ke PDF. Ternyata
        /// kombinasi itu punya bug nyata: NPOI menulis ulang struktur XML file
        /// dengan caranya sendiri (beda dari cara Excel/SharePoint aslinya), dan
        /// untuk workbook kompleks (banyak sheet, ada yang hidden) itu bisa
        /// menghasilkan file yang DevExpress gagal hitung halamannya sama sekali
        /// (PDF 0 halaman, tampil blank di viewer) - padahal file aslinya normal
        /// dan DevExpress sendiri baik-baik saja membacanya. Dibuktikan lewat
        /// reproduksi terisolasi (Aug 2026): bahkan NPOI load+save TANPA modifikasi
        /// apa pun sudah cukup memicu bug ini. Redesign ini menghilangkan celah
        /// itu total karena tidak ada lagi serialisasi lewat NPOI di alur ini.
        /// </summary>
        public DBResult GeneratePengesahanPdf(string webRootPath, string filePath, DocumentMaintenance documentMaintenanceParam, string outputPdfPath)
        {
            try
            {
                DocumentMaintenance documentMaintenance = documentMaintenanceRepo
                    .Search(documentMaintenanceParam, null, db, 1, 1)
                    .FirstOrDefault();

                if (documentMaintenance == null)
                {
                    documentMaintenance = documentMaintenanceRepo
                        .SearchHistoryToMaintenance(documentMaintenanceParam, GetLoginUsername(), db, 1, 1)
                        .FirstOrDefault();
                }

                if (documentMaintenance == null) return new DBResult(false, "Document not found");

                IList<ApprovalDetail> approvalDetails =
                    approvalRepo.GetApprovalDetail((int)documentMaintenance.APPROVAL_ID, db, null, null);

                // Kotak tanda tangan pertama (type2Index==0, "Dibuat Oleh") SELALU
                // menampilkan si pembuat dokumen (documentMaintenance.CREATED_BY),
                // lepas dari apa isi WORKFLOW_SEQ=1 di TB_R_APPROVAL_D. Normalnya
                // WORKFLOW_SEQ=1 memang milik si pembuat sendiri (auto-approved oleh
                // sp_WorkflowDoc_Create saat @DOCUMENT_CREATOR = @APPROVER) sehingga
                // kotak-kotak approver berikutnya (index>=1) aman mulai mencari dari
                // WORKFLOW_SEQ+1. TAPI kalau posisi "Dibuat Oleh" di konfigurasi
                // workflow (TB_M_WORKFLOW_DOC_D) tidak cocok dengan posisi pembuat
                // aslinya (mis. dokumen Pedoman/Prosedur level tinggi yang justru
                // dibuat oleh Staff biasa), WORKFLOW_SEQ=1 jadi approver SUNGGUHAN
                // yang berbeda dari pembuat - kalau tetap dilewati, approver itu sama
                // sekali tidak pernah muncul di PDF (bug ditemukan 2026-08-18: dokumen
                // Pedoman, Division Head approve WORKFLOW_SEQ=1 tapi tidak tercetak).
                // Deteksi kondisinya dengan cek langsung ke data approval, bukan
                // menduga dari LABEL (LABEL "Dibuat Oleh" bisa menempel ke approver
                // manapun kalau workflow-nya salah pasang).
                bool creatorAutoApprovedSeq1 = approvalDetails.Any(x =>
                    x.WORKFLOW_SEQ == 1 && string.Equals(x.APPROVER, documentMaintenance.CREATED_BY, StringComparison.OrdinalIgnoreCase));
                int approverSeqOffset = creatorAutoApprovedSeq1 ? 1 : 0;

                string fullPath = webRootPath + filePath;

                IList<ExcelTemplateMaster> excelTemplateMasters = documentMasterRepo
                    .SearchTemplate(new ExcelTemplateMaster { DOCUMENT_ID = documentMaintenance.DOCUMENT_ID }, db);

                if (excelTemplateMasters.Count == 0) return new DBResult(false, "Excel template configuration not found for this document type");

                // Kalau pembuat dokumen KEBETULAN punya jabatan yang juga punya box
                // sendiri (mis. Dept Head bikin dokumen Pedoman, dan template PDM
                // punya box khusus "DEPT. HEAD"), tanda tangannya dipindah SEPENUHNYA
                // ke box jabatan itu, bukan ke box "PIC" generik - box PIC cuma dipakai
                // kalau jabatan pembuat memang tidak punya box khusus (mis. Staff biasa)
                // (request Hendra 2026-08-18).
                User creator = UserRepo.Instance.GetByKey(new User { USERNAME = documentMaintenance.CREATED_BY }, db);
                int? creatorPositionId = creator?.POSITION_ID;
                bool creatorHasSpecificBox = creatorPositionId != null && excelTemplateMasters.Any(t =>
                    t.TYPE == 2 && "DIGITAL_SIGN".Equals(t.FIELD_NAME) &&
                    t.TARGET_POSITION_ID != null && t.TARGET_POSITION_ID != -1 &&
                    t.TARGET_POSITION_ID == creatorPositionId);

                // Template Excel EIS (5) & SOE (6) TIDAK punya baris terpisah untuk nama
                // approver di blok tanda tangan (dicek langsung ke file .xls-nya
                // 2026-08-16) - beda dari template lain (mis. IK/SOP) yang punya. Baris
                // "nama" pada template ini ikut ter-merge jadi satu sel bersama kotak
                // gambar tanda tangan, sehingga kalau tetap dipaksa ditulis, teksnya
                // ke-squeeze mepet ke baris jabatan di bawahnya (dilaporkan user, lihat
                // review PDF pengesahan EIS). Diputuskan (keputusan Hendra) untuk
                // template gaya ini cukup tampilkan tanda tangan + jabatan saja, TANPA
                // nama - sesuai desain asli template kosongnya.
                bool skipApproverNameText = documentMaintenance.DOCUMENT_ID == 5 || documentMaintenance.DOCUMENT_ID == 6;

                // Template PDM (Pedoman) & PRO (Prosedur) - cukup tanda tangan + Nama yang
                // tercetak di kotak "Lembar Pengesahan", baris jabatan di bawahnya TIDAK
                // perlu (request Hendra 2026-08-18, diperluas ke PRO 2026-08-19 - awalnya
                // cuma PDM yang diminta, tapi Level 2 PRO pakai template & box layout yang
                // sama persis jadi harus konsisten). Tipe lain tetap tampilkan jabatan
                // seperti biasa, tidak berubah.
                //
                // EIS (5) & SOE (6) juga ditambahkan ke sini (dilaporkan user 2026-08-27,
                // hasil cetak EIS) - baris "DEPT. HEAD/SECTION HEAD/STAFF" di bawah kotak
                // tanda tangan template ini SUDAH teks statis bawaan template, jadi tulisan
                // jabatan (user.POSITION_NAME) yang ditulis kode di baris +2 cuma jadi
                // duplikat kecil menempel di bawahnya, bukan mengisi kotak yang kosong.
                bool skipApproverPositionText = documentMaintenance.DOCUMENT_ID == 1 || documentMaintenance.DOCUMENT_ID == 2
                    || documentMaintenance.DOCUMENT_ID == 5 || documentMaintenance.DOCUMENT_ID == 6;

                // Field yang HANYA boleh ditulis di COVER sheet (sheetPosition=0) -
                // sheet lain sudah punya formula =COVER!xxx sehingga otomatis update.
                var coverOnlyFields = new HashSet<string>
                {
                    "DOCUMENT_CODE",
                    "DOCUMENT_TRANSACTION_NAME",
                    "DOCUMENT_REVISION_0_DATE",
                    "REVISION",
                    "DOCUMENT_DATE"
                };

                using (var workbook = new DevExpress.Spreadsheet.Workbook())
                {
                    workbook.LoadDocument(fullPath);

                    int sheetPosition = 0;
                    int selectedSheetPosition = 0;

                    foreach (DevExpress.Spreadsheet.Worksheet sheet in workbook.Worksheets)
                    {
                        DevExpress.Spreadsheet.WorksheetPrintOptions printOptions = sheet.PrintOptions;
                        int orientation = sheet.ActiveView.Orientation == DevExpress.Spreadsheet.PageOrientation.Landscape ? 1 : 0;

                        // Paksa print area selalu pas di 1 halaman - posisi field
                        // pengesahan (ROW/COL di TB_M_EXCEL_TEMPLATE) mengasumsikan
                        // layout satu halaman. DIKECUALIKAN untuk SPR (DOCUMENT_ID=15,
                        // request Hendra 2026-08-21) - template ini sudah didesain untuk
                        // ukuran kertasnya sendiri (di-cek: page setup file SPR memang
                        // sudah pas, bukan perlu di-shrink), dan pemaksaan fit-1-halaman
                        // ini yang bikin previewnya "pecah"/distorsi (isi ke-squeeze).
                        // Aman dilepas khusus SPR karena semua kotak tanda tangannya
                        // (WriteSprSignatureSection & WriteRelatedDivisionSection) pakai
                        // referensi SEL (row/col index), bukan koordinat piksel halaman -
                        // jadi tidak bergantung pada skala/pagination cetak.
                        if (documentMaintenance.DOCUMENT_ID != 15)
                        {
                            printOptions.FitToWidth = 1;
                            printOptions.FitToHeight = 1;
                            printOptions.FitToPage = true;
                        }

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
                         .OrderBy(x => x.TEMPLATE_ID)
                         .ToList();

                        // Marker berbasis Excel Named Range (pilot 2026-08-16, baru dipasang
                        // di EIS.xls) - kalau sheet yang diupload sudah punya named range
                        // SIGN_DISETUJUI/DIPERIKSA/DIBUAT & TITLE_..., posisinya dipakai
                        // langsung (tidak lagi bergantung ke angka ROW/COL/MERGE_CELL_ROW/COL
                        // di TB_M_EXCEL_TEMPLATE yang gampang basi kalau baris/kolom digeser).
                        // Kalau named range tidak ada (semua template lain untuk saat ini),
                        // otomatis jatuh balik ke mekanisme ROW/COL lama - tidak ada perubahan
                        // perilaku untuk tipe dokumen selain EIS.
                        // Pemetaan kolom->kotak pakai URUTAN (kiri ke kanan = Disetujui/
                        // Diperiksa/Dibuat), bukan angka kolom hardcode, supaya otomatis
                        // berlaku juga kalau template lain menyusul dikasih named range.
                        Dictionary<int, string> signBoxKeyByCol = new Dictionary<int, string>();
                        if (type2Templates.Count > 1)
                        {
                            string[] boxKeysLeftToRight = { "DISETUJUI", "DIPERIKSA", "DIBUAT" };
                            var sortedByCol = type2Templates.OrderBy(x => x.COL).ToList();
                            for (int i = 0; i < sortedByCol.Count && i < boxKeysLeftToRight.Length; i++)
                            {
                                signBoxKeyByCol[(int)sortedByCol[i].COL] = boxKeysLeftToRight[i];
                            }
                        }

                        // Kalau konfigurasi template cuma py 1 baris DIGITAL_SIGN (jalur fallback
                        // lama - mis. orientasi landscape EIS/SOE yang belum lengkap), kode lama
                        // menghitung posisi 3 kotak via WORKFLOW_SEQ (1=Dibuat/creator,
                        // 2=Diperiksa, 3=Disetujui - urutan ini yang dipakai matematika
                        // colIndex sebelumnya). Dipetakan ke nama marker yang sama supaya
                        // marker tetap dipakai walau lewat jalur fallback ini (2026-08-16,
                        // ditemukan lewat pengujian geser baris - jalur ini sebelumnya
                        // sama sekali tidak tersentuh marker karena hanya dicek saat
                        // type2Templates.Count > 1).
                        string BoxKeyBySeqFallback(int? workflowSeq) => workflowSeq switch
                        {
                            1 => "DIBUAT",
                            2 => "DIPERIKSA",
                            3 => "DISETUJUI",
                            _ => null
                        };

                        string ResolveBoxKey(int col, int? workflowSeqForFallback) => type2Templates.Count > 1
                            ? (signBoxKeyByCol.TryGetValue(col, out string bk) ? bk : null)
                            : BoxKeyBySeqFallback(workflowSeqForFallback);

                        // Safety-net: kalau nama named range ADA tapi range-nya sudah rusak
                        // (mis. jadi #REF! karena baris/kolom target dihapus, bukan digeser),
                        // DevExpress bisa melempar exception saat .Range diakses. Daripada
                        // seluruh generate PDF gagal (fallback ke ExcelViewerPreview), marker
                        // yang rusak dianggap tidak ada supaya jalur ROW/COL lama yang jalan.
                        DevExpress.Spreadsheet.DefinedName GetValidDefinedName(string name)
                        {
                            DevExpress.Spreadsheet.DefinedName dn = sheet.DefinedNames.GetDefinedName(name);
                            if (dn == null) return null;
                            try
                            {
                                DevExpress.Spreadsheet.CellRange range = dn.Range;
                                if (range == null) return null;
                                int touch = range.TopRowIndex + range.LeftColumnIndex;
                                return dn;
                            }
                            catch
                            {
                                return null;
                            }
                        }

                        DevExpress.Spreadsheet.DefinedName GetSignMarker(int col, int? workflowSeqForFallback = null)
                        {
                            string boxKey = ResolveBoxKey(col, workflowSeqForFallback);
                            return boxKey != null ? GetValidDefinedName("SIGN_" + boxKey) : null;
                        }

                        // NAME_x - baris nama approver (dipisah dari TITLE_x karena template
                        // gaya IK/SOP/OPL/ACU punya baris nama TERSENDIRI di antara gambar
                        // tanda tangan dan baris jabatan; EIS/SOE tidak, makanya file itu
                        // tidak dikasih named range ini - GetNameMarker otomatis balik null
                        // dan jalur lama (skipApproverNameText / ROW manual) yang jalan).
                        DevExpress.Spreadsheet.DefinedName GetNameMarker(int col, int? workflowSeqForFallback = null)
                        {
                            string boxKey = ResolveBoxKey(col, workflowSeqForFallback);
                            return boxKey != null ? GetValidDefinedName("NAME_" + boxKey) : null;
                        }

                        DevExpress.Spreadsheet.DefinedName GetTitleMarker(int col, int? workflowSeqForFallback = null)
                        {
                            string boxKey = ResolveBoxKey(col, workflowSeqForFallback);
                            return boxKey != null ? GetValidDefinedName("TITLE_" + boxKey) : null;
                        }

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

                                DevExpress.Spreadsheet.DefinedName fieldMarker = GetValidDefinedName(template.FIELD_NAME);
                                DevExpress.Spreadsheet.Cell targetCell = fieldMarker != null
                                    ? sheet[fieldMarker.Range.TopRowIndex, fieldMarker.Range.LeftColumnIndex]
                                    : sheet[(int)template.ROW, (int)template.COL];
                                targetCell = DxResolveMergeAnchor(targetCell);

                                DxSetCellValueBlackFont(targetCell, propValue.ToString());
                            }

                            if (template.TYPE == 2)
                            {
                                int targetSheet = template.SHEET_POSITION ?? 0;
                                if (targetSheet != sheetPosition) continue;

                                if (approvalDetails.Count == 0) continue;
                                if (!template.FIELD_NAME.Equals("DIGITAL_SIGN")) continue;

                                // Approver dipetakan ke kotak berdasarkan WORKFLOW_SEQ, bukan
                                // urutan iterasi — supaya langkah yang dilewati saat pembuatan
                                // workflow (mis. section tanpa Section Head) menyisakan
                                // kotaknya kosong.
                                //
                                // TAPI kalau box ini punya TARGET_POSITION_ID (template
                                // PDM/PRO - box-nya punya caption jabatan spesifik seperti
                                // "MIN. DIV. HEAD" yang tercetak di file), pemetaannya beda:
                                // dicocokkan ke JABATAN ASLI approver, bukan urutan approval-nya
                                // - supaya approver yang jabatannya "Div. Head" selalu masuk
                                // kotak Div. Head walau kebetulan dia yang approve duluan
                                // (request Hendra 2026-08-18, kasus divhead.itd approve sebagai
                                // langkah pertama Level 1 tapi harus tercetak di kotak Div.
                                // Head, bukan kotak pertama/"Dept. Head"). -1 = kotak generik
                                // untuk approver TERAKHIR dalam chain apapun jabatannya (mis.
                                // "Disetujui Oleh" - bisa EO, bisa Direktur, tergantung level).
                                IList<ApprovalDetail> targetApprovers;
                                if (template.TARGET_POSITION_ID == -1)
                                {
                                    ApprovalDetail lastApprover = approvalDetails.OrderByDescending(x => x.WORKFLOW_SEQ).FirstOrDefault();
                                    targetApprovers = lastApprover != null ? new List<ApprovalDetail> { lastApprover } : new List<ApprovalDetail>();
                                }
                                else if (template.TARGET_POSITION_ID != null)
                                {
                                    int wantedPositionId = (int)template.TARGET_POSITION_ID;
                                    targetApprovers = approvalDetails.Where(x =>
                                    {
                                        User approverUser = UserRepo.Instance.GetByKey(new User { USERNAME = x.APPROVER }, db);
                                        return approverUser != null && approverUser.POSITION_ID == wantedPositionId;
                                    }).ToList();
                                }
                                else
                                {
                                    targetApprovers = type2Templates.Count > 1
                                        ? approvalDetails.Where(x => x.WORKFLOW_SEQ == type2Index + approverSeqOffset).ToList()
                                        : approvalDetails.OrderBy(x => x.WORKFLOW_SEQ).ToList();
                                }

                                // Menulis info pembuat dokumen (nama/jabatan/tanda tangan,
                                // selalu tanpa syarat STATUS - beda dari approver biasa yang
                                // hanya tercetak tanda tangannya kalau sudah Approved) ke box
                                // manapun yang diberikan - dipakai baik untuk box "PIC" generik
                                // maupun box jabatan spesifik kalau ternyata pembuatnya
                                // kebetulan punya jabatan itu (lihat creatorHasSpecificBox).
                                void WriteCreatorIntoBox(ExcelTemplateMaster boxTemplate)
                                {
                                    if (creator == null) return;

                                    int creatorCol = (int)boxTemplate.COL;
                                    DevExpress.Spreadsheet.DefinedName creatorSignMarker = GetSignMarker(creatorCol, 1);
                                    DevExpress.Spreadsheet.DefinedName creatorNameMarker = GetNameMarker(creatorCol, 1);
                                    DevExpress.Spreadsheet.DefinedName creatorTitleMarker = GetTitleMarker(creatorCol, 1);

                                    // Kotak pakai 2 baris: baris tepat di bawah gambar tanda
                                    // tangan (+1) nampilin NAMA LENGKAP, baris berikutnya (+2)
                                    // nampilin POSISI - baris +2 ini di template sering sudah
                                    // ada teks contoh lama yang nempel dari waktu template
                                    // dibuat (mis. "Jayadi", "Santika"), jadi ditimpa langsung
                                    // (request user 2026-08-10).
                                    int creatorNameRow = creatorNameMarker != null
                                        ? creatorNameMarker.Range.TopRowIndex
                                        : (int)boxTemplate.ROW + (int)boxTemplate.MERGE_CELL_ROW + 1;
                                    int creatorNameCol = creatorNameMarker != null
                                        ? creatorNameMarker.Range.LeftColumnIndex
                                        : creatorCol;
                                    int creatorNameMergeCol = creatorNameMarker != null
                                        ? creatorNameMarker.Range.ColumnCount
                                        : (int)boxTemplate.MERGE_CELL_COL;
                                    int creatorPositionRow = (int)boxTemplate.ROW + (int)boxTemplate.MERGE_CELL_ROW + 2;

                                    if (!skipApproverNameText)
                                    {
                                        // Pakai DxWriteNameCellSafely (bukan tulis langsung ke
                                        // anchor marker) karena NAME_x sengaja menunjuk ke
                                        // sebagian dari merged cell yang SAMA dengan gambar
                                        // tanda tangan (mis. IK/SOP/OPL/ACU) - kalau ditulis
                                        // langsung akan gagal/nggak nempel, jadi tetap perlu
                                        // dialihkan ke anchor merge itu.
                                        DxWriteNameCellSafely(sheet, creatorNameRow, creatorNameCol, creatorNameMergeCol, creator.FULL_NAME);
                                    }

                                    if (!skipApproverPositionText)
                                    {
                                        if (creatorTitleMarker != null)
                                        {
                                            DxSetCellValueBlackFontBottomAligned(
                                                sheet[creatorTitleMarker.Range.TopRowIndex, creatorTitleMarker.Range.LeftColumnIndex],
                                                creator.POSITION_NAME);
                                        }
                                        else
                                        {
                                            DxWriteNameCellSafely(sheet, creatorPositionRow, creatorCol, (int)boxTemplate.MERGE_CELL_COL, creator.POSITION_NAME);
                                        }
                                    }

                                    if (!string.IsNullOrEmpty(creator.SIGNATURE_PATH))
                                    {
                                        string creatorSignPath = webRootPath + creator.SIGNATURE_PATH;
                                        if (System.IO.File.Exists(creatorSignPath))
                                        {
                                            if (creatorSignMarker != null)
                                            {
                                                DxAddPicture(sheet, creatorSignPath, creatorSignMarker.Range);
                                            }
                                            else
                                            {
                                                int creatorRowStart = (int)boxTemplate.ROW + 1;
                                                DxAddPicture(sheet, creatorSignPath, creatorRowStart, creatorCol, (int)boxTemplate.MERGE_CELL_COL, (int)boxTemplate.MERGE_CELL_ROW);
                                            }
                                        }
                                    }
                                }

                                if (type2Templates.Count > 1 && type2Index == 0)
                                {
                                    // Box "PIC" generik cuma dipakai kalau jabatan pembuat TIDAK
                                    // punya box khusus sendiri di template ini - kalau punya
                                    // (lihat creatorHasSpecificBox), box PIC dibiarkan kosong,
                                    // tanda tangannya dipindah sepenuhnya ke box jabatan itu
                                    // (request Hendra 2026-08-18).
                                    if (!creatorHasSpecificBox)
                                    {
                                        WriteCreatorIntoBox(template);
                                    }
                                    type2Index++;
                                    continue;
                                }

                                // Box jabatan spesifik ini kebetulan cocok dengan jabatan asli
                                // si pembuat dokumen - tampilkan info pembuat di sini (bukan di
                                // box PIC), bukan approver dari TB_R_APPROVAL_D.
                                if (creatorHasSpecificBox && template.TARGET_POSITION_ID == creatorPositionId)
                                {
                                    WriteCreatorIntoBox(template);
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

                                    // Pakai DxWriteNameCellSafely supaya kalau baris ini ternyata
                                    // masih di dalam merged region gambar tanda tangan, value-nya
                                    // dialihkan ke cell anchor merge itu.
                                    // Baris posisi (+2) di template sering sudah ada teks contoh
                                    // lama yang nempel dari waktu template dibuat (mis. "Imbrianto
                                    // K", "Santika"), jadi ditimpa langsung dengan posisi approver
                                    // yang sebenarnya - sama seperti kotak "Dibuat Oleh".
                                    int positionRowIndex = (int)template.ROW + (int)template.MERGE_CELL_ROW + 2;
                                    DevExpress.Spreadsheet.DefinedName approverSignMarker = GetSignMarker(colIndex, (int?)approvalDetail.WORKFLOW_SEQ);
                                    DevExpress.Spreadsheet.DefinedName approverNameMarker = GetNameMarker(colIndex, (int?)approvalDetail.WORKFLOW_SEQ);
                                    DevExpress.Spreadsheet.DefinedName approverTitleMarker = GetTitleMarker(colIndex, (int?)approvalDetail.WORKFLOW_SEQ);

                                    int nameRowIndex = approverNameMarker != null
                                        ? approverNameMarker.Range.TopRowIndex
                                        : (int)template.ROW + (int)template.MERGE_CELL_ROW + 1;
                                    int nameColIndex = approverNameMarker != null
                                        ? approverNameMarker.Range.LeftColumnIndex
                                        : colIndex;
                                    int nameMergeColIndex = approverNameMarker != null
                                        ? approverNameMarker.Range.ColumnCount
                                        : (int)template.MERGE_CELL_COL;

                                    if (!skipApproverNameText)
                                    {
                                        DxWriteNameCellSafely(sheet, nameRowIndex, nameColIndex, nameMergeColIndex, user.FULL_NAME);
                                    }

                                    if (!skipApproverPositionText)
                                    {
                                        if (approverTitleMarker != null)
                                        {
                                            DxSetCellValueBlackFontBottomAligned(
                                                sheet[approverTitleMarker.Range.TopRowIndex, approverTitleMarker.Range.LeftColumnIndex],
                                                user.POSITION_NAME);
                                        }
                                        else
                                        {
                                            DxWriteNameCellSafely(sheet, positionRowIndex, colIndex, (int)template.MERGE_CELL_COL, user.POSITION_NAME);
                                        }
                                    }

                                    if ("1".Equals(approvalDetail.STATUS))
                                    {
                                        string signFullPath = webRootPath + user.SIGNATURE_PATH;
                                        if (System.IO.File.Exists(signFullPath))
                                        {
                                            if (approverSignMarker != null)
                                            {
                                                DxAddPicture(sheet, signFullPath, approverSignMarker.Range);
                                            }
                                            else
                                            {
                                                int signRowStart = (int)template.ROW + 1;
                                                DxAddPicture(sheet, signFullPath, signRowStart, colIndex, (int)template.MERGE_CELL_COL, (int)template.MERGE_CELL_ROW);
                                            }
                                        }
                                    }
                                }

                                type2Index++;
                            }

                            if (template.TYPE == 3)
                            {
                                DevExpress.Spreadsheet.DefinedName dateMarker = GetValidDefinedName(template.FIELD_NAME);
                                DevExpress.Spreadsheet.Cell dateCell = dateMarker != null
                                    ? sheet[dateMarker.Range.TopRowIndex, dateMarker.Range.LeftColumnIndex]
                                    : sheet[(int)template.ROW, (int)template.COL];
                                dateCell = DxResolveMergeAnchor(dateCell);

                                DxSetCellValueBlackFont(dateCell, "-"); // default

                                if (template.FIELD_NAME.Equals("DOCUMENT_REVISION_0_DATE"))
                                {
                                    PropertyInfo propertyInfo = documentMaintenance.GetType()
                                        .GetProperty(template.FIELD_NAME);
                                    if (propertyInfo == null) continue;
                                    object propValue = propertyInfo.GetValue(documentMaintenance);
                                    if (propValue == null) continue;

                                    string formattedDate = ParseAndFormatDate(propValue.ToString());
                                    DxSetCellValueBlackFont(dateCell, formattedDate);
                                }

                                if (template.FIELD_NAME.Equals("DOCUMENT_DATE"))
                                {
                                    // "Tanggal Dikeluarkan" - tanggal terbit dokumen ini, harus
                                    // selalu tampil berapa pun REVISION-nya (termasuk revisi 0,
                                    // dokumen pertama kali terbit). Sebelumnya ada syarat
                                    // "revisionValue != 0" yang malah menyembunyikan tanggal
                                    // ini justru di revisi 0 - salah tempat, dihapus (dilaporkan
                                    // user 2026-08-27, header SIPOCOR kosong).
                                    PropertyInfo propertyInfo = documentMaintenance.GetType().GetProperty(template.FIELD_NAME);
                                    if (propertyInfo == null) continue;

                                    object propValue = propertyInfo.GetValue(documentMaintenance);
                                    if (propValue == null) continue;

                                    string formattedDate = ParseAndFormatDate(propValue.ToString());
                                    DxSetCellValueBlackFont(dateCell, formattedDate);
                                }
                            }
                        }

                        // Kotak tanda tangan Disetujui/Diperiksa/Dibuat & Fitur "Divisi
                        // Terkait" - khusus SPR (SIPOCOR) Level 2, sheet Cover saja
                        // (sheetPosition 0). Bukan bagian TB_M_EXCEL_TEMPLATE biasa -
                        // yang pertama karena mekanisme generik cuma dukung 3 kotak
                        // (lihat WriteSprSignatureSection), yang kedua karena butuh N
                        // baris dinamis per divisi terkait (request Hendra 2026-08-20,
                        // signature section menyusul 2026-08-21).
                        if (documentMaintenance.DOCUMENT_ID == 15 && sheetPosition == 0)
                        {
                            WriteSprSignatureSection(sheet, approvalDetails, creator, webRootPath);
                            WriteRelatedDivisionSection(sheet, documentMaintenance, webRootPath);
                        }

                        // Fitur "Divisi Terkait" untuk tipe non-SPR yang templatenya punya
                        // sheet "LEMBAR MENGETAHUI" sendiri (PDM/PRO, dicek 2026-08-26 -
                        // request Hendra: kotak tanda tangan Mengetahui belum pernah terisi
                        // sama sekali di sheet ini). Dipicu lewat NAMA sheet (bukan
                        // DOCUMENT_ID seperti SPR di atas) supaya otomatis ikut jalan untuk
                        // tipe Level 2 lain di masa depan yang kebetulan pakai sheet dengan
                        // nama+layout sama persis - lihat WriteLembarMengetahuiSection.
                        if ("LEMBAR MENGETAHUI".Equals(sheet.Name?.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            WriteLembarMengetahuiSection(sheet, documentMaintenance, webRootPath);
                        }

                        sheetPosition++;
                    }

                    // DevExpress menghitung ulang formula (termasuk lintas-sheet, mis.
                    // "=COVER!H5") sendiri saat export - tidak perlu "flatten to values"
                    // manual seperti dulu (itu workaround khusus LibreOffice headless
                    // yang tidak selalu recalculate).
                    workbook.Calculate();

                    workbook.ExportToPdf(outputPdfPath);
                }

                // Safety-net yang sama seperti ConvertToPdf: pastikan hasilnya benar-benar
                // punya halaman sebelum dianggap sukses.
                using (PdfDocument checkDoc = PdfReader.Open(outputPdfPath, PdfDocumentOpenMode.InformationOnly))
                {
                    if (checkDoc.PageCount == 0)
                    {
                        System.IO.File.Delete(outputPdfPath);
                        return new DBResult(false, "PDF conversion produced no pages.");
                    }
                }

                return new DBResult(true, "File Converted");
            }
            catch (Exception ex)
            {
                return new DBResult(false, ex.Message);
            }
        }

        // Stempel MASTER (cap_master.png) cuma boleh muncul setelah dokumen didaftarkan
        // ke P4D DAN sudah di-approve/receive oleh QMS (TB_R_CTRL_DOCUMENT.STATUS = '2',
        // lihat sp_P4DMaintenance_ApproveReject) - BUKAN begitu approval Document
        // Preparation-nya sendiri (TB_R_DOCUMENT.STATUS = '1') selesai. loginUser
        // sengaja null - ini cek eksistensi/status murni, tanpa scoping divisi/dept.
        private bool IsReceivedByQms(int? documentTransactionId)
        {
            DocumentControlMaintenance ctrlDocument = p4DMaintenanceRepo
                .Search(new DocumentControlMaintenance { DOCUMENT_TRANSACTION_ID = documentTransactionId }, null, db, 1, 1)
                .FirstOrDefault();
            return ctrlDocument != null && ctrlDocument.STATUS == "2";
        }

        private void DxSetCellValueBlackFont(DevExpress.Spreadsheet.Cell cell, string value)
        {
            cell.SetValueFromText(value);
            cell.Font.Color = System.Drawing.Color.Black;
        }

        // Kalau target cell ternyata bagian NON-ANCHOR dari merged region (mis.
        // header "Nomor Dokumen"/"Revisi" di template SPR yang value-cell-nya
        // di-merge lebar ke kanan), nulis langsung ke situ gagal/nggak kebaca
        // (persis kasus yang sudah didokumentasikan di DxWriteNameCellSafely) -
        // dialihkan ke anchor (kiri-atas) merge itu dulu. Beda dari
        // DxWriteNameCellSafely, helper ini TIDAK membuat merge baru kalau belum
        // ada, cuma redirect kalau memang sudah ke-merge dari template aslinya
        // (dipakai untuk field header sederhana TYPE 1/3, bukan kotak nama/tanda
        // tangan approver). Ditemukan 2026-08-27 - header Nomor Dokumen/Tanggal
        // Dikeluarkan/Revisi/Tanggal Revisi SIPOCOR kosong total di hasil cetak.
        private DevExpress.Spreadsheet.Cell DxResolveMergeAnchor(DevExpress.Spreadsheet.Cell cell)
        {
            IList<DevExpress.Spreadsheet.CellRange> merges = cell.GetMergedRanges();
            if (merges.Count > 0)
            {
                DevExpress.Spreadsheet.CellRange anchor = merges[0];
                return cell.Worksheet[anchor.TopRowIndex, anchor.LeftColumnIndex];
            }
            return cell;
        }

        private void DxSetCellValueBlackFontBottomAligned(DevExpress.Spreadsheet.Cell cell, string value)
        {
            DxSetCellValueBlackFont(cell, value);

            // Baris nama & posisi pengesahan sering menimpa teks contoh lama di
            // template (lihat komentar DxWriteNameCellSafely) yang formatnya beda-beda
            // antar file template (warna, bold, rata kiri/kanan) - dipaksa konsisten
            // di sini (center, non-bold) supaya tidak ikut kebawa dari template asli
            // walau warnanya sudah dipaksa hitam di DxSetCellValueBlackFont.
            cell.Font.Bold = false;
            cell.Alignment.Horizontal = DevExpress.Spreadsheet.SpreadsheetHorizontalAlignment.Center;
            cell.Alignment.Vertical = DevExpress.Spreadsheet.SpreadsheetVerticalAlignment.Bottom;
        }

        // Rata kiri-tengah (request Hendra 2026-08-26) - dipakai tabel "LEMBAR
        // MENGETAHUI" (WriteLembarMengetahuiSection) yang defaultnya center-align
        // dari template, bikin teks pendek (kode divisi/nama) terasa "mengambang"
        // di tengah kolom lebar.
        private void DxSetCellValueBlackFontLeftMiddleAligned(DevExpress.Spreadsheet.Cell cell, string value)
        {
            DxSetCellValueBlackFont(cell, value);
            cell.Alignment.Horizontal = DevExpress.Spreadsheet.SpreadsheetHorizontalAlignment.Left;
            cell.Alignment.Vertical = DevExpress.Spreadsheet.SpreadsheetVerticalAlignment.Center;
        }

        /// <summary>
        /// Tulis nama ke cell - kalau cell itu ternyata bagian dari merged region
        /// (mis. template SOP yang menggabungkan area gambar tanda tangan + baris
        /// nama jadi satu kotak), value dialihkan ke cell anchor (kiri-atas) merge
        /// itu supaya benar-benar tampil, bukan ke cell non-anchor yang tidak
        /// dirender. Kalau belum ada merge, buat baru selebar mergeCellCol.
        /// </summary>
        private void DxWriteNameCellSafely(DevExpress.Spreadsheet.Worksheet sheet, int row, int col, int mergeCellCol, string value)
        {
            IList<DevExpress.Spreadsheet.CellRange> existingMerges = sheet[row, col].GetMergedRanges();
            if (existingMerges.Count > 0)
            {
                DevExpress.Spreadsheet.CellRange existing = existingMerges[0];
                DxSetCellValueBlackFontBottomAligned(sheet[existing.TopRowIndex, existing.LeftColumnIndex], value);
            }
            else
            {
                DxSetCellValueBlackFontBottomAligned(sheet[row, col], value);
                sheet.MergeCells(sheet.Range.FromLTRB(col, row, col + mergeCellCol - 1, row));
            }
        }

        // Kotak tanda tangan "Disetujui/Diperiksa/Dibuat" - khusus SPR (SIPOCOR),
        // sheet Cover saja (request Hendra 2026-08-21). TB_M_EXCEL_TEMPLATE TIDAK
        // punya baris DIGITAL_SIGN sama sekali untuk DOCUMENT_ID=15 (dicek
        // langsung - kosong), dan mekanisme generik di atas (type2Templates)
        // cuma bisa mewakili 3 kotak (satu approver per kotak) - template SPR ini
        // justru punya 4 KOTAK TANDA TANGAN TERPISAH meski "Dibuat" cuma SATU
        // judul yang membentang di atas 2 di antaranya (Dept. Head & Staff/Section
        // Head sama-sama "Dibuat", bukan approver tunggal). Makanya dibuat method
        // khusus di sini, sama seperti WriteRelatedDivisionSection di bawah -
        // bukan diperluas ke mekanisme generik (risiko regresi ke 8 tipe dokumen
        // lain yang sudah pakai itu).
        //
        // Koordinat sel awalnya ditemukan dengan membuka langsung file yang
        // di-upload user (tes-134317479101019728.xlsx, DOCUMENT_TRANSACTION_ID=4) -
        // baris 37-39 (0-indexed 36-38) = area gambar tanda tangan, kolom N/P/R/T
        //
        // Diturunkan ke row 36-38 (0-indexed 35-37) - 2026-08-27, user geser satu
        // baris di template (SPR.xlsx) sehingga baris kosong area tanda tangan
        // sekarang PERSIS di bawah header "Disetujui/Diperiksa/Dibuat" (row 36),
        // bukan lagi row 37 - koordinat lama nyerempet 1 baris ke bawah, tumpang
        // tindih baris caption "EO/Div. Head/Dept. Head/Staff-Section Head" (row 39),
        // gambar jadi kelihatan naik/kurang rapi dalam kotaknya. Kalau template
        // digeser lagi, ukur ulang jarak baris kosong antara header dan caption ini,
        // jangan asumsikan sama - lihat juga catatan serupa di
        // WriteRelatedDivisionSection.tableEndRow.
        // (0-indexed 13/15/17/19) = Disetujui(EO)/Diperiksa(Div.Head)/
        // Dibuat-kiri(Dept.Head)/Dibuat-kanan(Staff/Section Head, diisi PEMBUAT
        // DOKUMEN, bukan dari TB_R_APPROVAL_D). Label jabatan (EO/Div. Head/
        // Dept. Head/Staff/Section Head) di baris 40 sudah teks statis di
        // template, TIDAK perlu ditulis ulang - method ini cuma menempelkan
        // gambar tanda tangan. Dicocokkan lewat POSITION_ID approver (5=EO,
        // 4=Div.Head, 3=Dept.Head - lihat sp_UserPosition_InsertUpdate.sql),
        // BUKAN urutan WORKFLOW_SEQ tetap, supaya tetap benar walau urutan
        // approval berbeda-beda per Document Level. Kalau template SPR diganti
        // lagi, petakan ulang koordinat ini dari nol, jangan asumsikan sama.
        private void WriteSprSignatureSection(DevExpress.Spreadsheet.Worksheet sheet, IList<ApprovalDetail> approvalDetails, User creator, string webRootPath)
        {
            const int signRowStart = 35; // Excel row 36 (0-indexed) - atas area gambar tanda tangan
            const int signRowSpan = 3;   // baris 36-38 (baris 39 = caption EO/Div.Head/dst, jangan ikut)
            const int signColSpan = 2;   // tiap kotak selebar 2 kolom (mis. N:O)
            const int disetujuiCol = 13; // Excel col N - EO
            const int diperiksaCol = 15; // Excel col P - Div. Head
            const int dibuatDeptCol = 17; // Excel col R - Dept. Head (bagian kiri "Dibuat")
            const int dibuatStaffCol = 19; // Excel col T - Staff/Section Head/pembuat (bagian kanan "Dibuat")

            void PlaceSignature(int col, string signaturePath)
            {
                if (string.IsNullOrEmpty(signaturePath)) return;
                string fullSignPath = webRootPath + signaturePath;
                if (System.IO.File.Exists(fullSignPath))
                {
                    DxAddPicture(sheet, fullSignPath, signRowStart, col, signColSpan, signRowSpan);
                }
            }

            ApprovalDetail FindApprovedByPosition(int positionId) => approvalDetails.FirstOrDefault(x =>
            {
                if (!"1".Equals(x.STATUS)) return false; // tanda tangan cuma tampil kalau sudah Approved
                User approverUser = UserRepo.Instance.GetByKey(new User { USERNAME = x.APPROVER }, db);
                return approverUser != null && approverUser.POSITION_ID == positionId;
            });

            ApprovalDetail eoApproval = FindApprovedByPosition(5);
            if (eoApproval != null)
            {
                User eoUser = UserRepo.Instance.GetByKey(new User { USERNAME = eoApproval.APPROVER }, db);
                PlaceSignature(disetujuiCol, eoUser?.SIGNATURE_PATH);
            }

            ApprovalDetail divHeadApproval = FindApprovedByPosition(4);
            if (divHeadApproval != null)
            {
                User divHeadUser = UserRepo.Instance.GetByKey(new User { USERNAME = divHeadApproval.APPROVER }, db);
                PlaceSignature(diperiksaCol, divHeadUser?.SIGNATURE_PATH);
            }

            ApprovalDetail deptHeadApproval = FindApprovedByPosition(3);
            if (deptHeadApproval != null)
            {
                User deptHeadUser = UserRepo.Instance.GetByKey(new User { USERNAME = deptHeadApproval.APPROVER }, db);
                PlaceSignature(dibuatDeptCol, deptHeadUser?.SIGNATURE_PATH);
            }

            // Pembuat dokumen SELALU tanda tangan tanpa syarat status approval -
            // box "Staff/Section Head" ini bukan approver TB_R_APPROVAL_D sama
            // sekali (sama seperti WriteCreatorIntoBox() di mekanisme generik di
            // atas).
            PlaceSignature(dibuatStaffCol, creator?.SIGNATURE_PATH);
        }

        // Fitur "Divisi Terkait" - khusus SPR (SIPOCOR) Level 2 (request Hendra
        // 2026-08-20). Menulis baris marker (satu kolom per divisi - kolomnya
        // dibaca langsung dari header row H13 di file, TIDAK di-hardcode, supaya
        // otomatis ikut kalau template diedit lagi) dan tabel "Mengetahui Divisi
        // Terkait" (baris dinamis, satu per divisi terkait yang dipilih creator)
        // di sheet Cover. Koordinat baris/kolom tabel & header MASIH hardcode ke
        // layout SPR2.xlsx saat ini (row 13/14 = header/marker, row 18-29 =
        // tabel Mengetahui, kolom H/J/L) - kalau template diganti lagi, petakan
        // ulang koordinat ini dari nol, jangan asumsikan sama.
        //
        // tableEndRow diturunkan dari 32 ke 27 (2026-08-27, dicek ulang setelah
        // user geser satu baris di template) - baris "Remarks:" sekarang mulai
        // di row 29 (0-indexed), jadi slot terakhir yang aman buat entry 2-baris
        // adalah start di row 27 (berakhir row 28, masih sebelum Remarks). Kapasitas
        // tabel jadi ~6 divisi (dari sebelumnya ~8) - lebih dari cukup untuk kasus
        // nyata, tapi kalau template digeser lagi, ukur ulang jarak ke "Remarks:".
        private void WriteRelatedDivisionSection(DevExpress.Spreadsheet.Worksheet sheet, DocumentMaintenance documentMaintenance, string webRootPath)
        {
            const int headerRow = 12;      // Excel row 13 (0-indexed)
            const int markerRow = 13;      // Excel row 14
            const int firstDivisionCol = 1; // Excel col B
            const int tableStartRow = 17;  // Excel row 18
            const int tableEndRow = 27;    // Excel row 28 (baris awal terakhir yang aman - berakhir row 29, masih sebelum "Remarks:")
            const int divisiCol = 7;       // Excel col H
            const int kepalaDivisiCol = 9; // Excel col J
            const int tandaTanganCol = 11; // Excel col L

            // DIVISION_ROLE per divisi (request Hendra 2026-08-20) - Main PIC
            // sekarang dipilih manual & disimpan di tabel ini sendiri (boleh lebih
            // dari satu divisi), TIDAK LAGI otomatis diturunkan dari
            // TB_R_DOCUMENT.DIVISION. ACKNOWLEDGED_FLAG dibaca dari kolom yang
            // sama seperti sebelumnya (acknowledgment sudah lepas dari
            // TB_R_APPROVAL_D, lihat DocumentMaintenance_RelatedDivision_Role_Migration.sql).
            Dictionary<string, string> roleByDivision = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, bool> acknowledgedByDivision = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                if (cmd.Connection.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
                cmd.CommandText = "SELECT DIVISION_CODE, DIVISION_ROLE, ACKNOWLEDGED_FLAG FROM TB_R_DOCUMENT_RELATED_DIVISION WHERE DOCUMENT_TRANSACTION_ID = @id";
                var idParam = cmd.CreateParameter();
                idParam.ParameterName = "@id";
                idParam.Value = documentMaintenance.DOCUMENT_TRANSACTION_ID;
                cmd.Parameters.Add(idParam);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string divisionCode = reader.GetString(0);
                        roleByDivision[divisionCode] = reader.GetString(1);
                        acknowledgedByDivision[divisionCode] = reader.GetBoolean(2);
                    }
                }
            }

            // Baris marker (◎ Main PIC / ○ Related / - none) - kolom dibaca
            // langsung dari header row, berhenti di kolom kosong pertama.
            // Peran "Note Related" (●) sempat ada lalu dihapus lagi (request
            // Hendra 2026-08-20 revisi ke-3) - kalau DIVISION_ROLE lama masih
            // menyimpan nilai itu (data historis), tetap jatuh ke cabang
            // "○" di bawah, bukan error.
            for (int col = firstDivisionCol; ; col++)
            {
                string divisionCode = sheet[headerRow, col].Value.ToString().Trim();
                if (string.IsNullOrEmpty(divisionCode)) break;

                string marker = "-";
                if (roleByDivision.TryGetValue(divisionCode, out string role))
                {
                    marker = role.Equals("MAIN_PIC", StringComparison.OrdinalIgnoreCase) ? "◎" : "○"; // RELATED
                }

                DxSetCellValueBlackFont(sheet[markerRow, col], marker);
            }

            // Tabel "Mengetahui Divisi Terkait" - satu baris per divisi yang punya
            // role apapun (Main PIC MAUPUN Related) ikut tanda tangan Mengetahui.
            // Sebelumnya cuma role RELATED yang dimasukkan (Main PIC dianggap
            // cukup terwakili di baris marker ◎) - diubah atas request user
            // 2026-08-27: kolom tanda tangan tetap harus terisi untuk Main PIC
            // juga, bukan cuma Related.
            List<string> relatedDivisions = roleByDivision.Keys.ToList();

            // Gambar tanda tangan di-spanning 2 baris template (bukan 1) supaya cukup
            // tinggi buat kebaca (request Hendra 2026-08-21) - baris tunggal (~20pt,
            // dirancang buat teks biasa) bikin gambar kegepeng nyaris tak kebaca,
            // dibanding kotak Disetujui/Diperiksa/Dibuat yang tingginya ~75pt lewat 3
            // baris. SENGAJA pakai cara spanning-baris (bukan sheet.Rows[i].Height=X) -
            // sudah dicoba menaikkan Height satu baris secara eksplisit (bahkan sampai
            // nilai ekstrem 100pt) dan TIDAK berpengaruh sama sekali ke hasil render
            // PDF (diuji 2026-08-21) - entah kenapa DevExpress mengabaikannya di jalur
            // export ini. Konsekuensi: kapasitas tabel berkurang jadi separuh (~8
            // divisi terkait, dari sebelumnya ~16) - lebih dari cukup untuk kasus nyata.
            const int signRowSpan = 2;

            int tableRow = tableStartRow;
            foreach (string divisionCode in relatedDivisions)
            {
                if (tableRow > tableEndRow) break; // jangan overflow ke area di luar tabel

                string headUsername = null, headFullName = null, headSignaturePath = null;
                using (var cmd = db.Database.GetDbConnection().CreateCommand())
                {
                    if (cmd.Connection.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
                    cmd.CommandText = "SELECT TOP 1 U.USERNAME, U.FULL_NAME, U.SIGNATURE_PATH " +
                        "FROM TB_M_USER_POS UP JOIN TB_M_USER U ON UP.USERNAME = U.USERNAME " +
                        "WHERE UP.POSITION_ID = 4 AND UP.DIVISION = @div";
                    var divParam = cmd.CreateParameter();
                    divParam.ParameterName = "@div";
                    divParam.Value = divisionCode;
                    cmd.Parameters.Add(divParam);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            headUsername = reader.GetString(0);
                            headFullName = reader.IsDBNull(1) ? null : reader.GetString(1);
                            headSignaturePath = reader.IsDBNull(2) ? null : reader.GetString(2);
                        }
                    }
                }

                DxSetCellValueBlackFont(sheet[tableRow, divisiCol], divisionCode);

                if (headUsername != null)
                {
                    DxSetCellValueBlackFont(sheet[tableRow, kepalaDivisiCol], headFullName);

                    bool acknowledged = acknowledgedByDivision.TryGetValue(divisionCode, out bool ackFlag) && ackFlag;

                    if (acknowledged && !string.IsNullOrEmpty(headSignaturePath))
                    {
                        string signPath = webRootPath + headSignaturePath;
                        if (System.IO.File.Exists(signPath))
                        {
                            DxAddPicture(sheet, signPath, tableRow, tandaTanganCol, 2, signRowSpan);
                        }
                    }
                }
                else
                {
                    DxSetCellValueBlackFont(sheet[tableRow, kepalaDivisiCol], "(Div. Head not assigned)");
                }

                tableRow += signRowSpan;
            }
        }

        // Fitur "Divisi Terkait" - sheet "LEMBAR MENGETAHUI" pada template PDM
        // (Pedoman, DOCUMENT_ID=1) & PRO (Prosedur, DOCUMENT_ID=2), Level 2
        // (request Hendra 2026-08-26 - kotak tanda tangan divisi yang mengetahui
        // belum pernah terisi otomatis dari aplikasi). Beda dari
        // WriteRelatedDivisionSection (SPR) di atas: sheet ini TIDAK punya baris
        // marker Main PIC/Related terpisah, cuma satu tabel lurus
        // "No. | Divisi | Nama | Tanda Tangan" (dicek langsung dari file
        // tes_dokumen_prosedur_1-134321880018518093.xlsx, DOCUMENT_TRANSACTION_ID=7),
        // jadi SEMUA divisi terkait ditulis di sini apapun DIVISION_ROLE-nya
        // (beda dari SPR yang cuma menulis role RELATED ke tabelnya karena Main
        // PIC sudah terwakili di baris marker terpisah).
        //
        // Koordinat baris/kolom (0-indexed) ditemukan dari file yang sama:
        // header "No./Divisi/Nama/Tanda Tangan" di row 16 (statis, tidak perlu
        // ditulis ulang), tabel mulai row 18, tiap baris data di-merge 2 baris
        // (mis. D18:E19, F18:O19, P18:Z19, AA18:AL19) sampai baris aman terakhir
        // row 54 - kalau template diganti lagi, petakan ulang koordinat ini dari
        // nol, jangan asumsikan sama.
        private void WriteLembarMengetahuiSection(DevExpress.Spreadsheet.Worksheet sheet, DocumentMaintenance documentMaintenance, string webRootPath)
        {
            const int noCol = 3;           // Excel col D
            const int divisiCol = 5;       // Excel col F (merge F:O)
            const int namaCol = 15;        // Excel col P (merge P:Z)
            const int tandaTanganCol = 26; // Excel col AA (merge AA:AL)
            const int tableStartRow = 17;  // Excel row 18
            const int tableEndRow = 53;    // Excel row 54 (baris terakhir yang aman di template)
            const int rowSpan = 2;         // tiap baris tabel di-merge 2 baris

            // Kolom "Divisi" cukup kode saja (mis. "CED", bukan "CED - Corporate &
            // External Affairs Div") supaya tidak kepanjangan/terpotong di kolom
            // tabel yang sempit (request Hendra 2026-08-26, dicek langsung dari
            // hasil cetak - nama lengkap ke-truncate).
            var relatedDivisions = new List<(string Code, bool Acknowledged, string HeadFullName, string HeadSignaturePath)>();
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                if (cmd.Connection.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
                // Kepala Divisi dicari lewat POSITION_ID=4 (Div. Head), pola sama
                // persis seperti WriteRelatedDivisionSection di atas.
                cmd.CommandText = "SELECT RD.DIVISION_CODE, RD.ACKNOWLEDGED_FLAG, U.FULL_NAME, U.SIGNATURE_PATH " +
                    "FROM TB_R_DOCUMENT_RELATED_DIVISION RD " +
                    "OUTER APPLY (SELECT TOP 1 UP.USERNAME FROM TB_M_USER_POS UP WHERE UP.POSITION_ID = 4 AND UP.DIVISION = RD.DIVISION_CODE) DH " +
                    "LEFT JOIN TB_M_USER U ON U.USERNAME = DH.USERNAME " +
                    "WHERE RD.DOCUMENT_TRANSACTION_ID = @id ORDER BY RD.DIVISION_CODE";
                var idParam = cmd.CreateParameter();
                idParam.ParameterName = "@id";
                idParam.Value = documentMaintenance.DOCUMENT_TRANSACTION_ID;
                cmd.Parameters.Add(idParam);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        relatedDivisions.Add((
                            reader.GetString(0),
                            !reader.IsDBNull(1) && reader.GetBoolean(1),
                            reader.IsDBNull(2) ? null : reader.GetString(2),
                            reader.IsDBNull(3) ? null : reader.GetString(3)));
                    }
                }
            }

            int tableRow = tableStartRow;
            int no = 1;
            foreach (var division in relatedDivisions)
            {
                if (tableRow > tableEndRow) break; // jangan overflow ke area di luar tabel

                DxSetCellValueBlackFontLeftMiddleAligned(sheet[tableRow, noCol], (no++).ToString());
                DxSetCellValueBlackFontLeftMiddleAligned(sheet[tableRow, divisiCol], division.Code);

                if (division.HeadFullName != null)
                {
                    DxSetCellValueBlackFontLeftMiddleAligned(sheet[tableRow, namaCol], division.HeadFullName);

                    if (division.Acknowledged && !string.IsNullOrEmpty(division.HeadSignaturePath))
                    {
                        string signPath = webRootPath + division.HeadSignaturePath;
                        if (System.IO.File.Exists(signPath))
                        {
                            // Kolom Tanda Tangan sudah di-merge di template (mis.
                            // AA18:AL19) - pakai merge yang sudah ada sebagai target
                            // gambar (pola sama seperti marker-based DxAddPicture di
                            // atas) daripada menghitung ulang lebar kolom manual.
                            IList<DevExpress.Spreadsheet.CellRange> signMerges = sheet[tableRow, tandaTanganCol].GetMergedRanges();
                            DevExpress.Spreadsheet.CellRange signRange = signMerges.Count > 0
                                ? signMerges[0]
                                : sheet.Range.FromLTRB(tandaTanganCol, tableRow, tandaTanganCol + 2, tableRow + rowSpan - 1);
                            DxAddPicture(sheet, signPath, signRange);
                        }
                    }
                }
                else
                {
                    DxSetCellValueBlackFontLeftMiddleAligned(sheet[tableRow, namaCol], "(Div. Head not assigned)");
                }

                tableRow += rowSpan;
            }
        }

        private void DxAddPicture(DevExpress.Spreadsheet.Worksheet sheet, string imagePath, int rowStart, int colStart, int mergeCellCol, int mergeCellRow)
        {
            DevExpress.Spreadsheet.CellRange targetRange = sheet.Range.FromLTRB(colStart, rowStart, colStart + mergeCellCol - 1, rowStart + mergeCellRow - 1);
            DxAddPicture(sheet, imagePath, targetRange);
        }

        // Overload dipakai jalur marker (Named Range) - area gambar sudah didapat
        // langsung dari range marker-nya, tidak perlu dihitung dari rowStart/mergeCell.
        private void DxAddPicture(DevExpress.Spreadsheet.Worksheet sheet, string imagePath, DevExpress.Spreadsheet.CellRange targetRange)
        {
            sheet.Pictures.AddPicture(DevExpress.Spreadsheet.SpreadsheetImageSource.FromFile(imagePath), targetRange, false);
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
                            // Related Division "Mengetahui" (SPR/SIPOCOR Level 2) sudah
                            // TIDAK bagian dari chain approval ini lagi (request Hendra
                            // 2026-08-20) - approval asli selesai di sini terlepas dari
                            // acknowledgment. Tapi dokumen baru benar-benar Approved (1)
                            // kalau semua Related Division juga sudah Mengetahui; kalau
                            // masih ada yang pending, dokumen masuk status antara
                            // "Waiting Acknowledgment" (6) dan efek-samping "selesai
                            // approval" (obsolete-control, cache PDF, email) ditunda
                            // sampai acknowledgment terakhir masuk lewat
                            // AcknowledgeRelatedDivisionAsync.
                            bool hasPendingAck = documentMaintenanceRepo.CountPendingRelatedDivisionAck((int)documentMaintenance.DOCUMENT_TRANSACTION_ID, db) > 0;

                            documentMaintenance.STATUS = hasPendingAck ? "6" : "1";
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
                            else if (hasPendingAck)
                            {
                                logRepo.WriteLog(processid, "1", "INF", "Approval chain completed; waiting on Related Division acknowledgment(s).", location, GetLoginUsername(), db);
                            }
                            else
                            {
                                FinalizeApproval(documentMaintenance, mode, remark, webRootPath);
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

        // Efek-samping "selesai approval" (obsolete-control revisi lama, hapus
        // cache PDF, email) - diekstrak dari ApproveRejectAsync (request Hendra
        // 2026-08-20) supaya bisa dipakai juga dari AcknowledgeRelatedDivisionAsync
        // saat acknowledgment TERAKHIR yang menaikkan dokumen ke Approved (dokumen
        // sempat "Waiting Acknowledgment" menunggu Related Division, bukan
        // langsung Approved di titik approval asli selesai).
        private void FinalizeApproval(DocumentMaintenance documentMaintenance, string mode, string remark, string webRootPath)
        {
            // loginUser sengaja null di kedua Search di bawah - approver/Div Head
            // yang memproses ini belum tentu satu divisi/department dengan
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
        }

        // Panel "Related Division Acknowledgment" di modal Approval List - list
        // kosong (data.length == 0) berarti dokumen ini bukan SPR Level 2 / tidak
        // punya Related Division, JS-nya yang menyembunyikan section (request
        // Hendra 2026-08-20).
        public JsonResult GetRelatedDivisionAckStatus(int documentTransactionId)
        {
            try
            {
                IList<DocumentRelatedDivision> data = documentMaintenanceRepo.GetRelatedDivisionAckStatus(documentTransactionId, db);
                return Json(new { status = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Aksi "Mengetahui" Div Head - TERPISAH dari ApproveRejectAsync, tidak ada
        // opsi reject (lihat sp_DocumentMaintenance_AcknowledgeRelatedDivision utk
        // alasan lengkapnya). Kalau ini acknowledgment terakhir DAN approval
        // aslinya sudah selesai lebih dulu (STATUS=6), naikkan ke Approved (1) dan
        // jalankan efek-samping "selesai approval" yang sama seperti approval biasa.
        public JsonResult AcknowledgeRelatedDivisionAsync(int documentTransactionId, string divisionCode, string remark)
        {
            string webRootPath = Environment.WebRootPath;
            string location = "DocumentMaintenance/AcknowledgeRelatedDivision";

            LogHeader logH = new LogHeader { MODULE = "Document Maintenance", FUNCTION = "Acknowledge Related Division" };
            long processid = logRepo.StartLog(logH, location, GetLoginUsername(), db);

            try
            {
                db.Database.BeginTransaction();

                DBResult result = documentMaintenanceRepo.AcknowledgeRelatedDivision(documentTransactionId, divisionCode, GetLoginUsername(), db, out bool promotedToApproved);

                if (!result.status)
                {
                    db.Database.RollbackTransaction();
                    logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);

                    return Json(new { status = false, message = result.message });
                }

                if (promotedToApproved)
                {
                    DocumentMaintenance documentMaintenance = documentMaintenanceRepo
                        .Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentTransactionId }, null, db, 1, 1)
                        .FirstOrDefault();

                    if (documentMaintenance != null)
                    {
                        documentMaintenance.STATUS = "1";
                        DBResult statusResult = documentMaintenanceRepo.UpdateStatus(documentMaintenance, GetLoginUsername(), db);
                        if (!statusResult.status)
                        {
                            db.Database.RollbackTransaction();
                            logRepo.WriteLog(processid, "3", "ERR", statusResult.message, location, GetLoginUsername(), db);

                            return Json(new { status = false, message = statusResult.message });
                        }

                        FinalizeApproval(documentMaintenance, "approve", remark, webRootPath);
                    }
                }

                db.Database.CommitTransaction();
                logRepo.WriteLog(processid, "2", "INF", result.message, location, GetLoginUsername(), db);

                return Json(new { status = true, message = result.message, promotedToApproved = promotedToApproved });
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

                // Dulu pakai approvalDetail.FILE_PATH (foto profil approver, ikut di-join
                // dari TB_M_USER.FILE_PATH oleh sp_Approval_GetDetail) - salah, itu bukan
                // tanda tangan. Ambil SIGNATURE_PATH dari user-nya langsung, sama seperti
                // pola yang sudah benar di GeneratePengesahanPdf (bug ditemukan 2026-08-12).
                User approverUser = UserRepo.Instance.GetByKey(new User { USERNAME = approvalDetail.APPROVER }, db);
                string anotherFullPath = approverUser != null && !string.IsNullOrEmpty(approverUser.SIGNATURE_PATH)
                    ? webRootPath + approverUser.SIGNATURE_PATH
                    : null;

                if (anotherFullPath != null && System.IO.File.Exists(anotherFullPath))
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
                // Konversi Excel (xlsx/xls) -> PDF pakai DevExpress Spreadsheet Document API,
                // gantikan LibreOffice headless (soffice.exe --convert-to pdf).
                // Nama file output tetap sama seperti sebelumnya: <nama file input>.pdf,
                // ditaruh di folder outputPath (perilaku sama seperti versi LibreOffice).
                string outputPdfPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(outputPath),
                    System.IO.Path.GetFileNameWithoutExtension(inputPath) + ".pdf");

                using (var workbook = new DevExpress.Spreadsheet.Workbook())
                {
                    workbook.LoadDocument(inputPath);
                    workbook.ExportToPdf(outputPdfPath);
                }

                // DevExpress can silently produce a 0-page PDF for some source files
                // (seen with a complex multi-sheet SharePoint-synced workbook, Aug 2026)
                // without throwing - catch it here instead of caching/serving a blank
                // PDF that just shows as a black screen in the viewer.
                using (PdfDocument checkDoc = PdfReader.Open(outputPdfPath, PdfDocumentOpenMode.InformationOnly))
                {
                    if (checkDoc.PageCount == 0)
                    {
                        System.IO.File.Delete(outputPdfPath);
                        return new DBResult(false, "PDF conversion produced no pages. The source file's print area or sheet setup may not be supported - try re-saving it from Excel and upload again.");
                    }
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

                // Obsolete-control (Aug 2026): sama seperti ViewAttachment - dokumen yang
                // sudah disupersede tidak lagi punya baris live di TB_R_DOCUMENT, jadi kalau
                // tidak ketemu di sana berarti ini dokumen lama/obsolete. Sebelumnya print
                // tidak pernah cek ini, jadi dokumen obsolete bisa dicetak tanpa watermark
                // OBSOLETE sama sekali. loginUser sengaja null - ini cek eksistensi murni.
                DocumentMaintenance currentDocument = documentMaintenanceRepo
                    .Search(new DocumentMaintenance { DOCUMENT_TRANSACTION_ID = documentMaintenance.DOCUMENT_TRANSACTION_ID }, null, db, 1, 1)
                    .FirstOrDefault();
                bool isObsolete = currentDocument == null;

                // Stempel gambar berdasarkan status dokumen, bukan siapa yang login - lihat
                // ViewAttachment untuk penjelasan lengkap aturannya (sama persis di sini,
                // termasuk CONTROLLED COPY cuma untuk type 3/end user - request Hendra
                // 2026-08-14).
                bool isFullyAcknowledged = !isObsolete && currentDocument.STATUS == "5";
                bool isMasterStamped = !isObsolete && (isFullyAcknowledged || IsReceivedByQms(currentDocument.DOCUMENT_TRANSACTION_ID));
                bool isEndUserView = type == "3";

                string masterStampPath = null;
                string controlledCopyStampPath = null;
                string obsoleteStampPath = null;

                if (isObsolete)
                {
                    obsoleteStampPath = webRootPath + "/images/cap_obsolete.png";
                }
                else
                {
                    if (isMasterStamped) masterStampPath = webRootPath + "/images/cap_master.png";
                    if (isFullyAcknowledged && isEndUserView) controlledCopyStampPath = webRootPath + "/images/cap_controlledcopy.png";
                }

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

                    // outputFullPath sudah salinan sekali-pakai (bukan file asli/cache
                    // bersama), jadi aman di-watermark/stempel langsung di tempat.
                    if (masterStampPath != null || controlledCopyStampPath != null || obsoleteStampPath != null)
                        AddImageStamps(outputFullPath, outputFullPath, masterStampPath, controlledCopyStampPath, obsoleteStampPath);

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
                        pengesahanModifiedfileNames = cachedPdfRelative;

                        if (masterStampPath != null || controlledCopyStampPath != null || obsoleteStampPath != null)
                        {
                            // Jangan watermark/stempel file cache-nya langsung - file itu
                            // dipakai bersama oleh request lain yang belum tentu butuh
                            // stempel yang sama. Salin dulu ke path sendiri, sama seperti
                            // pola di ViewAttachment.
                            string watermarkedRelative = cachedPdfRelative.Replace(".pdf", "_print_wm.pdf");
                            string watermarkedFullPath = webRootPath + watermarkedRelative;
                            System.IO.File.Copy(cachedPdfFullPath, watermarkedFullPath, overwrite: true);
                            AddImageStamps(watermarkedFullPath, watermarkedFullPath, masterStampPath, controlledCopyStampPath, obsoleteStampPath);
                            pengesahanModifiedfileNames = watermarkedRelative;
                        }
                    }
                    else
                    {
                        // Sama seperti ViewAttachment - tulis field/tanda tangan langsung
                        // lewat DevExpress dan export dari workbook yang sama, tanpa
                        // round-trip lewat NPOI (lihat GeneratePengesahanPdf).
                        string pengesahanPdfRelative = "/Upload/ATTACHMENT/DOCUMENT_TEMP/TEMP_"
                            + System.IO.Path.GetFileNameWithoutExtension(fileName) + ".pdf";
                        string pengesahanPdfFullPath = webRootPath + pengesahanPdfRelative;

                        result = GeneratePengesahanPdf(webRootPath, documentMaintenance.FILE_PATH, documentMaintenance, pengesahanPdfFullPath);
                        if (!result.status)
                            return Json(new { status = false, message = result.message });

                        pengesahanModifiedfileNames = pengesahanPdfRelative;

                        // File hasil konversi ini baru & sekali-pakai, aman di-watermark/stempel langsung.
                        if (masterStampPath != null || controlledCopyStampPath != null || obsoleteStampPath != null)
                            AddImageStamps(pengesahanPdfFullPath, pengesahanPdfFullPath, masterStampPath, controlledCopyStampPath, obsoleteStampPath);
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

                // Cache-buster - lihat komentar di ViewAttachment untuk alasannya (nama file
                // hasil selalu sama meski isinya di-regenerate tiap request).
                return Json(new { status = true, data = finalRelativePath + "?v=" + DateTime.UtcNow.Ticks });
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

    }



    
}
