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
    public class DivisionMasterController : BaseController
    {
        DBContext db;
        public DivisionMasterController(DBContext db)
        {
            this.db = db;
        }

        private DivisionMasterRepo divisionMasterRepo = DivisionMasterRepo.Instance;
        private MSystemRepo mSystemRepo               = MSystemRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DivisionMaster/Index"))
                {
                    return StatusCode(403);
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("DIVISIONMASTER-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("DIVISIONMASTER-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("DIVISIONMASTER-DELETE");


            ViewData["Title"] = "Division Master";

            return View();
        }

        public JsonResult GetByKey(DivisionMaster data)
        {
            try
            {
                DivisionMaster result = divisionMasterRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(DivisionMaster data, bool initialMode)
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
                    var listData = divisionMasterRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = divisionMasterRepo.Search(data, db, null, null).Count;
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

        //public IActionResult SearchByDistribution(DocumentDistribution data)
        //{
        //    try
        //    {
        //        var listData = divisionMasterRepo.Search(new DivisionMaster(), db, null, null).OrderBy(x => x.DIVISION);
        //        var listDataDistribution = P4DMaintenanceRepo.Instance.SearchDocumentDistribution(data, db, null, null);
        //        foreach (var department in listData)
        //        {
        //            department.STATUS_EXIST = false;

        //            foreach (var distribution in listDataDistribution)
        //            {
        //                if (department.DEPARTMENT_ID.ToString() == distribution.DEPARTMENT_ID)
        //                {
        //                    department.STATUS_EXIST = true;
        //                }
        //            }
        //        }

        //        return Json(new { status = true, data = listData });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { status = false, message = ex.Message });
        //    }
        //}

        //public JsonResult Delete(DivisionMaster data)
        //{
        //    DBResult result = null;
        //    try
        //    {
        //        result = divisionMasterRepo.Delete(data, GetLoginUsername(), db);
        //        return Json(result);
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { status = false, message = ex.Message });
        //    }
        //}
        public JsonResult Delete(List<int> divisionIds)
        {
            try
            {
                DBResult result = divisionMasterRepo.Delete(divisionIds, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }


        public JsonResult AddEdit(string screenMode, DivisionMaster data)
        {
            DBResult result = null;
            try
            {
                if (screenMode == "ADD")
                {
                    result = divisionMasterRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = divisionMasterRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        //public JsonResult GetListDepartmentByDivision(string q, string pageLimit, string page, string param)
        //{
        //    try
        //    {
        //        AjaxResult ajaxResult = new AjaxResult();
        //        RepoResult repoResult = new RepoResult();

        //        DepartmentMaster oModel = new DepartmentMaster();
        //        if (q != "")
        //            oModel.DEPARTMENT_CODE = '*' + q + '*';

        //        //ambil division yg mana 
        //        if (param != "")
        //            oModel.DIVISION = param;

        //        int result, pageInt;

        //        if (int.TryParse(page, out result))
        //        {
        //            pageInt = int.Parse(page);
        //        }
        //        else
        //        {
        //            pageInt = 1;
        //        }
        //        //looping division by department code
        //        IList<DepartmentMaster> dataList = departmentMasterRepo.Search(oModel, db, pageInt, int.Parse(pageLimit))
        //            .GroupBy(x => x.DEPARTMENT_CODE).Select(x => x.First()).ToList();

        //        var list = new List<Select2>();
        //        foreach (var data in dataList)
        //        {
        //            list.Add(new Select2() { text = data.DEPARTMENT_CODE, id = data.DEPARTMENT_CODE });
        //        }
        //        return Json(new { status = true, items = list });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { status = false, message = ex.Message });
        //    }
        //}
        public JsonResult GetDivisionCode(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DivisionMaster oDepartmentMaster = new DivisionMaster();
                if (q != null)
                    oDepartmentMaster.DIVISION_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DivisionMaster> dataList = divisionMasterRepo.Search(oDepartmentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DIVISION_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DIVISION_CODE, id = data.DIVISION_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
        public JsonResult GetDivisionName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DivisionMaster oDepartmentMaster = new DivisionMaster();
                if (q != null)
                    oDepartmentMaster.DIVISION_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DivisionMaster> dataList = divisionMasterRepo.Search(oDepartmentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DIVISION_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DIVISION_NAME, id = data.DIVISION_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        //public JsonResult GetDepartmentIdAndName(string q, string pageLimit, string page)
        //{
        //    try
        //    {
        //        AjaxResult ajaxResult = new AjaxResult();
        //        RepoResult repoResult = new RepoResult();

        //        DepartmentMaster oDepartmentMaster = new DepartmentMaster();
        //        if (q != null)
        //            oDepartmentMaster.DEPARTMENT_CODE_NAME = '*' + q + '*';

        //        int result, pageInt;

        //        if (int.TryParse(page, out result))
        //        {
        //            pageInt = int.Parse(page);
        //        }
        //        else
        //        {
        //            pageInt = 1;
        //        }

        //        IList<DepartmentMaster> dataList = departmentMasterRepo.Search(oDepartmentMaster, db, pageInt, int.Parse(pageLimit));

        //        var list = new List<Select2>();
        //        foreach (var data in dataList)
        //        {
        //            list.Add(new Select2() { text = data.DEPARTMENT_CODE_NAME, id = data.DEPARTMENT_ID.ToString() });
        //        }
        //        return Json(new { status = true, items = list });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { status = false, message = ex.Message });
        //    }
        //}

        

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

                IList<Workflow> dataList = WorkflowRepo.Instance.Search(oWorkflow, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.WORKFLOW_NAME, id = data.WORKFLOW_ID.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        //public JsonResult GetDepartmentIdAndNameByDivision(string q, string pageLimit, string page, string param)
        //{
        //    try
        //    {
        //        AjaxResult ajaxResult = new AjaxResult();
        //        RepoResult repoResult = new RepoResult();

        //        DepartmentMaster oDepartmentMaster = new DepartmentMaster();
        //        if (q != "")
        //            oDepartmentMaster.DEPARTMENT_CODE_NAME = '*' + q + '*';
        //        if (param != "")
        //            oDepartmentMaster.DIVISION = param;

        //        int result, pageInt;

        //        if (int.TryParse(page, out result))
        //        {
        //            pageInt = int.Parse(page);
        //        }
        //        else
        //        {
        //            pageInt = 1;
        //        }

        //        IList<DepartmentMaster> dataList = departmentMasterRepo.Search(oDepartmentMaster, db, pageInt, int.Parse(pageLimit));

        //        var list = new List<Select2>();
        //        foreach (var data in dataList)
        //        {
        //            list.Add(new Select2() { text = data.DEPARTMENT_CODE_NAME, id = data.DEPARTMENT_ID.ToString() });
        //        }
        //        return Json(new { status = true, items = list });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { status = false, message = ex.Message });
        //    }
        //}

        //public JsonResult GetDivisionByUsername(string q, string pageLimit, string page)
        //{
        //    try
        //    {
        //        AjaxResult ajaxResult = new AjaxResult();
        //        RepoResult repoResult = new RepoResult();

        //        MSystem oSystem = new MSystem();
        //        if (q != "")
        //            oSystem.SYSTEM_CODE_VALUE = '*' + q + '*';

        //        int result, pageInt;

        //        if (int.TryParse(page, out result))
        //        {
        //            pageInt = int.Parse(page);
        //        }
        //        else
        //        {
        //            pageInt = 1;
        //        }

        //        IList<MSystem> dataList = departmentMasterRepo.SearchDivision(oSystem, GetLoginUsername(), db, pageInt, int.Parse(pageLimit));
        //        //.GroupBy(x => x.SYSTEM_VALUE).Select(x => x.First()).ToList();

        //        var list = new List<Select2>();
        //        foreach (var data in dataList)
        //        {
        //            list.Add(new Select2() { text = data.SYSTEM_CODE_VALUE, id = data.SYSTEM_CODE });
        //        }
        //        return Json(new { status = true, items = list });
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(new { status = false, message = ex.Message });
        //    }
        //}

    }
}
