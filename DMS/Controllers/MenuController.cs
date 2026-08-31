using System.Data;
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
    public class MenuController : BaseController
    {
        DBContext db;
        public MenuController(DBContext db)
        {
            this.db = db;
        }

        private MenuRepo menuRepo = MenuRepo.Instance;
        private FunctionRepo functionRepo = FunctionRepo.Instance;

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/Menu/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("MENU-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("MENU-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("MENU-DELETE");
            ViewData["Function"] = HttpContext.Session.GetString("functionList").Contains("MENU-FUNCTION");

            ViewData["Title"] = "Menu & Function";

            return View();
        }

        public JsonResult GetMenuFunctionStats()
        {
            try
            {
                var result = menuRepo.GetStats(db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetMenuTree()
        {
            try
            {
                var result = menuRepo.GetTree(db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult ToggleMenuStatus(string menuId, int deleteFlag)
        {
            DBResult result = null;
            try
            {
                result = menuRepo.ToggleStatus(menuId, deleteFlag, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult ToggleFunctionStatus(string functionId, int deleteFlag)
        {
            DBResult result = null;
            try
            {
                result = functionRepo.ToggleStatus(functionId, deleteFlag, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel()
        {
            IList<MenuListItem> menuList = menuRepo.GetTree(db);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Menu & Function");

            string[] headers = { "No", "Menu ID", "Parent Menu", "Menu Name", "Menu URL", "Function Count", "Used By Roles", "Status" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (MenuListItem item in menuList)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.MENU_ID);
                row.CreateCell(2).SetCellValue(item.PARENT_NAME);
                row.CreateCell(3).SetCellValue(item.MENU_NAME);
                row.CreateCell(4).SetCellValue(item.MENU_URL);
                row.CreateCell(5).SetCellValue(item.FUNCTION_COUNT ?? 0);
                row.CreateCell(6).SetCellValue(item.USED_BY_ROLES ?? 0);
                row.CreateCell(7).SetCellValue(item.DELETE_FLAG == 1 ? "Inactive" : "Active");

                rowIndex++;
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                workbook.Write(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MENU-FUNCTION-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        public ActionResult Search(Menu data)
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
                var menuData = menuRepo.Search(data, db, pageNumber, pageSize);
                var dataCount = menuRepo.Search(data, db, null, null).Count;
                recordsTotal = dataCount;
                var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = menuData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult GetByKey(Menu data)
        {
            try
            {
                Menu result = menuRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetMenuLastID(Menu data)
        {
            String result = null;
            try
            {
                result = menuRepo.GetMenuLastID(data, db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(result);
            }
        }

        public JsonResult GetMenuLastSeq(Menu data)
        {
            String result = null;
            try
            {
                result = menuRepo.GetMenuLastSeq(data, db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(result);
            }
        }

        public JsonResult AddEdit(string screenMode, Menu data)
        {
            DBResult result = null;
            try
            {
                if (screenMode == "ADD")
                {
                    result = menuRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = menuRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(Menu data)
        {
            DBResult result = null;
            try
            {
                result = menuRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetMenuName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Menu oMenu = new Menu();
                if (q != "")
                    oMenu.MENU_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Menu> dataList = menuRepo.Search(oMenu, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.MENU_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.MENU_NAME, id = data.MENU_NAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetMenuParent(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Menu oMenu = new Menu();
                if (q != "")
                    oMenu.MENU_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Menu> dataList = menuRepo.SearchParent(oMenu, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.MENU_NAME, id = data.MENU_ID });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetMenuChild(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                Menu oMenu = new Menu();
                if (q != "")
                    oMenu.MENU_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<Menu> dataList = menuRepo.SearchChild(oMenu, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.MENU_NAME, id = data.MENU_ID });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetFunctionByMenu(Function data)
        {
            try
            {
                var result = functionRepo.GetByMenuList(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult AddEditFunction(string screenMode, Function data)
        {
            DBResult result = null;
            try
            {
                if (screenMode == "ADD")
                {
                    result = functionRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = functionRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult DeleteFunction(Function data)
        {
            DBResult result = null;
            try
            {
                result = functionRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

    }
}
