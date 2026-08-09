using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DMS.Controllers
{
    public class PositionMasterController : BaseController
    {
        DBContext db;
        public PositionMasterController(DBContext db)
        {
            this.db = db;
        }

        private PositionMasterRepo positionMasterRepo = PositionMasterRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/PositionMaster/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("POSITIONMASTER-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("POSITIONMASTER-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("POSITIONMASTER-DELETE");

            ViewData["Title"] = "Position Master";

            return View();
        }

        public JsonResult GetByKey(PositionMaster data)
        {
            try
            {
                PositionMaster result = positionMasterRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(PositionMaster data, bool initialMode, bool showAll = false)
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
                    var listData = positionMasterRepo.Search(data, db, pageNumber, pageSize, showAll);
                    var dataCount = positionMasterRepo.Search(data, db, null, null, showAll).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
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

        public IActionResult DownloadExcel()
        {
            IList<PositionMaster> listData = positionMasterRepo.Search(new PositionMaster(), db, null, null, true);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Position Master");

            string[] headers = { "No", "Position Name", "Position Level", "Status" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (PositionMaster item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.POSITION_NAME);
                row.CreateCell(2).SetCellValue(item.POSITION_LEVEL ?? 0);
                row.CreateCell(3).SetCellValue(item.DELETE_FLAG == 1 ? "Inactive" : "Active");

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
                "POSITION-MASTER-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        public JsonResult AddEdit(string screenMode, PositionMaster data)
        {
            DBResult result = null;
            try
            {
                if (screenMode == "ADD")
                {
                    result = positionMasterRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = positionMasterRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(PositionMaster data)
        {
            DBResult result = null;
            try
            {
                result = positionMasterRepo.Delete(data, GetLoginUsername(), db);
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
                    result = positionMasterRepo.Delete(new PositionMaster { POSITION_ID = id }, GetLoginUsername(), db);
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
    }
}
