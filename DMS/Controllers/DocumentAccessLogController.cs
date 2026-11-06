using DMS.Common.Controllers;
using DMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;

namespace DMS.Controllers
{
    /// <summary>
    /// Laporan audit trail akses dokumen - Administration > Document Access Log
    /// (request Hendra 2026-11-06). Data dicatat oleh Services/DocumentAccessLogService.
    /// </summary>
    public class DocumentAccessLogController : BaseController
    {
        private readonly DBContext db;

        public DocumentAccessLogController(DBContext db)
        {
            this.db = db;
        }

        public class Row
        {
            public long ACCESS_LOG_ID { get; set; }
            public DateTime ACCESS_DT { get; set; }
            public string USERNAME { get; set; }
            public string USER_FULL_NAME { get; set; }
            public string ACTION { get; set; }
            public string SOURCE_TYPE { get; set; }
            public string DOCUMENT_CODE { get; set; }
            public string DOCUMENT_TITLE { get; set; }
            public string REVISION { get; set; }
            public string DEPARTMENT_NAME { get; set; }
            public int? COPIES { get; set; }
            public string SOURCE_PAGE { get; set; }
            public string CLIENT_IP { get; set; }
            public string USER_AGENT { get; set; }
        }

        bool HasAccess() =>
            HttpContext.Session.GetString("USERNAME") != null &&
            (HttpContext.Session.GetString("menuURL") ?? "").Contains("/DocumentAccessLog/Index");

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null) return Redirect("/Auth/Login");
            if (!HasAccess())
            {
                Response.StatusCode = 403;
                return View("Error403");
            }
            ViewData["Title"] = "Document Access Log";
            return View();
        }

        [HttpPost]
        public IActionResult Search(DateTime? startDt, DateTime? endDt, string action, string username, string keyword, int skip = 0, int take = 25)
        {
            if (!HasAccess()) return StatusCode(403, new { status = false, message = "Access denied" });
            try
            {
                take = Math.Clamp(take, 1, 500);
                int total;
                List<Row> rows = Load(startDt, endDt, action, username, keyword, skip / take + 1, take, out total);
                return Json(new { status = true, data = rows, totalCount = total });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadExcel(DateTime? startDt, DateTime? endDt, string action, string username, string keyword)
        {
            if (!HasAccess()) return StatusCode(403);

            List<Row> rows = Load(startDt, endDt, action, username, keyword, null, null, out _);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Access Log");
            string[] headers = { "No", "Time", "User", "Full Name", "Action", "Copies", "Document No.", "Title", "Revision",
                "Department", "Source", "Opened From", "IP Address", "Browser / Device" };
            IRow headerRow = sheet.CreateRow(0);
            for (int c = 0; c < headers.Length; c++) headerRow.CreateCell(c).SetCellValue(headers[c]);

            int r = 1;
            foreach (Row item in rows)
            {
                IRow row = sheet.CreateRow(r);
                row.CreateCell(0).SetCellValue(r);
                row.CreateCell(1).SetCellValue(item.ACCESS_DT.ToString("yyyy-MM-dd HH:mm:ss"));
                row.CreateCell(2).SetCellValue(item.USERNAME);
                row.CreateCell(3).SetCellValue(item.USER_FULL_NAME ?? "");
                row.CreateCell(4).SetCellValue(item.ACTION);
                if (item.COPIES != null) row.CreateCell(5).SetCellValue(item.COPIES.Value);
                row.CreateCell(6).SetCellValue(item.DOCUMENT_CODE ?? "");
                row.CreateCell(7).SetCellValue(item.DOCUMENT_TITLE ?? "");
                row.CreateCell(8).SetCellValue(item.REVISION ?? "");
                row.CreateCell(9).SetCellValue(item.DEPARTMENT_NAME ?? "");
                row.CreateCell(10).SetCellValue(item.SOURCE_TYPE == "EXTERNAL" ? "External" : "Internal");
                row.CreateCell(11).SetCellValue(item.SOURCE_PAGE ?? "");
                row.CreateCell(12).SetCellValue(item.CLIENT_IP ?? "");
                row.CreateCell(13).SetCellValue(item.USER_AGENT ?? "");
                r++;
            }
            for (int c = 0; c < headers.Length - 1; c++) sheet.AutoSizeColumn(c);

            using var memoryStream = new MemoryStream();
            workbook.Write(memoryStream);
            return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DOCUMENT-ACCESS-LOG-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        private List<Row> Load(DateTime? startDt, DateTime? endDt, string action, string username, string keyword,
            int? pageNumber, int? pageSize, out int total)
        {
            var rows = new List<Row>();
            total = 0;
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "[dbo].[sp_DocumentAccessLog_Search]";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@START_DT", (object)startDt ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@END_DT", (object)endDt ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ACTION", (object)action ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@USERNAME", (object)username ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@KEYWORD", (object)keyword ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@PageNumber", (object)pageNumber ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@PageSize", (object)pageSize ?? DBNull.Value));

                using var rd = command.ExecuteReader();
                string S(string c) => rd[c] == DBNull.Value ? null : Convert.ToString(rd[c]);
                while (rd.Read())
                {
                    total = Convert.ToInt32(rd["TOTAL_ROWS"]);
                    rows.Add(new Row
                    {
                        ACCESS_LOG_ID = Convert.ToInt64(rd["ACCESS_LOG_ID"]),
                        ACCESS_DT = Convert.ToDateTime(rd["ACCESS_DT"]),
                        USERNAME = S("USERNAME"),
                        USER_FULL_NAME = S("USER_FULL_NAME"),
                        ACTION = S("ACTION"),
                        SOURCE_TYPE = S("SOURCE_TYPE"),
                        DOCUMENT_CODE = S("DOCUMENT_CODE"),
                        DOCUMENT_TITLE = S("DOCUMENT_TITLE"),
                        REVISION = S("REVISION"),
                        DEPARTMENT_NAME = S("DEPARTMENT_NAME"),
                        COPIES = rd["COPIES"] == DBNull.Value ? null : Convert.ToInt32(rd["COPIES"]),
                        SOURCE_PAGE = S("SOURCE_PAGE"),
                        CLIENT_IP = S("CLIENT_IP"),
                        USER_AGENT = S("USER_AGENT"),
                    });
                }
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
            return rows;
        }
    }
}
