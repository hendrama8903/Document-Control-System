using DMS.Common.Controllers;
using DMS.Hubs;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Hangfire;
using MDC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NPOI.POIFS.FileSystem;

namespace DMS.Controllers
{
    public class DocumentReportController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;

        public DocumentReportController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentReport/Index"))
                {
                    return StatusCode(403);
                }
            }

            ViewData["Title"] = "Document Report";

            return View();
        }

        public ActionResult Search(DocumentReport data, bool initialMode)
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
                    var listData = documentMaintenanceRepo.SearchReport(data, db, pageNumber, pageSize);
                    var dataCount = documentMaintenanceRepo.SearchReport(data, db, null, null).Count;
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
    }
}
