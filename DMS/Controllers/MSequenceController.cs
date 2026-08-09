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
                    Response.StatusCode = 403;
                    return View("Error403");
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

        public IActionResult DownloadExcel()
        {
            IList<MSequence> listData = mSequenceRepo.Search(new MSequence(), db, null, null);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Numbering");

            string[] headers = { "No", "Sequence Code", "Type", "Last Number Used", "Changed By", "Changed Date" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (MSequence item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.SEQ_CODE);
                row.CreateCell(2).SetCellValue(item.SEQ_TYPE);
                row.CreateCell(3).SetCellValue(item.SEQ_NO ?? 0);
                row.CreateCell(4).SetCellValue(item.CHANGED_BY);
                row.CreateCell(5).SetCellValue(item.CHANGED_DT != null ? item.CHANGED_DT.Value.ToString("yyyy-MM-dd HH:mm:ss") : "");

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
                "DOCUMENT-NUMBERING-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
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
