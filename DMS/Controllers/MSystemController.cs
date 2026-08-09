using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class MSystemController : BaseController
    {
        DBContext db;
        public MSystemController(DBContext db)
        {
            this.db = db;
        }

        private MSystemRepo msystemRepo = MSystemRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/MSystem/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("SYSTEMMASTER-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("SYSTEMMASTER-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("SYSTEMMASTER-DELETE");

            ViewData["Title"] = "System Parameters";

            return View();
        }

        public JsonResult GetByKey(MSystem data)
        {
            try
            {
                MSystem result = msystemRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult Search(MSystem data, bool initialMode)
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
                    var msystemData = msystemRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = msystemRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = msystemData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public IActionResult DownloadExcel()
        {
            IList<MSystem> listData = msystemRepo.Search(new MSystem(), db, null, null);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("System Parameters");

            string[] headers = { "No", "System Type", "System Code", "System Value", "Status" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (MSystem item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.SYSTEM_TYPE);
                row.CreateCell(2).SetCellValue(item.SYSTEM_CODE);
                row.CreateCell(3).SetCellValue(item.SYSTEM_VALUE);
                row.CreateCell(4).SetCellValue(item.STATUS == 1 ? "Active" : "Inactive");

                rowIndex++;
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                workbook.Write(ms);
                fileBytes = ms.ToArray();
            }

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "SYSTEM-PARAMETERS-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        public IActionResult SearchSystem(MSystem data)
        {
            try
            {
                var msystemData = msystemRepo.Search(data, db, null, null);
                return Json(new { status = true, data = msystemData });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult AddEdit(string screenMode, MSystem data)
        {
            DBResult result = null;
            try
            {
                if (screenMode == "ADD")
                {
                    result = msystemRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = msystemRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(MSystem data)
        {
            DBResult result = null;
            try
            {
                result = msystemRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSystemType(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                MSystem oSystem = new MSystem();
                if (q != "")
                    oSystem.SYSTEM_TYPE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<MSystem> dataList = msystemRepo.Search(oSystem, db, pageInt, int.Parse(pageLimit));
                //.GroupBy(x => x.SYSTEM_TYPE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SYSTEM_TYPE, id = data.SYSTEM_TYPE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSystemCode(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                MSystem oSystem = new MSystem();
                if (q != "")
                    oSystem.SYSTEM_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<MSystem> dataList = msystemRepo.Search(oSystem, db, pageInt, int.Parse(pageLimit));
                //.GroupBy(x => x.SYSTEM_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SYSTEM_CODE, id = data.SYSTEM_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSystemValue(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                MSystem oSystem = new MSystem();
                if (q != "")
                    oSystem.SYSTEM_VALUE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<MSystem> dataList = msystemRepo.Search(oSystem, db, pageInt, int.Parse(pageLimit));
                //.GroupBy(x => x.SYSTEM_VALUE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SYSTEM_VALUE, id = data.SYSTEM_VALUE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSystemCodeSystemValue(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                MSystem oSystem = new MSystem();
                if (q != "")
                    oSystem.SYSTEM_CODE_VALUE = '*' + q + '*';
                if (param != "")
                    oSystem.SYSTEM_TYPE = param;

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<MSystem> dataList = msystemRepo.Search(oSystem, db, pageInt, int.Parse(pageLimit));
                //.GroupBy(x => x.SYSTEM_VALUE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SYSTEM_CODE_VALUE, id = data.SYSTEM_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetSystemCodeBySystemType(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                MSystem oSystem = new MSystem();
                if (q != "")
                    oSystem.SYSTEM_VALUE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<MSystem> dataList = msystemRepo.GetByType(param, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.SYSTEM_VALUE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.SYSTEM_CODE, id = data.SYSTEM_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetTop1SysCodeSysVal(MSystem data)
        {
            try
            {
                MSystem result = msystemRepo.GetTop1BySysVal(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

    }
}
