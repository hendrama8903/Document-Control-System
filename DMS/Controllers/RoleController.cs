using System.Data;
using System.Diagnostics;
using System.Text;
using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DMS.Controllers
{
    public class RoleController : BaseController
    {
        DBContext db;
        public RoleController(DBContext db)
        {
            this.db = db;
        }

        private RoleRepo roleRepo = RoleRepo.Instance;
        private AuthMenuRepo authMenuRepo = AuthMenuRepo.Instance;
        private AuthFunctionRepo authFunctionRepo = AuthFunctionRepo.Instance;
        private FunctionRepo functionRepo = FunctionRepo.Instance;
        private MenuRepo menuRepo = MenuRepo.Instance;
        private LogMonitoringRepo logRepo = LogMonitoringRepo.Instance;

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/Role/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("ROLE-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("ROLE-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("ROLE-DELETE");
            ViewData["Authorization"] = HttpContext.Session.GetString("functionList").Contains("ROLE-AUTHORIZATION");

            ViewData["Title"] = "Role Authorization";

            return View();
        }

        public JsonResult GetByKey(Role data)
        {
            try
            {
                Role result = roleRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(Role data)
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
                var roleData = roleRepo.SearchGrid(data, db, pageNumber, pageSize);
                var dataCount = roleRepo.SearchGrid(data, db, null, null).Count;
                recordsTotal = dataCount;
                var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = roleData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public IActionResult DownloadExcel()
        {
            IList<RoleListItem> listData = roleRepo.SearchGrid(new Role(), db, null, null);

            var memoryStream = new MemoryStream();
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Role Authorization");

            string[] headers = { "No", "Role ID", "Role Name", "Description", "Users", "Permissions" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (RoleListItem item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.ROLE_ID);
                row.CreateCell(2).SetCellValue(item.ROLE_NAME);
                row.CreateCell(3).SetCellValue(item.ROLE_DESC);
                row.CreateCell(4).SetCellValue(item.USERS_COUNT ?? 0);
                row.CreateCell(5).SetCellValue(item.PERMISSIONS_COUNT ?? 0);

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
                "ROLE-AUTHORIZATION-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        public JsonResult AddEdit(string screenMode, Role data)
        {
            DBResult result = null;
            try
            {
                if (screenMode == "ADD")
                {
                    result = roleRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = roleRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(Role data)
        {
            DBResult result = null;
            try
            {
                result = roleRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetRoleId(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Role oRole = new Role();
                if (q != null)
                    oRole.ROLE_ID = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Role> dataList = roleRepo.Search(oRole, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.ROLE_ID).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.ROLE_ID, id = data.ROLE_ID });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetRoleName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Role oRole = new Role();
                if (q != "")
                    oRole.ROLE_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Role> dataList = roleRepo.Search(oRole, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.ROLE_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.ROLE_NAME, id = data.ROLE_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetRoleIdName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Role oRole = new Role();
                if (q != "")
                    oRole.ROLE_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Role> dataList = roleRepo.Search(oRole, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.ROLE_NAME, id = data.ROLE_ID.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult GetAuthByRole(AuthMenu authMenu)
        {
            IList<Menu> dataMenu;
            IList<Function> dataFunction;
            IList<AuthMenu> menuByRole;
            IList<AuthFunction> funcByRole;

            bool status;
            object param;

            AuthFunction authFunction = new AuthFunction { ROLE_ID = authMenu.ROLE_ID };

            try
            {
                dataMenu = menuRepo.GetAll(db);
                menuByRole = authMenuRepo.Search(authMenu, db);

                dataFunction = functionRepo.GetAll(db);
                funcByRole = authFunctionRepo.Search(authFunction, db);

                status = true;
                param = new object[] { dataMenu, menuByRole, dataFunction, funcByRole };
            }
            catch (Exception ex)
            {
                status = false;
                param = new object[] { };
            }

            return Json(new { status, param });
        }

        public ActionResult GetAuthMenuByRole(AuthMenu data)
        {
            IList<Menu> dataMenu;
            IList<AuthMenu> dataByRole;

            bool status;
            object param;

            try
            {
                dataMenu = menuRepo.GetAll(db);
                dataByRole = authMenuRepo.Search(data, db);

                status = true;
                param = new object[] { dataMenu, dataByRole };
            }
            catch (Exception ex)
            {
                status = false;
                param = new object[] { };
            }

            return Json(new { status, param });
        }

        public ActionResult GetAuthFunctionByRole(AuthFunction data)
        {
            IList<Function> dataFunction;
            IList<AuthFunction> dataByRole;

            bool status;
            object param;

            try
            {
                dataFunction = functionRepo.GetAll(db);
                dataByRole = authFunctionRepo.Search(data, db);

                status = true;
                param = new object[] { dataFunction, dataByRole };
            }
            catch (Exception ex)
            {
                status = false;
                param = new object[] { };
            }

            return Json(new { status, param });
        }

        public JsonResult AddEditMenu(List<AuthMenu> data)
        {
            DBResult result = null;
            string module = "Role Authorization - Authorization";
            string location = "BOMPartMasterController/AddEditMenu";
            string functionLocation = "Insert";

            LogHeader logH = new LogHeader();
            logH.MODULE = module;
            logH.FUNCTION = functionLocation;

            long processid = logRepo.StartLog(logH, location, GetLoginUsername(), db);

            try
            {
                db.Database.BeginTransaction();

                AuthMenu oAuthMenuRole = new AuthMenu();
                oAuthMenuRole.ROLE_ID = data[0].ROLE_ID;

                result = authMenuRepo.DeleteByRole(oAuthMenuRole, db);
                if (result.status == false)
                {
                    db.Database.RollbackTransaction();
                    logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);
                    return Json(result);
                }
                else
                {
                    logRepo.WriteLog(processid, "1", "INF", result.message, location, GetLoginUsername(), db);
                }

                if (data[0].MENU_ID != null)
                {
                    foreach (AuthMenu oAuthMenu in data)
                    {
                        AuthMenu oAuthMenuData = new AuthMenu();
                        oAuthMenuData.ROLE_ID = oAuthMenu.ROLE_ID;
                        oAuthMenuData.MENU_ID = oAuthMenu.MENU_ID;

                        result = authMenuRepo.Insert(oAuthMenuData, GetLoginUsername(), db);
                        if (result.status == false)
                        {
                            db.Database.RollbackTransaction();
                            logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);
                            return Json(result);
                        }
                        else
                        {
                            logRepo.WriteLog(processid, "1", "INF", result.message, location, GetLoginUsername(), db);
                        }
                    }
                }
                
                db.Database.CommitTransaction();
                logRepo.WriteLog(processid, "2", "INF", result.message, location, GetLoginUsername(), db);

                return Json(result);
            }
            catch (Exception ex)
            {
                db.Database.RollbackTransaction();
                logRepo.WriteLog(processid, "4", "ERR", ex.Message, location, GetLoginUsername(), db);

                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult AddEditFunction(List<AuthFunction> data)
        {
            DBResult result = null;
            string module = "Role Authorization - Authorization";
            string location = "BOMPartMasterController/AddEditFunction";
            string functionLocation = "Insert";

            LogHeader logH = new LogHeader();
            logH.MODULE = module;
            logH.FUNCTION = functionLocation;

            long processid = logRepo.StartLog(logH, location, GetLoginUsername(), db);

            try
            {
                db.Database.BeginTransaction();

                AuthFunction oAuthFunctionRole = new AuthFunction();
                oAuthFunctionRole.ROLE_ID = data[0].ROLE_ID;

                result = authFunctionRepo.DeleteByRole(oAuthFunctionRole, db);
                if (result.status == false)
                {
                    db.Database.RollbackTransaction();
                    logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);
                    return Json(result);
                }
                else
                {
                    logRepo.WriteLog(processid, "1", "INF", result.message, location, GetLoginUsername(), db);
                }

                if (data[0].FUNCTION_ID != null)
                {
                    foreach (AuthFunction oAuthFunction in data)
                    {
                        AuthFunction oAuthFunctionData = new AuthFunction();
                        oAuthFunctionData.ROLE_ID = oAuthFunction.ROLE_ID;
                        oAuthFunctionData.FUNCTION_ID = oAuthFunction.FUNCTION_ID; ;

                        result = authFunctionRepo.Insert(oAuthFunctionData, GetLoginUsername(), db);
                        if (result.status == false)
                        {
                            db.Database.RollbackTransaction();
                            logRepo.WriteLog(processid, "3", "ERR", result.message, location, GetLoginUsername(), db);
                            return Json(result);
                        }
                        else
                        {
                            logRepo.WriteLog(processid, "1", "INF", result.message, location, GetLoginUsername(), db);
                        }
                    }
                }

                db.Database.CommitTransaction();
                logRepo.WriteLog(processid, "2", "INF", result.message, location, GetLoginUsername(), db);

                return Json(result);
            }
            catch (Exception ex)
            {
                db.Database.RollbackTransaction();
                logRepo.WriteLog(processid, "4", "ERR", ex.Message, location, GetLoginUsername(), db);

                return Json(new { status = false, message = ex.Message });
            }
        }

    }
}
