using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class WorkflowDocController : BaseController
    {
        DBContext db;
        public WorkflowDocController(DBContext db)
        {
            this.db = db;
        }

        private WorkflowDocRepo workflowDocRepo         = WorkflowDocRepo.Instance;
        private PositionMasterRepo positionMasterRepo   = PositionMasterRepo.Instance;

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/WorkflowDoc/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"]    = HttpContext.Session.GetString("functionList").Contains("WORKFLOWDOC-ADD");
            ViewData["Edit"]   = HttpContext.Session.GetString("functionList").Contains("WORKFLOWDOC-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("WORKFLOWDOC-DELETE");

            ViewData["Title"] = "Approval Workflow Setup";

            return View();
        }

        public ActionResult Search(WorkflowDocArr data, bool initialMode)
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
                    var dataList = workflowDocRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = workflowDocRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = dataList };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult GetByCode(WorkflowDoc data, PositionMaster data2 )
        {
            try
            {
                IList<WorkflowDoc> result = workflowDocRepo.GetByCode(data, db);
                //get position name 
                PositionMaster result2    = positionMasterRepo.GetByKey(data2, db);
                return Json(new { status = true, data = result, data2 = result2 });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult AddEdit([FromBody] List<WorkflowDoc> data)
        {
            DBResult result = null;
            DBResult resultHeader = null;
            try
            {
                if (data != null && data.Count > 0)
                {
                    WorkflowDoc temp     = new WorkflowDoc();
                    temp.WORKFLOW_DOC_ID = data[0].WORKFLOW_DOC_ID;

                    if (workflowDocRepo.GetByName(temp, db).Count > 0)
                    {
                        result = workflowDocRepo.Delete(temp, GetLoginUsername(), db);
                    }

                    temp.DOCUMENT_LEVEL = data[0].DOCUMENT_LEVEL;
                    temp.CREATOR_LEVEL = data[0].CREATOR_LEVEL;
                    resultHeader = workflowDocRepo.InsertHeader(temp, GetLoginUsername(), db);
                    if (resultHeader.status)
                    {
                        foreach (var workflow in data)
                        {
                            workflow.WORKFLOW_DOC_ID = resultHeader.returnId;
                            result = workflowDocRepo.InsertDetail(workflow, GetLoginUsername(), db);
                        }
                    }
                    else
                    {
                        return Json(resultHeader);
                    }
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(WorkflowDoc data)
        {
            DBResult result = null;
            try
            {
                result = workflowDocRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
        public JsonResult GetPositionName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                PositionMaster oWorkflow = new PositionMaster();
                if (q != null)
                    oWorkflow.POSITION_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<PositionMaster> dataList = positionMasterRepo.Search(oWorkflow, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.POSITION_NAME, id = data.POSITION_ID?.ToString() });
                    //list.Add(new Select2() { text = data.POSITION_NAME, id = data.POSITION_ID });
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
