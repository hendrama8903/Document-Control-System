using DMS.Common.Controllers;
using DMS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;

namespace DMS.Controllers
{
    /// <summary>
    /// Administration > Reference Access (request Hendra 2026-11-18, tahap 1).
    /// Document Control lets a person READ the documents of another department (optionally
    /// one category) for a set period, e.g. GA reading IT procedures to learn from them. The rule
    /// itself is applied by dbo.fn_Document_AccessReason (reason 5), so Search, preview and file
    /// access follow it and a revoked or expired grant disappears everywhere at once. View only:
    /// see DocumentAccessService.IsReferenceOnly. Tables: SQL/ReferenceAccess_Migration.sql.
    /// </summary>
    public class ReferenceAccessController : BaseController
    {
        private const string MenuUrl = "/ReferenceAccess/Index";
        public const string FunctionAdd = "REFERENCEACCESS-ADD";
        public const string FunctionEdit = "REFERENCEACCESS-EDIT";
        public const string FunctionRevoke = "REFERENCEACCESS-REVOKE";

        private readonly DBContext db;

        public ReferenceAccessController(DBContext db)
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

            string functions = HttpContext.Session.GetString("functionList") ?? "";
            bool Has(string id) => functions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(id);
            ViewData["Add"] = Has(FunctionAdd);
            ViewData["Edit"] = Has(FunctionEdit);
            ViewData["Revoke"] = Has(FunctionRevoke);
            ViewData["Title"] = "Reference Access";
            return View();
        }

        private JsonResult DenyWithoutMenu() =>
            GetLoginUsername() == null ? Json(new { status = false, message = "Session expired. Please log in again." })
            : !HasMenuAccess(MenuUrl) ? Json(new { status = false, message = "You are not authorized to use this feature." })
            : null;

        // ------------------------------------------------------------------ data for the form

        /// <summary>Active users (readers), departments (owners) and document categories for the form.</summary>
        [HttpGet]
        public JsonResult Options()
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;

            var users = Query(@"
                SELECT U.USERNAME, U.FULL_NAME, " + UserDepartmentsSql("U.USERNAME") + @" AS DEPARTMENTS
                FROM dbo.TB_M_USER U
                WHERE ISNULL(U.DELETE_FLAG, '0') <> '1'
                ORDER BY U.FULL_NAME, U.USERNAME").Select(r => new
            {
                id = r["USERNAME"] as string,
                name = r["FULL_NAME"] as string,
                departments = r["DEPARTMENTS"] as string
            });

            var departments = Query(@"
                SELECT DEPARTMENT_ID, DEPARTMENT_CODE, DEPARTMENT_NAME, DIVISION
                FROM dbo.TB_M_DEPARTMENT
                WHERE ISNULL(DELETE_FLAG, 0) = 0 AND (VALID_TO IS NULL OR VALID_TO >= CAST(GETDATE() AS DATE))
                ORDER BY DEPARTMENT_CODE").Select(r => new
            {
                id = Convert.ToInt32(r["DEPARTMENT_ID"]),
                code = r["DEPARTMENT_CODE"] as string,
                name = r["DEPARTMENT_NAME"] as string,
                division = (r["DIVISION"] as string ?? "").Split(" - ", 2)[0].Trim()
            });

            var categories = Query(@"
                SELECT DOCUMENT_ID, DOCUMENT_CODE, DOCUMENT_NAME, [LEVEL]
                FROM dbo.TB_M_DOCUMENT
                WHERE ISNULL(DELETE_FLAG, 0) = 0
                ORDER BY [LEVEL], DOCUMENT_CODE").Select(r => new
            {
                id = Convert.ToInt32(r["DOCUMENT_ID"]),
                code = r["DOCUMENT_CODE"] as string,
                name = r["DOCUMENT_NAME"] as string,
                level = r["LEVEL"] == null ? (int?)null : Convert.ToInt32(r["LEVEL"])
            });

