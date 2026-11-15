using DMS.Common.Controllers;
using DMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System.Data;

namespace DMS.Controllers
{
    /// <summary>
    /// Administration > Access Rights Review (request Hendra 2026-11-14): who holds which role,
    /// which menus and functions that role opens, login activity
    /// (role matrix, per-user access). Downloadable as an Excel review pack
    /// with a Review Decision column for the periodic user access review.
    /// SQL: SQL/AccessRightReview_Migration.sql + sp_AccessRight_*.
    /// </summary>
    public class AccessRightController : BaseController
    {
        private readonly DBContext db;
        private const int DormantDays = 90;
        private const string AdminRole = "DMS-ADMIN";

        public AccessRightController(DBContext db)
        {
            this.db = db;
        }

        bool HasAccess() =>
            HttpContext.Session.GetString("USERNAME") != null &&
            (HttpContext.Session.GetString("menuURL") ?? "").Contains("/AccessRight/Index");

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null) return Redirect("/Auth/Login");
            if (!HasAccess())
            {
                Response.StatusCode = 403;
                return View("Error403");
            }
            ViewData["Title"] = "Access Rights Review";
            return View();
        }

        [HttpPost]
        public IActionResult Data(string roleId, string status, string keyword)
        {
            if (!HasAccess()) return StatusCode(403, new { status = false, message = "Access denied" });
            try
            {
                Review r = LoadReview(roleId, status, keyword);
                return Json(new
                {
                    status = true,
                    users = r.Users,
                    items = r.Items,
                    roles = r.Roles,
                    trackingSince = r.TrackingSince
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult LoginLog(DateTime? startDt, DateTime? endDt, string username)
        {
            if (!HasAccess()) return StatusCode(403, new { status = false, message = "Access denied" });
            try
            {
                return Json(new { status = true, data = Query("sp_AccessRight_LoginLog", LoginParams(startDt, endDt, username))[0] });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // ------------------------------------------------------------------ data

        private class Review
        {
            public List<Dictionary<string, object>> Users;
            public List<Dictionary<string, object>> Items;
            public List<Dictionary<string, object>> Roles;
            public DateTime? TrackingSince;
        }

        private Review LoadReview(string roleId, string status, string keyword)
        {
            var users = Query("sp_AccessRight_Users", new[]
            {
                new SqlParameter("@ROLE_ID", (object)Blank(roleId) ?? DBNull.Value),
                new SqlParameter("@STATUS", (object)Blank(status) ?? DBNull.Value),
                new SqlParameter("@KEYWORD", (object)Blank(keyword) ?? DBNull.Value),
            })[0];
            var matrix = Query("sp_AccessRight_Matrix", Array.Empty<SqlParameter>());
            DateTime? since = null;
            var first = Query(null, Array.Empty<SqlParameter>(), "SELECT MIN(LOGIN_DT) AS FIRST_DT FROM dbo.TB_R_USER_LOGIN_LOG")[0];
            if (first.Count > 0 && first[0]["FIRST_DT"] is DateTime d) since = d;

            return new Review
            {
                Users = users,
                Items = matrix[0],
                Roles = matrix[1],
                TrackingSince = since
            };
        }

        private static bool Granted(Dictionary<string, object> item, string role) =>
            (Str(item, "GRANTED_ROLES") ?? "").Split(',').Any(x => string.Equals(x, role, StringComparison.OrdinalIgnoreCase));

        private static string Str(Dictionary<string, object> row, string key) =>
            row.TryGetValue(key, out object v) && v != null && v != DBNull.Value && Convert.ToString(v) != "" ? Convert.ToString(v) : null;

        private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static SqlParameter[] LoginParams(DateTime? startDt, DateTime? endDt, string username) => new[]
        {
            new SqlParameter("@START_DT", (object)startDt ?? DBNull.Value),
            new SqlParameter("@END_DT", (object)endDt ?? DBNull.Value),
            new SqlParameter("@USERNAME", (object)Blank(username) ?? DBNull.Value),
        };

        /// <summary>Runs a stored procedure (or plain SQL) and returns every result set as rows of column/value.</summary>
        private List<List<Dictionary<string, object>>> Query(string procedure, SqlParameter[] parameters, string sql = null)
        {
            var sets = new List<List<Dictionary<string, object>>>();
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql ?? "[dbo].[" + procedure + "]";
                command.CommandType = sql == null ? CommandType.StoredProcedure : CommandType.Text;
                foreach (var p in parameters) command.Parameters.Add(p);
                using var rd = command.ExecuteReader();
                do
                {
                    var rows = new List<Dictionary<string, object>>();
                    while (rd.Read())
                    {
                        var row = new Dictionary<string, object>(rd.FieldCount);
                        for (int i = 0; i < rd.FieldCount; i++)
                            row[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i);
                        rows.Add(row);
                    }
                    sets.Add(rows);
                } while (rd.NextResult());
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
            return sets;
        }

        // ------------------------------------------------------------------ Excel

        public IActionResult DownloadExcel(string roleId, string status, string keyword)
        {
            if (!HasAccess()) return StatusCode(403);

            Review r = LoadReview(roleId, status, keyword);
            var logins = Query("sp_AccessRight_LoginLog", LoginParams(DateTime.Today.AddDays(-DormantDays), DateTime.Today, null))[0];

            var wb = new XSSFWorkbook();
            var st = new Styles(wb);

            // 1. Summary (report info + reviewer sign-off)
            ISheet sum = wb.CreateSheet("Summary");
            int row = 0;
            Cell(sum, row++, 0, "Access Rights Review", st.Title);
            row++;
            string filter = string.Join(", ", new[]
            {
                Blank(roleId) != null ? "Role = " + roleId : null,
                Blank(status) != null ? "Status = " + status : "Status = All",
                Blank(keyword) != null ? "Keyword = " + keyword : null
            }.Where(x => x != null));
            var info = new (string, string)[]
            {
                ("Generated", DateTime.Now.ToString("dd MMM yyyy HH:mm")),
                ("Generated by", HttpContext.Session.GetString("USERNAME")),
                ("Filter", filter),
                ("Users in this report", r.Users.Count.ToString()),
                ("Active users", r.Users.Count(u => Convert.ToInt32(u["IS_ACTIVE"]) == 1).ToString()),
                ("Admin role users (active)", r.Users.Count(u => Convert.ToInt32(u["IS_ACTIVE"]) == 1 && string.Equals(Str(u, "ROLE_ID"), AdminRole, StringComparison.OrdinalIgnoreCase)).ToString()),
                ("Roles", r.Roles.Count.ToString()),
                ("Login history since", r.TrackingSince?.ToString("dd MMM yyyy") ?? "Not recorded yet"),
            };
            foreach (var (k, v) in info)
            {
                Cell(sum, row, 0, k, st.Label);
                Cell(sum, row++, 1, v, st.Text);
            }
            sum.SetColumnWidth(0, 26 * 256);
            sum.SetColumnWidth(1, 40 * 256);
            row++;
            Cell(sum, row++, 0, "Review sign-off", st.Title);
            foreach (string s in new[] { "Reviewed by", "Position", "Date", "Signature" })
            {
                Cell(sum, row, 0, s, st.Label);
                Cell(sum, row++, 1, "", st.Input);
            }

            // 2. Users (one row per user, with reviewer columns)
            ISheet us = wb.CreateSheet("Users");
            string[] uh = { "No", "Username", "Full Name", "Reg. No", "Email", "Role ID", "Role Name", "Account Type", "Status",
                "Position - Org. Unit", "Division", "Department", "Menus", "Functions", "Last Login", "Days Since Login",
                "Logins (90 days)", "Failed Logins (30 days)", "Created By", "Created", "Changed By", "Changed",
                "Review Decision", "Reviewer Notes" };
            Header(us, 0, st, uh);
            int n = 1;
            foreach (var u in r.Users)
            {
                IRow x = us.CreateRow(n);
                int c = 0;
                Num(x, c++, n, st.Text);
                Txt(x, c++, Str(u, "USERNAME"), st.Text);
                Txt(x, c++, Str(u, "FULL_NAME"), st.Text);
                Txt(x, c++, Str(u, "REG_NO"), st.Text);
                Txt(x, c++, Str(u, "EMAIL"), st.Text);
                Txt(x, c++, Str(u, "ROLE_ID"), st.Text);
                Txt(x, c++, Str(u, "ROLE_NAME"), st.Text);
                Txt(x, c++, Str(u, "ACCOUNT_TYPE"), st.Text);
                Txt(x, c++, Convert.ToInt32(u["IS_ACTIVE"]) == 1 ? "Active" : "Inactive", st.Text);
                Txt(x, c++, Str(u, "POSITIONS"), st.Text);
                Txt(x, c++, Str(u, "DIVISIONS"), st.Text);
                Txt(x, c++, Str(u, "DEPARTMENTS"), st.Text);
                Num(x, c++, Convert.ToInt32(u["MENU_COUNT"]), st.Text);
                Num(x, c++, Convert.ToInt32(u["FUNCTION_COUNT"]), st.Text);
                if (u["LAST_LOGIN_DT"] is DateTime ll)
                {
                    Txt(x, c++, ll.ToString("yyyy-MM-dd HH:mm"), st.Text);
                    Num(x, c++, (int)(DateTime.Now - ll).TotalDays, st.Text);
                }
                else
                {
                    Txt(x, c++, "-", st.Text);
                    Txt(x, c++, "-", st.Text);
                }
                Num(x, c++, Convert.ToInt32(u["LOGIN_COUNT_90D"]), st.Text);
                Num(x, c++, Convert.ToInt32(u["FAILED_LOGIN_30D"]), st.Text);
                Txt(x, c++, Str(u, "CREATED_BY"), st.Text);
                Txt(x, c++, u["CREATED_DT"] is DateTime cd ? cd.ToString("yyyy-MM-dd") : "", st.Text);
                Txt(x, c++, Str(u, "CHANGED_BY"), st.Text);
                Txt(x, c++, u["CHANGED_DT"] is DateTime chd ? chd.ToString("yyyy-MM-dd") : "", st.Text);
                Txt(x, c++, "", st.Input);
                Txt(x, c++, "", st.Input);
                n++;
            }
            if (r.Users.Count > 0)
            {
                var dv = us.GetDataValidationHelper();
                var v = dv.CreateValidation(dv.CreateExplicitListConstraint(new[] { "Keep", "Change role", "Revoke" }),
                    new CellRangeAddressList(1, r.Users.Count, uh.Length - 2, uh.Length - 2));
                v.ShowErrorBox = true;
                us.AddValidationData(v);
            }
            Finish(us, uh.Length, r.Users.Count, new[] { 5, 16, 24, 10, 30, 14, 22, 14, 10, 40, 30, 34, 8, 9, 16, 10, 10, 12, 14, 12, 14, 12, 16, 40 });

            // 3. User access detail (user x menu/function)
            ISheet ud = wb.CreateSheet("User Access Detail");
            string[] dh = { "Username", "Full Name", "Status", "Role ID", "Module", "Menu", "Type", "Function ID", "Function" };
            Header(ud, 0, st, dh);
            n = 1;
            foreach (var u in r.Users)
            {
                string role = Str(u, "ROLE_ID");
                foreach (var i in r.Items.Where(i => role != null && Granted(i, role)))
                {
                    IRow x = ud.CreateRow(n++);
                    Txt(x, 0, Str(u, "USERNAME"), st.Text);
                    Txt(x, 1, Str(u, "FULL_NAME"), st.Text);
                    Txt(x, 2, Convert.ToInt32(u["IS_ACTIVE"]) == 1 ? "Active" : "Inactive", st.Text);
                    Txt(x, 3, role, st.Text);
                    Txt(x, 4, Str(i, "PARENT_NAME") ?? "-", st.Text);
                    Txt(x, 5, Str(i, "MENU_NAME"), st.Text);
                    Txt(x, 6, Str(i, "ITEM_TYPE") == "MENU" ? "Menu" : "Function", st.Text);
                    Txt(x, 7, Str(i, "FUNCTION_ID"), st.Text);
                    Txt(x, 8, Str(i, "FUNCTION_NAME"), st.Text);
                }
            }
            Finish(ud, dh.Length, n - 1, new[] { 16, 24, 10, 18, 18, 28, 10, 26, 36 });

            // 4. Role matrix (menu/function x role)
            ISheet rm = wb.CreateSheet("Role Matrix");
            var roleCols = r.Roles.Select(x => Str(x, "ROLE_ID")).ToList();
            var mh = new List<string> { "Module", "Menu", "Type", "Function ID", "Function" };
            mh.AddRange(roleCols);
            Header(rm, 0, st, mh.ToArray());
            IRow cnt = rm.CreateRow(1);
            Txt(cnt, 0, "Active users per role", st.Label);
            for (int k = 0; k < r.Roles.Count; k++) Num(cnt, 5 + k, Convert.ToInt32(r.Roles[k]["ACTIVE_USERS"]), st.Center);
            n = 2;
            foreach (var i in r.Items)
            {
                IRow x = rm.CreateRow(n++);
                bool isMenu = Str(i, "ITEM_TYPE") == "MENU";
                var s = isMenu ? st.Label : st.Text;
                Txt(x, 0, Str(i, "PARENT_NAME") ?? "-", s);
                Txt(x, 1, Str(i, "MENU_NAME"), s);
                Txt(x, 2, isMenu ? "Menu" : "Function", s);
                Txt(x, 3, Str(i, "FUNCTION_ID"), s);
                Txt(x, 4, Str(i, "FUNCTION_NAME"), s);
                for (int k = 0; k < roleCols.Count; k++)
                    Txt(x, 5 + k, Granted(i, roleCols[k]) ? "✓" : "", st.Check);
            }
            var widths = new List<int> { 18, 28, 10, 26, 34 };
            widths.AddRange(roleCols.Select(_ => 14));
            Finish(rm, mh.Count, n - 1, widths.ToArray(), freezeCols: 5);

            // 5. Login history (last 90 days)
            ISheet lg = wb.CreateSheet("Login History");
            string[] lh = { "Time", "Username", "Full Name", "Result", "Message", "IP Address", "Browser / Device" };
            Header(lg, 0, st, lh);
            n = 1;
            foreach (var l in logins)
            {
                IRow x = lg.CreateRow(n++);
                Txt(x, 0, l["LOGIN_DT"] is DateTime t ? t.ToString("yyyy-MM-dd HH:mm:ss") : "", st.Text);
                Txt(x, 1, Str(l, "USERNAME"), st.Text);
                Txt(x, 2, Str(l, "FULL_NAME"), st.Text);
                Txt(x, 3, Str(l, "RESULT") == "SUCCESS" ? "Success" : "Failed", st.Text);
                Txt(x, 4, Str(l, "MESSAGE"), st.Text);
                Txt(x, 5, Str(l, "CLIENT_IP"), st.Text);
                Txt(x, 6, Str(l, "USER_AGENT"), st.Text);
            }
            Finish(lg, lh.Length, logins.Count, new[] { 20, 18, 26, 10, 36, 16, 60 });

            using var ms = new MemoryStream();
            wb.Write(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ACCESS-RIGHTS-REVIEW-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

        private class Styles
        {
            public ICellStyle Title, Header, Label, Text, Wrap, Input, Check, Center;
            public Styles(IWorkbook wb)
            {
                IFont F(bool bold, short size, bool white = false)
                {
                    var f = wb.CreateFont();
                    f.FontName = "Arial";
                    f.FontHeightInPoints = size;
                    f.IsBold = bold;
                    if (white) f.Color = IndexedColors.White.Index;
                    return f;
                }
                ICellStyle S(IFont f, bool border = true)
                {
                    var s = wb.CreateCellStyle();
                    s.SetFont(f);
                    s.VerticalAlignment = VerticalAlignment.Top;
                    if (border)
                    {
                        s.BorderTop = s.BorderBottom = s.BorderLeft = s.BorderRight = BorderStyle.Thin;
                        s.TopBorderColor = s.BottomBorderColor = s.LeftBorderColor = s.RightBorderColor = IndexedColors.Grey25Percent.Index;
                    }
                    return s;
                }
                Title = S(F(true, 13), false);
                Header = S(F(true, 10, true));
                Header.FillForegroundColor = IndexedColors.RoyalBlue.Index;
                Header.FillPattern = FillPattern.SolidForeground;
                Header.WrapText = true;
                Label = S(F(true, 10));
                Text = S(F(false, 10));
                Wrap = S(F(false, 10));
                Wrap.WrapText = true;
                Input = S(F(false, 10));
                Input.FillForegroundColor = IndexedColors.LightYellow.Index;
                Input.FillPattern = FillPattern.SolidForeground;
                Check = S(F(true, 10));
                Check.Alignment = HorizontalAlignment.Center;
                Center = S(F(false, 10));
                Center.Alignment = HorizontalAlignment.Center;
            }
        }

        private static void Cell(ISheet sh, int r, int c, string v, ICellStyle s)
        {
            IRow row = sh.GetRow(r) ?? sh.CreateRow(r);
            Txt(row, c, v, s);
        }

        private static void Txt(IRow row, int c, string v, ICellStyle s)
        {
            ICell cell = row.CreateCell(c);
            cell.SetCellValue(v ?? "");
            cell.CellStyle = s;
        }

        private static void Num(IRow row, int c, double v, ICellStyle s)
        {
            ICell cell = row.CreateCell(c);
            cell.SetCellValue(v);
            cell.CellStyle = s;
        }

        private static void Header(ISheet sh, int r, Styles st, params string[] headers)
        {
            IRow row = sh.CreateRow(r);
            for (int c = 0; c < headers.Length; c++) Txt(row, c, headers[c], st.Header);
        }

        private static void Finish(ISheet sh, int cols, int dataRows, int[] widths, int freezeCols = 0)
        {
            for (int c = 0; c < widths.Length && c < cols; c++) sh.SetColumnWidth(c, widths[c] * 256);
            sh.CreateFreezePane(freezeCols, 1);
            if (dataRows > 0) sh.SetAutoFilter(new CellRangeAddress(0, dataRows, 0, cols - 1));
        }
    }
}
