using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.POIFS.Crypt.Dsig;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class SectionMasterController : BaseController
    {
        DBContext db;
        public SectionMasterController(DBContext db)
        {
            this.db = db;
        }

        private SectionMasterRepo sectionMasterRepo = SectionMasterRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/SectionMaster/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("SECTIONMASTER-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("SECTIONMASTER-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("SECTIONMASTER-DELETE");

            ViewData["Title"] = "Section Master";

            return View();
        }

        public JsonResult GetByKey(SectionMaster data)
        {
            try
            {
                SectionMaster result = sectionMasterRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(SectionMaster data, bool initialMode)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int pageNumber = pageSize > 0 ? (skip / pageSize) + 1 : 1;
                int recordsTotal = 0;
                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = new List<object>() };
                    return Ok(jsonData);
                }
                else
                {
                    var sectionMasterData = sectionMasterRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = sectionMasterRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = sectionMasterData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = 0,
                    recordsFiltered = 0,
                    recordsTotal = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        public JsonResult AddEdit(string screenMode, SectionMaster data)
        {
            DBResult result = null;
            try
            {
                if (screenMode == "ADD")
                {
                    result = sectionMasterRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = sectionMasterRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(SectionMaster data)
        {
            DBResult result = null;
            try
            {
                result = sectionMasterRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult DeleteMultiple(List<int> selectedIds)
        {
            DBResult result = null;
            try
            {
                foreach (var id in selectedIds)
                {
                    result = sectionMasterRepo.Delete(new SectionMaster { SECTION_ID = id }, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        return Json(result);
                    }
                }

                return Json(new { status = true, message = "Data Deleted Successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSectionCode(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Select2SectionCode oSectionMaster = new Select2SectionCode();
                string DepartmentCode = null;

                if (param != "")
                    DepartmentCode = param;

                if (q != null)
                    oSectionMaster.SECTION_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Select2SectionCode> dataList = sectionMasterRepo.ListSectionCode(oSectionMaster, DepartmentCode, db, pageInt, int.Parse(pageLimit));
                    //.GroupBy(x => x.SECTION_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SECTION_CODE, id = data.SECTION_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
        public JsonResult GetSectionName(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Select2SectionName oSectionMaster = new Select2SectionName();
                string DepartmentCode = null;

                if (param != "")
                    DepartmentCode = param;

                if (q != null)
                    oSectionMaster.SECTION_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Select2SectionName> dataList = sectionMasterRepo.ListSectionName(oSectionMaster, DepartmentCode, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SECTION_NAME, id = data.SECTION_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSectionCodeAndName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Select2SectionCodeAndName oSectionMaster = new Select2SectionCodeAndName();

                if (q != null)
                    oSectionMaster.SECTION_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Select2SectionCodeAndName> dataList = sectionMasterRepo.ListSectionCodeAndName(oSectionMaster, null, null, db, pageInt, int.Parse(pageLimit)).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SECTION_NAME, id = data.SECTION_CODE.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSectionCodeAndNameByDepartment(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Select2SectionCodeAndName oSectionMaster = new Select2SectionCodeAndName();
                int? departmentId = null;

                if (param != "")
                    departmentId = int.Parse(param);

                if (q != null)
                    oSectionMaster.SECTION_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Select2SectionCodeAndName> dataList = sectionMasterRepo.ListSectionCodeAndName(oSectionMaster, departmentId, null, db, pageInt, int.Parse(pageLimit)).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SECTION_NAME, id = data.SECTION_CODE.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSectionNameAndSectionIDByDepartment(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                int result, pageInt, paramInt;

                if (int.TryParse(param, out result))
                {
                    paramInt = int.Parse(param);
                }
                else
                {
                    paramInt = 0;
                }

                SectionMaster oSectionMaster = new SectionMaster();
                if (q != null)
                    oSectionMaster.SECTION_NAME = '*' + q + '*';
                if (param != null)
                    oSectionMaster.DEPARTMENT_ID = Int32.Parse(param);

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<SectionMaster> dataList = sectionMasterRepo.Search(oSectionMaster, db, pageInt, int.Parse(pageLimit));
                    //.GroupBy(x => x.SECTION_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SECTION_NAME, id = data.SECTION_ID.ToString() });
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

