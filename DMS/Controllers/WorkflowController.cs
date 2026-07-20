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
    public class WorkflowController : BaseController
    {
        DBContext db;
        public WorkflowController(DBContext db)
        {
            this.db = db;
        }

        private WorkflowRepo workflowRepo = WorkflowRepo.Instance;

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/Workflow/Index"))
                {
                    return StatusCode(403);
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("WORKFLOW-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("WORKFLOW-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("WORKFLOW-DELETE");

            ViewData["Title"] = "Workflow Master";

            return View();
        }

        public ActionResult Search(Workflow data, bool initialMode)
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
                    var dataList = workflowRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = workflowRepo.Search(data, db, null, null).Count;
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

        public JsonResult GetByCode(Workflow data)
        {
            try
            {
                IList<Workflow> result = workflowRepo.GetByCode(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetByName(Workflow data)
        {
            try
            {
                IList<Workflow> result = workflowRepo.GetByName(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult AddEdit([FromBody] List<Workflow> data)
        {
            DBResult result = null;
            DBResult resultHeader = null;
            try
            {
                if (data != null && data.Count > 0)
                {
                    Workflow temp = new Workflow();
                    temp.WORKFLOW_ID = data[0].WORKFLOW_ID;

                    if (workflowRepo.GetByName(temp, db).Count > 0)
                    {
                        result = workflowRepo.Delete(temp, GetLoginUsername(), db);
                    }

                    temp.WORKFLOW_CODE = data[0].WORKFLOW_CODE;
                    temp.WORKFLOW_NAME = data[0].WORKFLOW_NAME;
                    resultHeader = workflowRepo.InsertHeader(temp, GetLoginUsername(), db);
                    if (resultHeader.status)
                    {
                        foreach (var workflow in data)
                        {
                            workflow.WORKFLOW_ID = resultHeader.returnId;
                            result = workflowRepo.InsertDetail(workflow, GetLoginUsername(), db);
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

        public JsonResult Delete(Workflow data)
        {
            DBResult result = null;
            try
            {
                result = workflowRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetWorkflowName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Workflow oWorkflow = new Workflow();
                if (q != null)
                    oWorkflow.WORKFLOW_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Workflow> dataList = workflowRepo.Search(oWorkflow, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.WORKFLOW_NAME, id = data.WORKFLOW_NAME });
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
