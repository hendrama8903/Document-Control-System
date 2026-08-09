using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class LogMonitoringController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        public LogMonitoringController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        public LogMonitoringRepo logMonitoringRepo = LogMonitoringRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/LogMonitoring/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Title"] = "Activity Log";

            return View();
        }

        public IActionResult DownloadExcel(DateTime? startDt, DateTime? endDt, string module, string createdBy)
        {
            LogHeader filter = new LogHeader
            {
                START_DT = startDt,
                END_DT = endDt,
                MODULE = module,
                CREATED_BY = createdBy
            };
            IList<LogHeader> listData = logMonitoringRepo.Search(filter, db, null, null);

            XSSFWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Activity Log");

            string[] headers = { "No", "Process ID", "Time", "User", "Module", "Function", "Status", "Duration (ms)" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (LogHeader item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.PROCESS_ID ?? 0);
                row.CreateCell(2).SetCellValue(item.START_DT != null ? item.START_DT.Value.ToString("yyyy-MM-dd HH:mm:ss") : "");
                row.CreateCell(3).SetCellValue(item.CREATED_BY);
                row.CreateCell(4).SetCellValue(item.MODULE);
                row.CreateCell(5).SetCellValue(item.FUNCTION);
                row.CreateCell(6).SetCellValue(ProcessStatusLabel(item.PROCESS_STATUS));
                double durationMs = (item.START_DT != null && item.END_DT != null)
                    ? (item.END_DT.Value - item.START_DT.Value).TotalMilliseconds
                    : 0;
                row.CreateCell(7).SetCellValue(durationMs);

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
                "ACTIVITY-LOG-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        private static string ProcessStatusLabel(string processStatus)
        {
            switch (processStatus)
            {
                case "2": return "Success";
                case "3": return "Failed";
                case "4": return "Failed";
                default: return "Running";
            }
        }

        public JsonResult GetByKey(LogHeader data)
        {
            try
            {
                LogHeader result = logMonitoringRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult Search(LogHeader data)
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
                var SupplierMasterData = logMonitoringRepo.Search(data, db, pageNumber, pageSize);
                var dataCount = logMonitoringRepo.Search(data, db, null, null).Count;
                recordsTotal = dataCount;
                var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = SupplierMasterData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult SearchDetail(int PROCESS_ID)
        {
            LogDetail dLogDetail = new LogDetail();
            try
            {
                dLogDetail.PROCESS_ID = PROCESS_ID;
                var result = logMonitoringRepo.SearchDetail(dLogDetail, db, null, null);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetListModule(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                LogHeader oLogHeader = new LogHeader();
                if (q != null)
                    oLogHeader.MODULE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<LogHeader> dataList = logMonitoringRepo.GetListModule(oLogHeader, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.MODULE, id = data.MODULE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetListFunction(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                LogHeader oLogHeader = new LogHeader();
                if (q != null)
                    oLogHeader.FUNCTION = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<LogHeader> dataList = logMonitoringRepo.GetListFunction(oLogHeader, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.FUNCTION, id = data.FUNCTION });
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
