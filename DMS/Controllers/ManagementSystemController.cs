using System.Text.RegularExpressions;
using DMS.Common.Controllers;
using DMS.Models;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    /// <summary>
    /// Master Data > Management System (IMS tahap 1, 2026-11-19): QMS, K3L, SMK3, EMS ...
    /// Each system has a number code for Level 1-2 numbers, an active flag (archived = no new
    /// documents), its creators (none = everyone) and its Document Control. Tables:
    /// TB_M_MGMT_SYSTEM / TB_M_MGMT_SYSTEM_USER (SQL/ManagementSystem_Migration.sql).
    /// </summary>
    public class ManagementSystemController : BaseController
    {
        private const string MenuUrl = "/ManagementSystem/Index";
        public const string FunctionAdd = "MGMTSYSTEM-ADD";
        public const string FunctionEdit = "MGMTSYSTEM-EDIT";
        private static readonly Regex CodePattern = new Regex("^[A-Z0-9]{2,10}$");

        private readonly DBContext db;

        public ManagementSystemController(DBContext db)
        {
            this.db = db;
        }

        public IActionResult Index()
        {
            if (GetLoginUsername() == null) return Redirect("/Auth/Login");
            if (!HasMenuAccess(MenuUrl))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }
            ViewData["Add"] = HasFunction(FunctionAdd);
            ViewData["Edit"] = HasFunction(FunctionEdit);
            ViewData["Title"] = "Management System";
            return View();
        }

        private bool HasFunction(string id) => (HttpContext.Session.GetString("functionList") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(id);

        private JsonResult DenyWithoutMenu() =>
            GetLoginUsername() == null ? Json(new { status = false, message = "Session expired. Please log in again." })
            : !HasMenuAccess(MenuUrl) ? Json(new { status = false, message = "You are not authorized to use this feature." })
            : null;

        private JsonResult Fail(string message) => Json(new { status = false, message });

        [HttpPost]
        public JsonResult Search()
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;

            var users = OrgChangeService.Query(db, @"
                SELECT SU.MGMT_SYSTEM_CODE, SU.USER_ROLE, SU.USERNAME, ISNULL(U.FULL_NAME, SU.USERNAME) AS FULL_NAME
                FROM dbo.TB_M_MGMT_SYSTEM_USER SU LEFT JOIN dbo.TB_M_USER U ON U.USERNAME = SU.USERNAME
                ORDER BY FULL_NAME");
            object People(string code, string role) => users
                .Where(u => (string)u["MGMT_SYSTEM_CODE"] == code && (string)u["USER_ROLE"] == role)
                .Select(u => new { username = u["USERNAME"] as string, name = u["FULL_NAME"] as string }).ToList();

            var data = OrgChangeService.Query(db, @"
                SELECT S.MGMT_SYSTEM_CODE, S.MGMT_SYSTEM_NAME, S.STANDARD_REF, S.NUMBER_CODE, S.SORT_NO, S.IS_ACTIVE,
                       ISNULL(S.CHANGED_BY, S.CREATED_BY) AS CHANGED_BY, ISNULL(S.CHANGED_DT, S.CREATED_DT) AS CHANGED_DT,
                       (SELECT COUNT(DISTINCT D.DOCUMENT_CODE) FROM dbo.TB_R_DOCUMENT D
                        WHERE ISNULL(D.MGMT_SYSTEM, 'QMS') = S.MGMT_SYSTEM_CODE AND ISNULL(D.DELETE_FLAG, 0) = 0) AS DOCUMENTS
                FROM dbo.TB_M_MGMT_SYSTEM S ORDER BY S.SORT_NO, S.MGMT_SYSTEM_CODE")
                .Select(r =>
                {
                    string code = r["MGMT_SYSTEM_CODE"] as string;
                    return new
                    {
                        Code = code,
                        Name = r["MGMT_SYSTEM_NAME"] as string,
                        Standard = r["STANDARD_REF"] as string,
                        NumberCode = r["NUMBER_CODE"] as string,
                        SortNo = Convert.ToInt32(r["SORT_NO"]),
                        Active = Convert.ToBoolean(r["IS_ACTIVE"]),
                        Documents = Convert.ToInt32(r["DOCUMENTS"]),
                        ChangedBy = r["CHANGED_BY"] as string,
                        ChangedDt = r["CHANGED_DT"] as DateTime?,
                        Creators = People(code, "CREATOR"),
                        Controllers = People(code, "CONTROLLER")
                    };
                }).ToList();
            return Json(new { status = true, data });
        }

        /// <summary>Active users for the creator / Document Control pickers.</summary>
        [HttpGet]
        public JsonResult Users()
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;
            var data = OrgChangeService.Query(db, @"SELECT USERNAME, FULL_NAME FROM dbo.TB_M_USER
                WHERE ISNULL(DELETE_FLAG, 0) = 0 ORDER BY FULL_NAME")
                .Select(r => new { username = r["USERNAME"] as string, name = r["FULL_NAME"] as string });
            return Json(new { status = true, data });
        }

        public class SaveModel
        {
            public bool IsNew { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public string Standard { get; set; }
            public string NumberCode { get; set; }
            public int SortNo { get; set; }
            public bool Active { get; set; }
            public List<string> Creators { get; set; } = new List<string>();
            public List<string> Controllers { get; set; } = new List<string>();
        }

        [HttpPost]
        public JsonResult Save(SaveModel m)
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;
            if (!HasFunction(m.IsNew ? FunctionAdd : FunctionEdit)) return Fail("You are not authorized to use this feature.");

            string code = (m.Code ?? "").Trim().ToUpperInvariant();
            string name = (m.Name ?? "").Trim();
            string number = string.IsNullOrWhiteSpace(m.NumberCode) ? null : m.NumberCode.Trim().ToUpperInvariant();
            if (!CodePattern.IsMatch(code)) return Fail("Code: 2-10 letters or digits.");
            if (name.Length == 0) return Fail("Name is required.");
            if (number != null && !CodePattern.IsMatch(number)) return Fail("Number code: 2-10 letters or digits.");
            if (number != null && Convert.ToInt32(OrgChangeService.Scalar(db,
                    "SELECT COUNT(*) FROM dbo.TB_M_MGMT_SYSTEM WHERE NUMBER_CODE = @N AND MGMT_SYSTEM_CODE <> @C", ("@N", number), ("@C", code))) > 0)
                return Fail("Number code " + number + " is used by another system.");

            string user = GetLoginUsername();
            var creators = (m.Creators ?? new List<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();
            var controllers = (m.Controllers ?? new List<string>()).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList();

            using var tx = db.Database.BeginTransaction();
            try
            {
                var existing = OrgChangeService.Query(db,
                    "SELECT NUMBER_CODE, IS_ACTIVE FROM dbo.TB_M_MGMT_SYSTEM WITH (UPDLOCK) WHERE MGMT_SYSTEM_CODE = @C", ("@C", code)).FirstOrDefault();
                if (m.IsNew)
                {
                    if (existing != null) return Fail("Code " + code + " already exists.");
                    OrgChangeService.Execute(db, @"INSERT INTO dbo.TB_M_MGMT_SYSTEM
                        (MGMT_SYSTEM_CODE, MGMT_SYSTEM_NAME, STANDARD_REF, NUMBER_CODE, SORT_NO, IS_ACTIVE, CREATED_BY, CREATED_DT)
                        VALUES (@C, @N, @S, @NUM, @SORT, @A, @U, GETDATE())",
                        ("@C", code), ("@N", name), ("@S", Blank(m.Standard)), ("@NUM", (object)number ?? DBNull.Value),
                        ("@SORT", m.SortNo), ("@A", m.Active), ("@U", user));
                }
                else
                {
                    if (existing == null) return Fail("Management system " + code + " not found.");
                    int documents = Convert.ToInt32(OrgChangeService.Scalar(db,
                        "SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT WHERE ISNULL(MGMT_SYSTEM, 'QMS') = @C AND ISNULL(DELETE_FLAG, 0) = 0", ("@C", code)));
                    // the number code is part of every number already given: it stays once documents exist
                    if (documents > 0 && !string.Equals(existing["NUMBER_CODE"] as string, number, StringComparison.OrdinalIgnoreCase))
                        return Fail("The number code cannot change: " + documents + " document(s) already carry it.");
                    if (!m.Active && Convert.ToBoolean(existing["IS_ACTIVE"]) && Convert.ToInt32(OrgChangeService.Scalar(db,
                            "SELECT COUNT(*) FROM dbo.TB_M_MGMT_SYSTEM WHERE IS_ACTIVE = 1 AND MGMT_SYSTEM_CODE <> @C", ("@C", code))) == 0)
                        return Fail("At least one system must stay active.");
                    OrgChangeService.Execute(db, @"UPDATE dbo.TB_M_MGMT_SYSTEM SET MGMT_SYSTEM_NAME = @N, STANDARD_REF = @S, NUMBER_CODE = @NUM,
                        SORT_NO = @SORT, IS_ACTIVE = @A, CHANGED_BY = @U, CHANGED_DT = GETDATE() WHERE MGMT_SYSTEM_CODE = @C",
                        ("@C", code), ("@N", name), ("@S", Blank(m.Standard)), ("@NUM", (object)number ?? DBNull.Value),
                        ("@SORT", m.SortNo), ("@A", m.Active), ("@U", user));
                }

                OrgChangeService.Execute(db, "DELETE FROM dbo.TB_M_MGMT_SYSTEM_USER WHERE MGMT_SYSTEM_CODE = @C", ("@C", code));
                foreach (var (role, list) in new[] { ("CREATOR", creators), ("CONTROLLER", controllers) })
                    foreach (string u in list)
                        OrgChangeService.Execute(db, @"INSERT INTO dbo.TB_M_MGMT_SYSTEM_USER (MGMT_SYSTEM_CODE, USERNAME, USER_ROLE, CREATED_BY, CREATED_DT)
                            SELECT @C, USERNAME, @R, @U, GETDATE() FROM dbo.TB_M_USER WHERE USERNAME = @X AND ISNULL(DELETE_FLAG, 0) = 0",
                            ("@C", code), ("@R", role), ("@U", user), ("@X", u));

                tx.Commit();
                return Json(new { status = true, message = "Management system " + code + " saved." });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return Fail(ex.Message);
            }
        }

        private static object Blank(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
    }
}
