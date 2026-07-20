using Amazon.S3;
using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class LogMonitoringController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        public LogMonitoringController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        public LogMonitoringRepo logMonitoringRepo = LogMonitoringRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/LogMonitoring/Index"))
                {
                    return StatusCode(403);
                }
            }

            ViewData["Title"] = "Activity Log";

            return View();
        }

        public JsonResult GetByKey(LogHeader data)
        {
            try
            {
                LogHeader result = logMonitoringRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult Search(LogHeader data)
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
                var SupplierMasterData = logMonitoringRepo.Search(data, db, pageNumber, pageSize);
                var dataCount = logMonitoringRepo.Search(data, db, null, null).Count;
                recordsTotal = dataCount;
                var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = SupplierMasterData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult SearchDetail(int PROCESS_ID)
        {
            LogDetail dLogDetail = new LogDetail();
            try
            {
                dLogDetail.PROCESS_ID = PROCESS_ID;
                var result = logMonitoringRepo.SearchDetail(dLogDetail, db, null, null);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetListModule(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                LogHeader oLogHeader = new LogHeader();
                if (q != null)
                    oLogHeader.MODULE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<LogHeader> dataList = logMonitoringRepo.GetListModule(oLogHeader, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.MODULE, id = data.MODULE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetListFunction(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                LogHeader oLogHeader = new LogHeader();
                if (q != null)
                    oLogHeader.FUNCTION = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<LogHeader> dataList = logMonitoringRepo.GetListFunction(oLogHeader, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.FUNCTION, id = data.FUNCTION });
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
