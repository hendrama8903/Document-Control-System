using DMS.Common.Controllers;
using DMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DMS.Controllers
{
    /// <summary>
    /// Pencarian dokumen global dari kotak "Search documents" di top bar - mencari di
    /// nomor, judul, dan ISI dokumen (request Hendra 2026-11-06). Isi dokumen diindeks
    /// oleh Services/DocumentContentIndexer; hak lihat diatur sp_DocumentSearch.
    /// </summary>
    public class SearchController : BaseController
    {
        private readonly DBContext db;
        public const int PageSize = 20;

        public SearchController(DBContext db)
        {
            this.db = db;
        }

        public class Result
        {
            public string SOURCE_TYPE { get; set; }
            public int SOURCE_ID { get; set; }
            public string DOCUMENT_CODE { get; set; }
            public string TITLE { get; set; }
            public string REVISION { get; set; }
            public string STATUS { get; set; }
            public string STATUS_NAME { get; set; }
            public string DEPARTMENT_NAME { get; set; }
            public string CATEGORY_NAME { get; set; }
            public string FILE_PATH { get; set; }
            public DateTime? DOC_DATE { get; set; }
            public bool MATCH_IN_TITLE { get; set; }
            public string INDEX_STATUS { get; set; }
            public string EXTRACT_METHOD { get; set; }   // TEXT / OCR / TEXT+OCR / TEXT/NO-OCR
            public string SNIPPET { get; set; }
        }

        public class PageModel
        {
            public string Query { get; set; }
            public List<string> Terms { get; set; } = new List<string>();
            public List<Result> Results { get; set; } = new List<Result>();
            public int Total { get; set; }
            public int Page { get; set; }
            public bool IsAdmin { get; set; }
            public int PageCount => (Total + PageSize - 1) / PageSize;
        }

        public IActionResult Index(string q, int page = 1)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
                return Redirect("/Auth/Login?param=" + Uri.EscapeDataString(Request.Path + Request.QueryString));

            ViewData["Title"] = "Search Documents";
            var model = new PageModel
            {
                Query = (q ?? "").Trim(),
                Page = Math.Max(1, page),
                IsAdmin = HttpContext.Session.GetString("ROLE_ID") == "DMS-ADMIN",
            };
            if (model.Query.Length > 200) model.Query = model.Query.Substring(0, 200);
            model.Terms = model.Query.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 2).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();

            if (model.Terms.Count > 0) Search(model);
            return View(model);
        }

        private void Search(PageModel model)
        {
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "[dbo].[sp_DocumentSearch]";
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 60;
                command.Parameters.Add(new SqlParameter("@KEYWORD", model.Query));
                command.Parameters.Add(new SqlParameter("@USERNAME", GetLoginUsername()));
                command.Parameters.Add(new SqlParameter("@IS_ADMIN", model.IsAdmin));
                command.Parameters.Add(new SqlParameter("@PageNumber", model.Page));
                command.Parameters.Add(new SqlParameter("@PageSize", PageSize));

                using var r = command.ExecuteReader();
                string S(string c) => r[c] == DBNull.Value ? null : Convert.ToString(r[c]);
                while (r.Read())
                {
                    model.Total = Convert.ToInt32(r["TOTAL_ROWS"]);
                    model.Results.Add(new Result
                    {
                        SOURCE_TYPE = S("SOURCE_TYPE"),
                        SOURCE_ID = Convert.ToInt32(r["SOURCE_ID"]),
                        DOCUMENT_CODE = S("DOCUMENT_CODE"),
                        TITLE = S("TITLE"),
                        REVISION = S("REVISION"),
                        STATUS = S("STATUS"),
                        STATUS_NAME = S("STATUS_NAME"),
                        DEPARTMENT_NAME = S("DEPARTMENT_NAME"),
                        CATEGORY_NAME = S("CATEGORY_NAME"),
                        FILE_PATH = S("FILE_PATH"),
                        DOC_DATE = r["DOC_DATE"] == DBNull.Value ? null : Convert.ToDateTime(r["DOC_DATE"]),
                        MATCH_IN_TITLE = Convert.ToInt32(r["MATCH_IN_TITLE"]) == 1,
                        INDEX_STATUS = S("INDEX_STATUS"),
                        EXTRACT_METHOD = S("EXTRACT_METHOD"),
                        SNIPPET = S("SNIPPET"),
                    });
                }
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }
    }
}