            return Json(new { status = true, users, departments, categories });
        }

        // The user's department codes, "GAD, HRD" (a user can hold several positions).
        private static string UserDepartmentsSql(string usernameColumn) => @"
            STUFF((SELECT DISTINCT ', ' + D.DEPARTMENT_CODE
                   FROM dbo.TB_M_USER_POS P JOIN dbo.TB_M_DEPARTMENT D ON D.DEPARTMENT_ID = P.DEPARTMENT_ID
                   WHERE P.USERNAME = " + usernameColumn + @"
                   FOR XML PATH('')), 1, 2, '')";

        // ------------------------------------------------------------------ list

        // Status shown in the list: Revoked, or for an active grant Scheduled / Active / Expired
        // by today's date. Documents = what the reader can read through it right now (same
        // conditions as fn_Document_AccessReason reason 5, without the user's other rights).
        // The reader is one person (2026-11-18); VIEWER_DEPARTMENTS is only for information.
        // Also read by Access Rights Review (AccessRightController).
        public static readonly string ListSql = @"
            SELECT RA.REFERENCE_ACCESS_ID, RA.VIEWER_USERNAME, RA.OWNER_DEPARTMENT_ID, RA.DOCUMENT_ID,
                   VU.FULL_NAME AS VIEWER_FULL_NAME,
                   CASE WHEN VU.USERNAME IS NULL OR ISNULL(VU.DELETE_FLAG, '0') = '1' THEN 0 ELSE 1 END AS VIEWER_ACTIVE,
                   " + UserDepartmentsSql("RA.VIEWER_USERNAME") + @" AS VIEWER_DEPARTMENTS,
                   OD.DEPARTMENT_CODE AS OWNER_CODE, OD.DEPARTMENT_NAME AS OWNER_NAME,
                   C.DOCUMENT_CODE AS CATEGORY_CODE, C.DOCUMENT_NAME AS CATEGORY_NAME,
                   RA.VALID_FROM, RA.VALID_TO, RA.REASON, RA.[STATUS], RA.REVOKE_REASON, RA.REVOKED_BY, RA.REVOKED_DT,
                   RA.CREATED_BY, RA.CREATED_DT, RA.CHANGED_BY, RA.CHANGED_DT,
                   CASE WHEN RA.[STATUS] = 0 THEN 'Revoked'
                        WHEN CAST(GETDATE() AS DATE) < RA.VALID_FROM THEN 'Scheduled'
                        WHEN CAST(GETDATE() AS DATE) > RA.VALID_TO THEN 'Expired'
                        ELSE 'Active' END AS STATE,
                   (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT D
                    WHERE D.DEPARTMENT = RA.OWNER_DEPARTMENT_ID AND ISNULL(D.DELETE_FLAG, 0) = 0
                      AND D.[STATUS] IN ('1', '5') AND ISNULL(D.CLASSIFIED, 0) <> 2
                      AND (RA.DOCUMENT_ID IS NULL OR D.DOCUMENT_ID = RA.DOCUMENT_ID)) AS DOCUMENT_COUNT
            FROM dbo.TB_M_REFERENCE_ACCESS RA
            LEFT JOIN dbo.TB_M_USER VU ON VU.USERNAME = RA.VIEWER_USERNAME
            LEFT JOIN dbo.TB_M_DEPARTMENT OD ON OD.DEPARTMENT_ID = RA.OWNER_DEPARTMENT_ID
            LEFT JOIN dbo.TB_M_DOCUMENT C ON C.DOCUMENT_ID = RA.DOCUMENT_ID";

        [HttpPost]
        public JsonResult Search()
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;

            var rows = Query(ListSql + " ORDER BY RA.[STATUS] DESC, RA.VALID_TO DESC, RA.REFERENCE_ACCESS_ID DESC").Select(ToRow).ToList();
            return Json(new { status = true, data = rows });
        }

        private static object ToRow(Dictionary<string, object> r) => new
        {
            id = Convert.ToInt32(r["REFERENCE_ACCESS_ID"]),
            viewerUsername = r["VIEWER_USERNAME"] as string,
            ownerDepartmentId = Convert.ToInt32(r["OWNER_DEPARTMENT_ID"]),
            documentId = r["DOCUMENT_ID"] == null ? (int?)null : Convert.ToInt32(r["DOCUMENT_ID"]),
            viewer = ReaderLabel(r),
            viewerDepartments = r["VIEWER_DEPARTMENTS"] as string,
            viewerActive = Convert.ToInt32(r["VIEWER_ACTIVE"]) == 1,
            owner = Label(r["OWNER_CODE"], r["OWNER_NAME"], r["OWNER_DEPARTMENT_ID"]),
            category = r["DOCUMENT_ID"] == null ? "All categories" : Label(r["CATEGORY_CODE"], r["CATEGORY_NAME"], r["DOCUMENT_ID"]),
            validFrom = Convert.ToDateTime(r["VALID_FROM"]).ToString("yyyy-MM-dd"),
            validTo = Convert.ToDateTime(r["VALID_TO"]).ToString("yyyy-MM-dd"),
            reason = r["REASON"] as string,
            state = r["STATE"] as string,
            revokeReason = r["REVOKE_REASON"] as string,
            revokedBy = r["REVOKED_BY"] as string,
            revokedDt = r["REVOKED_DT"] as DateTime?,
            createdBy = r["CREATED_BY"] as string,
            createdDt = r["CREATED_DT"] as DateTime?,
            changedBy = r["CHANGED_BY"] as string,
            changedDt = r["CHANGED_DT"] as DateTime?,
            documentCount = Convert.ToInt32(r["DOCUMENT_COUNT"])
        };

        // A department deleted from the master after the grant was made still shows its id.
        private static string Label(object code, object name, object id) =>
            code == null ? "(deleted #" + id + ")" : code + " - " + name;

        /// <summary>"Budi Santoso (budi.s)"; a user removed from User Management keeps the username.</summary>
        public static string ReaderLabel(IDictionary<string, object> r) =>
            r["VIEWER_FULL_NAME"] is string name && name.Length > 0
                ? name + " (" + r["VIEWER_USERNAME"] + ")"
                : r["VIEWER_USERNAME"] + " (user not found)";

        // ------------------------------------------------------------------ save / revoke

        /// <summary>
        /// Add: one grant per person chosen (viewerUsernames may hold several). Edit: one grant,
        /// exactly one person. The reader is a person, not a department (2026-11-18).
        /// </summary>
        [HttpPost]
        public JsonResult Save(int? id, string[] viewerUsernames, int ownerDepartmentId, int? documentId,
            DateTime? validFrom, DateTime? validTo, string reason)
        {
            JsonResult denied = DenyWithoutMenu() ?? DenyWithoutFunction(id == null ? FunctionAdd : FunctionEdit);
            if (denied != null) return denied;

            reason = (reason ?? "").Trim();
            DateTime today = DateTime.Today;
            List<string> readers = (viewerUsernames ?? Array.Empty<string>())
                .Select(x => (x ?? "").Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (readers.Count == 0)
                return Fail("Choose at least one reader.");
            if (id != null && readers.Count != 1)
                return Fail("An existing reference access belongs to one reader. Add a new one for other people.");
            if (ownerDepartmentId <= 0)
                return Fail("Choose the owner department.");
            if (validFrom == null || validTo == null)
                return Fail("Valid From and Valid To are required.");
            if (validTo.Value.Date < validFrom.Value.Date)
                return Fail("Valid To must be on or after Valid From.");
            if (validTo.Value.Date < today)
                return Fail("Valid To is in the past; the access would never be usable.");
            if (reason.Length == 0)
                return Fail("Reason is required, e.g. what the reader needs to learn.");
            if (reason.Length > 500)
                return Fail("Reason is limited to 500 characters.");

            try
            {
                using var tx = db.Database.BeginTransaction();

                if (id != null)
                {
                    var current = Query("SELECT [STATUS] FROM dbo.TB_M_REFERENCE_ACCESS WITH (UPDLOCK) WHERE REFERENCE_ACCESS_ID = @ID", ("@ID", id)).FirstOrDefault();
                    if (current == null) return Fail("This reference access no longer exists. Refresh the list.");
                    if (Convert.ToInt32(current["STATUS"]) == 0) return Fail("A revoked reference access cannot be edited. Add a new one instead.");
                }

                var problems = new List<string>();
                foreach (string reader in readers)
                {
                    var u = Query(@"SELECT U.FULL_NAME, ISNULL(U.DELETE_FLAG, '0') AS DELETE_FLAG,
                                    CASE WHEN EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS P WHERE P.USERNAME = U.USERNAME AND P.DEPARTMENT_ID = @O) THEN 1 ELSE 0 END AS IN_OWNER
                                    FROM dbo.TB_M_USER U WHERE U.USERNAME = @U", ("@U", reader), ("@O", ownerDepartmentId)).FirstOrDefault();
                    string who = u?["FULL_NAME"] is string n && n.Length > 0 ? n + " (" + reader + ")" : reader;
                    if (u == null || (u["DELETE_FLAG"] as string) == "1") { problems.Add(who + " is not an active user"); continue; }
                    if (Convert.ToInt32(u["IN_OWNER"]) == 1) { problems.Add(who + " already belongs to the owner department"); continue; }

                    // One grant per reader / owner / category for overlapping dates, so the list stays clear.
                    var overlap = Query(@"
                        SELECT TOP 1 VALID_FROM, VALID_TO FROM dbo.TB_M_REFERENCE_ACCESS
                        WHERE [STATUS] = 1 AND VIEWER_USERNAME = @V AND OWNER_DEPARTMENT_ID = @O
                          AND ISNULL(DOCUMENT_ID, 0) = ISNULL(@C, 0)
                          AND VALID_FROM <= @TO AND VALID_TO >= @FROM
                          AND (@ID IS NULL OR REFERENCE_ACCESS_ID <> @ID)",
                        ("@V", reader), ("@O", ownerDepartmentId), ("@C", documentId),
                        ("@FROM", validFrom.Value.Date), ("@TO", validTo.Value.Date), ("@ID", id)).FirstOrDefault();
                    if (overlap != null)
                        problems.Add(who + " already has this access for " + Convert.ToDateTime(overlap["VALID_FROM"]).ToString("dd-MM-yyyy")
                            + " to " + Convert.ToDateTime(overlap["VALID_TO"]).ToString("dd-MM-yyyy") + " (edit that one)");
                }
                // All or nothing, so the user fixes the list once instead of finding half of it saved.
                if (problems.Count > 0)
                    return Fail("Nothing was saved: " + string.Join("; ", problems) + ".");

                string user = GetLoginUsername();
                foreach (string reader in readers)
                {
                    int savedId;
                    if (id == null)
                    {
                        savedId = Convert.ToInt32(Scalar(@"
                            INSERT INTO dbo.TB_M_REFERENCE_ACCESS (VIEWER_USERNAME, OWNER_DEPARTMENT_ID, DOCUMENT_ID, VALID_FROM, VALID_TO, REASON, [STATUS], CREATED_BY, CREATED_DT)
                            VALUES (@V, @O, @C, @FROM, @TO, @R, 1, @U, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            ("@V", reader), ("@O", ownerDepartmentId), ("@C", documentId),
                            ("@FROM", validFrom.Value.Date), ("@TO", validTo.Value.Date), ("@R", reason), ("@U", user)));
                    }
                    else
                    {
                        savedId = id.Value;
                        Execute(@"
                            UPDATE dbo.TB_M_REFERENCE_ACCESS
                            SET VIEWER_USERNAME = @V, OWNER_DEPARTMENT_ID = @O, DOCUMENT_ID = @C, VALID_FROM = @FROM, VALID_TO = @TO,
                                REASON = @R, CHANGED_BY = @U, CHANGED_DT = GETDATE()
                            WHERE REFERENCE_ACCESS_ID = @ID",
                            ("@V", reader), ("@O", ownerDepartmentId), ("@C", documentId),
                            ("@FROM", validFrom.Value.Date), ("@TO", validTo.Value.Date), ("@R", reason), ("@U", user), ("@ID", savedId));
                    }
                    WriteLog(savedId, id == null ? "CREATE" : "EDIT", Describe(savedId), user);
                }
                tx.Commit();

                return Json(new
                {
                    status = true,
                    message = id != null ? "Reference access updated."
                        : readers.Count == 1 ? "Reference access added." : "Reference access added for " + readers.Count + " people."
                });
            }
            catch (Exception ex)
            {
                return Fail("Saving failed: " + ex.Message);
            }
        }

        [HttpPost]
        public JsonResult Revoke(int id, string reason)
        {
            JsonResult denied = DenyWithoutMenu() ?? DenyWithoutFunction(FunctionRevoke);
            if (denied != null) return denied;

            reason = (reason ?? "").Trim();
            if (reason.Length == 0) return Fail("Write why the access is revoked.");
            if (reason.Length > 500) return Fail("Reason is limited to 500 characters.");

            try
            {
                using var tx = db.Database.BeginTransaction();
                string user = GetLoginUsername();
                int changed = Execute(@"
                    UPDATE dbo.TB_M_REFERENCE_ACCESS
                    SET [STATUS] = 0, REVOKE_REASON = @R, REVOKED_BY = @U, REVOKED_DT = GETDATE(), CHANGED_BY = @U, CHANGED_DT = GETDATE()
                    WHERE REFERENCE_ACCESS_ID = @ID AND [STATUS] = 1",
                    ("@R", reason), ("@U", user), ("@ID", id));
                if (changed == 0) return Fail("This reference access is already revoked or no longer exists. Refresh the list.");

                WriteLog(id, "REVOKE", "Revoked: " + reason, user);
                tx.Commit();
                return Json(new { status = true, message = "Reference access revoked. The reader can no longer see these documents." });
            }
            catch (Exception ex)
            {
                return Fail("Revoking failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ history / documents

        [HttpGet]
        public JsonResult History(int id)
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;

            var rows = Query(@"
                SELECT L.[ACTION], L.DETAIL, L.ACTION_BY, ISNULL(U.FULL_NAME, L.ACTION_BY) AS ACTION_BY_NAME, L.ACTION_DT
                FROM dbo.TB_M_REFERENCE_ACCESS_LOG L
                LEFT JOIN dbo.TB_M_USER U ON U.USERNAME = L.ACTION_BY
                WHERE L.REFERENCE_ACCESS_ID = @ID
                ORDER BY L.ACTION_DT DESC, L.LOG_ID DESC", ("@ID", id)).Select(r => new
            {
                action = r["ACTION"] as string,
                detail = r["DETAIL"] as string,
                by = r["ACTION_BY_NAME"] as string,
                at = Convert.ToDateTime(r["ACTION_DT"])
            });
            return Json(new { status = true, data = rows });
        }

        /// <summary>The documents the reader department can read through this grant (when it is active).</summary>
        [HttpGet]
        public JsonResult Documents(int id)
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;

            var rows = Query(@"
                SELECT D.DOCUMENT_CODE, D.DOCUMENT_TRANSACTION_NAME, D.REVISION, C.DOCUMENT_NAME AS CATEGORY_NAME, D.DOCUMENT_DATE
                FROM dbo.TB_M_REFERENCE_ACCESS RA
                JOIN dbo.TB_R_DOCUMENT D ON D.DEPARTMENT = RA.OWNER_DEPARTMENT_ID
                LEFT JOIN dbo.TB_M_DOCUMENT C ON C.DOCUMENT_ID = D.DOCUMENT_ID
                WHERE RA.REFERENCE_ACCESS_ID = @ID AND ISNULL(D.DELETE_FLAG, 0) = 0
                  AND D.[STATUS] IN ('1', '5') AND ISNULL(D.CLASSIFIED, 0) <> 2
                  AND (RA.DOCUMENT_ID IS NULL OR D.DOCUMENT_ID = RA.DOCUMENT_ID)
                ORDER BY D.DOCUMENT_CODE", ("@ID", id)).Select(r => new
            {
                code = r["DOCUMENT_CODE"] as string,
                title = r["DOCUMENT_TRANSACTION_NAME"] as string,
                revision = r["REVISION"]?.ToString(),
                category = r["CATEGORY_NAME"] as string,
                date = r["DOCUMENT_DATE"] as DateTime?
            });
            return Json(new { status = true, data = rows });
        }

        // ------------------------------------------------------------------ helpers

        private JsonResult Fail(string message) => Json(new { status = false, message });

        // The grant in one sentence for the log, read back after saving so it shows the codes.
        private string Describe(int id)
        {
            var r = Query(ListSql + " WHERE RA.REFERENCE_ACCESS_ID = @ID", ("@ID", id)).First();
            return ReaderLabel(r) + " may read "
                + Label(r["OWNER_CODE"], r["OWNER_NAME"], r["OWNER_DEPARTMENT_ID"]) + " documents ("
                + (r["DOCUMENT_ID"] == null ? "all categories" : r["CATEGORY_CODE"] + "") + ") from "
                + Convert.ToDateTime(r["VALID_FROM"]).ToString("dd-MM-yyyy") + " to " + Convert.ToDateTime(r["VALID_TO"]).ToString("dd-MM-yyyy")
                + ". Reason: " + r["REASON"];
        }

        private void WriteLog(int id, string action, string detail, string user) =>
            Execute(@"INSERT INTO dbo.TB_M_REFERENCE_ACCESS_LOG (REFERENCE_ACCESS_ID, [ACTION], DETAIL, ACTION_BY, ACTION_DT)
                      VALUES (@ID, @A, @D, @U, GETDATE())",
                ("@ID", id), ("@A", action), ("@D", detail != null && detail.Length > 1000 ? detail.Substring(0, 1000) : detail), ("@U", user));

        private List<Dictionary<string, object>> Query(string sql, params (string name, object value)[] parameters)
        {
            var rows = new List<Dictionary<string, object>>();
            Run(sql, parameters, command =>
            {
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < reader.FieldCount; i++)
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
            });
            return rows;
        }

        private object Scalar(string sql, params (string name, object value)[] parameters)
        {
            object result = null;
            Run(sql, parameters, command => result = command.ExecuteScalar());
            return result;
        }

        private int Execute(string sql, params (string name, object value)[] parameters)
        {
            int affected = 0;
            Run(sql, parameters, command => affected = command.ExecuteNonQuery());
            return affected;
        }

        private void Run(string sql, (string name, object value)[] parameters, Action<DbCommand> action)
        {
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
                if (transaction != null) command.Transaction = transaction;
                foreach (var (name, value) in parameters)
                    command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
                action(command);
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }
    }
}
