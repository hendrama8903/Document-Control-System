using System.Data;
using System.Data.Common;
using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DMS.Services
{
    /// <summary>
    /// Organization Change, tahap 1 (2026-11-19, decisions of QMS via Hendra). A register of
    /// the organisation changes HR announces (TB_R_ORG_CHANGE + _MAP + _LOG, SQL/OrgChange_Migration.sql):
    /// what changed, from which date, on which HR memo, old code -> new code. For every old code it
    /// shows what still hangs on it (documents owned / numbered, copies held, users, active
    /// sub-units). Old codes are deactivated with VALID_TO, never deleted; that is refused while
    /// something still hangs on them, because moving it is tahap 2. The register is also the
    /// successor map of the Master List import (it replaced System Parameters DEPT_SUCCESSOR).
    /// </summary>
    public static class OrgChangeService
    {
        public static readonly string[] Types = { "RENAME", "MERGE", "SPLIT", "MOVE", "DOWNGRADE", "NEW", "CLOSE" };
        public static readonly string[] Levels = { "DIVISION", "DEPARTMENT", "SECTION" };

        public static string TypeLabel(string type) => type switch
        {
            "RENAME" => "Ganti nama / kode",
            "MERGE" => "Penggabungan (merger)",
            "SPLIT" => "Pemecahan (split)",
            "MOVE" => "Pindah divisi / departemen",
            "DOWNGRADE" => "Departemen menjadi section",
            "NEW" => "Unit baru",
            "CLOSE" => "Unit dibubarkan",
            _ => type
        };

        public static string LevelLabel(string level) => level switch
        {
            "DIVISION" => "Divisi",
            "DEPARTMENT" => "Departemen",
            "SECTION" => "Section",
            _ => level
        };

        // ------------------------------------------------------------------ model

        public class Change
        {
            public int Id { get; set; }
            public string Type { get; set; }
            public string TypeLabel => OrgChangeService.TypeLabel(Type);
            public string Level { get; set; }
            public string LevelLabel => OrgChangeService.LevelLabel(Level);
            public DateTime? EffectiveDate { get; set; }
            public string HrReference { get; set; }
            public string Description { get; set; }
            public string AttachmentName { get; set; }
            public bool HasAttachment { get; set; }
            public int Status { get; set; }
            public string CancelReason { get; set; }
            public string CreatedBy { get; set; }
            public DateTime CreatedDt { get; set; }
            public string ChangedBy { get; set; }
            public DateTime? ChangedDt { get; set; }
            public List<Map> Maps { get; set; } = new List<Map>();
            /// <summary>What tahap 2 moved, e.g. "5 dokumen, 12 salinan" (empty when nothing).</summary>
            public string Moved { get; set; }
            /// <summary>Old units of this change with what still depends on them.</summary>
            public List<Impact> Impacts { get; set; } = new List<Impact>();
        }

        public class Map
        {
            public int Seq { get; set; }
            public string OldCode { get; set; }
            public string OldName { get; set; }
            public string OldParent { get; set; }
            public string NewLevel { get; set; }
            public string NewCode { get; set; }
            public string NewName { get; set; }
            public string NewParent { get; set; }
            public bool IsOwner { get; set; }
        }

        public class Impact
        {
            public string Level { get; set; }
            public string Code { get; set; }
            public string Parent { get; set; }
            /// <summary>Active, Inactive (since VALID_TO), NotInMaster.</summary>
            public string MasterState { get; set; }
            public DateTime? ValidTo { get; set; }
            public int DocumentsOwned { get; set; }
            public int DocumentsInApproval { get; set; }
            public int DocumentsNumbered { get; set; }
            public int CopiesHeld { get; set; }
            public int Users { get; set; }
            public int ActiveChildren { get; set; }
            /// <summary>Kept by the change (MOVE keeps the code): never deactivated here.</summary>
            public bool CodeKept { get; set; }
            public bool Blocked => DocumentsOwned + DocumentsInApproval + CopiesHeld + Users + ActiveChildren > 0;
        }

        // ------------------------------------------------------------------ read

        public static List<Change> List(DBContext db, bool includeCancelled, int? id = null)
        {
            var changes = Query(db, @"
                SELECT ORG_CHANGE_ID, CHANGE_TYPE, ORG_LEVEL, EFFECTIVE_DATE, HR_REFERENCE, DESCRIPTION, ATTACHMENT_PATH, ATTACHMENT_NAME,
                       [STATUS], CANCEL_REASON, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT
                FROM dbo.TB_R_ORG_CHANGE
                WHERE (@ALL = 1 OR [STATUS] = 1) AND (@ID IS NULL OR ORG_CHANGE_ID = @ID)
                ORDER BY CASE WHEN EFFECTIVE_DATE IS NULL THEN 0 ELSE 1 END, EFFECTIVE_DATE DESC, ORG_CHANGE_ID DESC",
                ("@ALL", includeCancelled ? 1 : 0), ("@ID", id)).Select(r => new Change
            {
                Id = Convert.ToInt32(r["ORG_CHANGE_ID"]),
                Type = r["CHANGE_TYPE"] as string,
                Level = r["ORG_LEVEL"] as string,
                EffectiveDate = r["EFFECTIVE_DATE"] as DateTime?,
                HrReference = r["HR_REFERENCE"] as string,
                Description = r["DESCRIPTION"] as string,
                AttachmentName = r["ATTACHMENT_NAME"] as string,
                HasAttachment = !string.IsNullOrEmpty(r["ATTACHMENT_PATH"] as string),
                Status = Convert.ToInt32(r["STATUS"]),
                CancelReason = r["CANCEL_REASON"] as string,
                CreatedBy = r["CREATED_BY"] as string,
                CreatedDt = Convert.ToDateTime(r["CREATED_DT"]),
                ChangedBy = r["CHANGED_BY"] as string,
                ChangedDt = r["CHANGED_DT"] as DateTime?
            }).ToList();
            if (changes.Count == 0) return changes;

            var byId = changes.ToDictionary(c => c.Id);
            foreach (var r in Query(db, @"
                SELECT ORG_CHANGE_ID, SEQ, OLD_CODE, OLD_NAME, OLD_PARENT, NEW_LEVEL, NEW_CODE, NEW_NAME, NEW_PARENT, IS_OWNER
                FROM dbo.TB_R_ORG_CHANGE_MAP WHERE (@ID IS NULL OR ORG_CHANGE_ID = @ID) ORDER BY ORG_CHANGE_ID, SEQ", ("@ID", id)))
            {
                if (!byId.TryGetValue(Convert.ToInt32(r["ORG_CHANGE_ID"]), out var c)) continue;
                c.Maps.Add(new Map
                {
                    Seq = Convert.ToInt32(r["SEQ"]),
                    OldCode = r["OLD_CODE"] as string,
                    OldName = r["OLD_NAME"] as string,
                    OldParent = r["OLD_PARENT"] as string,
                    NewLevel = r["NEW_LEVEL"] as string,
                    NewCode = r["NEW_CODE"] as string,
                    NewName = r["NEW_NAME"] as string,
                    NewParent = r["NEW_PARENT"] as string,
                    IsOwner = Convert.ToBoolean(r["IS_OWNER"])
                });
            }

            foreach (var c in changes)
            {
                c.Moved = OrgTransferService.MovedSummary(db, c.Id);
                var newCodes = new HashSet<string>(c.Maps.Where(m => m.NewCode != null && (m.NewLevel ?? c.Level) == c.Level)
                    .Select(m => m.NewCode), StringComparer.OrdinalIgnoreCase);
                foreach (var m in c.Maps.Where(m => m.OldCode != null)
                             .GroupBy(m => m.OldCode + "|" + m.OldParent, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
                {
                    var impact = LoadImpact(db, c.Level, m.OldCode, m.OldParent);
                    impact.CodeKept = newCodes.Contains(m.OldCode);
                    c.Impacts.Add(impact);
                }
            }
            return changes;
        }

        /// <summary>What still depends on one old unit (the same checks block Deactivate).</summary>
        public static Impact LoadImpact(DBContext db, string level, string code, string parent)
        {
            string sql = level switch
            {
                "DIVISION" => @"
                    SELECT
                      (SELECT TOP 1 CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 'Active' ELSE 'Inactive' END FROM dbo.TB_M_DIVISION WHERE DIVISION_CODE = @C AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)) AS MASTER_STATE,
                      (SELECT TOP 1 VALID_TO FROM dbo.TB_M_DIVISION WHERE DIVISION_CODE = @C AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)) AS VALID_TO,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT WHERE DIVISION = @C AND ISNULL(DELETE_FLAG, 0) = 0 AND [STATUS] IN ('1', '5')) AS OWNED,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT WHERE DIVISION = @C AND ISNULL(DELETE_FLAG, 0) = 0 AND [STATUS] IN ('0', '6')) AS IN_APPROVAL,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_CODE LIKE @C + '/%' AND ISNULL(DELETE_FLAG, 0) = 0 AND [STATUS] IN ('1', '5')) AS NUMBERED,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT_DISTRIBUTION DD JOIN dbo.TB_R_DOCUMENT D ON D.DOCUMENT_TRANSACTION_ID = DD.DOCUMENT_TRANSACTION_ID
                       WHERE DD.DIVISION = @C AND DD.[STATUS] = '1' AND ISNULL(D.DELETE_FLAG, 0) = 0 AND D.[STATUS] IN ('1', '5')) AS COPIES,
                      (SELECT COUNT(DISTINCT P.USERNAME) FROM dbo.TB_M_USER_POS P JOIN dbo.TB_M_USER U ON U.USERNAME = P.USERNAME
                       WHERE P.DIVISION = @C AND ISNULL(U.DELETE_FLAG, '0') <> '1') AS USERS,
                      (SELECT COUNT(*) FROM dbo.TB_M_DEPARTMENT WHERE DIVISION = @C AND ISNULL(DELETE_FLAG, 0) = 0) AS CHILDREN",
                "SECTION" => @"
                    SELECT
                      (SELECT TOP 1 CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 'Active' ELSE 'Inactive' END FROM dbo.TB_M_SECTION WHERE SECTION_CODE = @C AND DEPARTMENT_CODE = @P AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)) AS MASTER_STATE,
                      (SELECT TOP 1 VALID_TO FROM dbo.TB_M_SECTION WHERE SECTION_CODE = @C AND DEPARTMENT_CODE = @P AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)) AS VALID_TO,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT WHERE SECTION_CODE = @C AND DEPARTMENT_CODE = @P AND ISNULL(DELETE_FLAG, 0) = 0 AND [STATUS] IN ('1', '5')) AS OWNED,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT WHERE SECTION_CODE = @C AND DEPARTMENT_CODE = @P AND ISNULL(DELETE_FLAG, 0) = 0 AND [STATUS] IN ('0', '6')) AS IN_APPROVAL,
                      0 AS NUMBERED,
                      0 AS COPIES,
                      (SELECT COUNT(DISTINCT P.USERNAME) FROM dbo.TB_M_USER_POS P JOIN dbo.TB_M_SECTION S ON S.SECTION_ID = P.SECTION_ID
                       JOIN dbo.TB_M_USER U ON U.USERNAME = P.USERNAME
                       WHERE S.SECTION_CODE = @C AND S.DEPARTMENT_CODE = @P AND ISNULL(U.DELETE_FLAG, '0') <> '1') AS USERS,
                      0 AS CHILDREN",
                _ => @"
                    SELECT
                      (SELECT TOP 1 CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 'Active' ELSE 'Inactive' END FROM dbo.TB_M_DEPARTMENT WHERE DEPARTMENT_CODE = @C AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)) AS MASTER_STATE,
                      (SELECT TOP 1 VALID_TO FROM dbo.TB_M_DEPARTMENT WHERE DEPARTMENT_CODE = @C AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)) AS VALID_TO,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT D LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = D.DEPARTMENT
                       WHERE ISNULL(D.DEPARTMENT_CODE, M.DEPARTMENT_CODE) = @C AND ISNULL(D.DELETE_FLAG, 0) = 0 AND D.[STATUS] IN ('1', '5')) AS OWNED,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT D LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = D.DEPARTMENT
                       WHERE ISNULL(D.DEPARTMENT_CODE, M.DEPARTMENT_CODE) = @C AND ISNULL(D.DELETE_FLAG, 0) = 0 AND D.[STATUS] IN ('0', '6')) AS IN_APPROVAL,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_CODE LIKE '%-' + @C + '-%' AND ISNULL(DELETE_FLAG, 0) = 0 AND [STATUS] IN ('1', '5')) AS NUMBERED,
                      (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT_DISTRIBUTION DD JOIN dbo.TB_R_DOCUMENT D ON D.DOCUMENT_TRANSACTION_ID = DD.DOCUMENT_TRANSACTION_ID
                       LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = DD.DEPARTMENT_ID
                       WHERE ISNULL(DD.DEPARTMENT_CODE, M.DEPARTMENT_CODE) = @C AND DD.[STATUS] = '1' AND ISNULL(D.DELETE_FLAG, 0) = 0 AND D.[STATUS] IN ('1', '5')) AS COPIES,
                      (SELECT COUNT(DISTINCT P.USERNAME) FROM dbo.TB_M_USER_POS P JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = P.DEPARTMENT_ID
                       JOIN dbo.TB_M_USER U ON U.USERNAME = P.USERNAME
                       WHERE M.DEPARTMENT_CODE = @C AND ISNULL(U.DELETE_FLAG, '0') <> '1') AS USERS,
                      (SELECT COUNT(*) FROM dbo.TB_M_SECTION WHERE DEPARTMENT_CODE = @C AND ISNULL(DELETE_FLAG, 0) = 0) AS CHILDREN"
            };
            var r = Query(db, sql, ("@C", code), ("@P", parent)).First();
            return new Impact
            {
                Level = level,
                Code = code,
                Parent = parent,
                MasterState = r["MASTER_STATE"] as string ?? "NotInMaster",
                ValidTo = r["VALID_TO"] as DateTime?,
                DocumentsOwned = Convert.ToInt32(r["OWNED"]),
                DocumentsInApproval = Convert.ToInt32(r["IN_APPROVAL"]),
                DocumentsNumbered = Convert.ToInt32(r["NUMBERED"]),
                CopiesHeld = Convert.ToInt32(r["COPIES"]),
                Users = Convert.ToInt32(r["USERS"]),
                ActiveChildren = Convert.ToInt32(r["CHILDREN"])
            };
        }

        /// <summary>
        /// Former department code -> current department(s), the first owning the documents: valid
        /// department-level changes of the register, plus System Parameters DEPT_SUCCESSOR rows
        /// that are still active (switched off by the migration; kept as a fallback).
        /// </summary>
        public static Dictionary<string, string[]> LoadSuccessors(DBContext db)
        {
            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in Query(db, @"
                    SELECT M.OLD_CODE, M.NEW_CODE, M.IS_OWNER, M.SEQ, C.EFFECTIVE_DATE, C.ORG_CHANGE_ID
                    FROM dbo.TB_R_ORG_CHANGE_MAP M JOIN dbo.TB_R_ORG_CHANGE C ON C.ORG_CHANGE_ID = M.ORG_CHANGE_ID
                    WHERE C.[STATUS] = 1 AND C.ORG_LEVEL = 'DEPARTMENT' AND ISNULL(M.NEW_LEVEL, 'DEPARTMENT') = 'DEPARTMENT'
                      AND M.OLD_CODE IS NOT NULL AND M.NEW_CODE IS NOT NULL AND M.OLD_CODE <> M.NEW_CODE")
                .GroupBy(r => ((string)r["OLD_CODE"]).Trim().ToUpperInvariant()))
            {
                // the latest change of this old code wins; within it the owner comes first
                int latest = g.OrderByDescending(r => r["EFFECTIVE_DATE"] as DateTime? ?? DateTime.MinValue)
                              .ThenByDescending(r => Convert.ToInt32(r["ORG_CHANGE_ID"])).Select(r => Convert.ToInt32(r["ORG_CHANGE_ID"])).First();
                result[g.Key] = g.Where(r => Convert.ToInt32(r["ORG_CHANGE_ID"]) == latest)
                    .OrderByDescending(r => Convert.ToBoolean(r["IS_OWNER"])).ThenBy(r => Convert.ToInt32(r["SEQ"]))
                    .Select(r => ((string)r["NEW_CODE"]).Trim().ToUpperInvariant()).Distinct().ToArray();
            }
            foreach (var r in Query(db, @"SELECT SYSTEM_CODE, SYSTEM_VALUE FROM dbo.TB_M_SYSTEM
                                          WHERE SYSTEM_TYPE = 'DEPT_SUCCESSOR' AND ISNULL([STATUS], 1) = 1"))
            {
                string code = (r["SYSTEM_CODE"] as string ?? "").Trim().ToUpperInvariant();
                string value = r["SYSTEM_VALUE"] as string ?? "";
                if (code.Length == 0 || result.ContainsKey(code)) continue;
                var codes = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim().ToUpperInvariant()).ToArray();
                if (codes.Length > 0) result[code] = codes;
            }
            return result;
        }

        // ------------------------------------------------------------------ deactivate

        /// <summary>
        /// Deactivates the old units of a change in their master (DELETE_FLAG 1, VALID_TO = the day
        /// before the effective date). All or nothing: refused while any old unit still has documents,
        /// copies, users or active sub-units (moving them is tahap 2). Returns the deactivated codes.
        /// </summary>
        public static List<string> Deactivate(DBContext db, Change change, string user, out string error)
        {
            error = null;
            var done = new List<string>();
            if (change.Status != 1) { error = "This change is cancelled."; return done; }
            if (change.EffectiveDate == null) { error = "Fill in the effective date first: the old codes are valid until the day before it."; return done; }

            var targets = change.Impacts.Where(i => !i.CodeKept && i.MasterState == "Active").ToList();
            if (targets.Count == 0) { error = "No old code of this change is still active in the master."; return done; }

            var blocked = targets.Where(i => i.Blocked).Select(i => i.Code + " (" + Describe(i) + ")").ToList();
            if (blocked.Count > 0)
            {
                error = "Nothing was deactivated. Still in use: " + string.Join("; ", blocked)
                      + ". Move them to the new unit first (Organization Change tahap 2, or by hand in the related menus).";
                return done;
            }

            DateTime validTo = change.EffectiveDate.Value.Date.AddDays(-1);
            foreach (var i in targets)
            {
                string sql = i.Level switch
                {
                    "DIVISION" => "UPDATE dbo.TB_M_DIVISION SET DELETE_FLAG = 1, VALID_TO = @TO, CHANGED_BY = @U, CHANGED_DT = GETDATE() WHERE DIVISION_CODE = @C AND ISNULL(DELETE_FLAG, 0) = 0",
                    "SECTION" => "UPDATE dbo.TB_M_SECTION SET DELETE_FLAG = 1, VALID_TO = @TO, CHANGED_BY = @U, CHANGED_DT = GETDATE() WHERE SECTION_CODE = @C AND DEPARTMENT_CODE = @P AND ISNULL(DELETE_FLAG, 0) = 0",
                    _ => "UPDATE dbo.TB_M_DEPARTMENT SET DELETE_FLAG = 1, VALID_TO = @TO, CHANGED_BY = @U, CHANGED_DT = GETDATE() WHERE DEPARTMENT_CODE = @C AND ISNULL(DELETE_FLAG, 0) = 0"
                };
                Execute(db, sql, ("@TO", validTo), ("@U", user), ("@C", i.Code), ("@P", i.Parent));
                done.Add(i.Code);
            }
            return done;
        }

        public static string Describe(Impact i)
        {
            var parts = new List<string>();
            if (i.DocumentsOwned > 0) parts.Add(i.DocumentsOwned + " document(s) owned");
            if (i.DocumentsInApproval > 0) parts.Add(i.DocumentsInApproval + " in approval");
            if (i.CopiesHeld > 0) parts.Add(i.CopiesHeld + " copy record(s)");
            if (i.Users > 0) parts.Add(i.Users + " user(s)");
            if (i.ActiveChildren > 0) parts.Add(i.ActiveChildren + " active " + (i.Level == "DIVISION" ? "department(s)" : "section(s)"));
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts);
        }

        public static void WriteLog(DBContext db, int id, string action, string detail, string user) =>
            Execute(db, @"INSERT INTO dbo.TB_R_ORG_CHANGE_LOG (ORG_CHANGE_ID, [ACTION], DETAIL, ACTION_BY, ACTION_DT)
                          VALUES (@ID, @A, @D, @U, GETDATE())",
                ("@ID", id), ("@A", action), ("@D", detail != null && detail.Length > 2000 ? detail.Substring(0, 2000) : detail), ("@U", user));

        /// <summary>"QPD, QDT -> QPA" for logs and the report.</summary>
        public static string Summary(Change c)
        {
            string olds = string.Join(", ", c.Maps.Where(m => m.OldCode != null).Select(m => m.OldCode).Distinct(StringComparer.OrdinalIgnoreCase));
            string news = string.Join(", ", c.Maps.Where(m => m.NewCode != null).Select(m => m.NewCode).Distinct(StringComparer.OrdinalIgnoreCase));
            return (olds.Length > 0 ? olds : "(new)") + " -> " + (news.Length > 0 ? news : "(closed)");
        }

        // ------------------------------------------------------------------ sql helpers (join the open EF transaction)

        public static List<Dictionary<string, object>> Query(DBContext db, string sql, params (string name, object value)[] parameters)
        {
            var rows = new List<Dictionary<string, object>>();
            Run(db, sql, parameters, command =>
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

        public static object Scalar(DBContext db, string sql, params (string name, object value)[] parameters)
        {
            object result = null;
            Run(db, sql, parameters, command => result = command.ExecuteScalar());
            return result;
        }

        public static int Execute(DBContext db, string sql, params (string name, object value)[] parameters)
        {
            int affected = 0;
            Run(db, sql, parameters, command => affected = command.ExecuteNonQuery());
            return affected;
        }

        private static void Run(DBContext db, string sql, (string name, object value)[] parameters, Action<DbCommand> action)
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
