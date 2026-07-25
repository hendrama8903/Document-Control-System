using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    public class MSequenceController : BaseController
    {
        DBContext db;
        public MSequenceController(DBContext db)
        {
            this.db = db;
        }

        private MSequenceRepo mSequenceRepo = MSequenceRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/MSequence/Index"))
                {
                    return StatusCode(403);
                }
            }

            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("MSEQUENCE-EDIT");
            ViewData["Reset"] = HttpContext.Session.GetString("functionList").Contains("MSEQUENCE-RESET");

            ViewData["Title"] = "Document Numbering";

            return View();
        }

        public JsonResult GetByKey(MSequence data)
        {
            try
            {
                MSequence result = mSequenceRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(MSequence data, bool initialMode)
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
                    var listData = mSequenceRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = mSequenceRepo.Search(data, db, null, null).Count;
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

        public JsonResult Update(MSequence data, string OLD_SEQ_TYPE, string OLD_SEQ_CODE)
        {
            try
            {
                DBResult result = mSequenceRepo.Update(data, OLD_SEQ_TYPE, OLD_SEQ_CODE, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Reset(MSequence data)
        {
            if (!HttpContext.Session.GetString("functionList").Contains("MSEQUENCE-RESET"))
            {
                return Json(new { status = false, message = "You are not authorized to perform this action." });
            }

            try
            {
                DBResult result = mSequenceRepo.Reset(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
