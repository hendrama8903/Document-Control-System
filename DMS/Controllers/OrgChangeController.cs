using DMS.Common.Controllers;
using DMS.Models;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    /// <summary>
    /// Master Data > Organization Change (tahap 1, 2026-11-19). Register of the organisation
    /// changes announced by HR, with what still depends on each old code and a guarded
    /// Deactivate. Logic and rules: Services/OrgChangeService. Report: OrgChangeReportController.
    /// </summary>
    public class OrgChangeController : BaseController
    {
        private const string MenuUrl = "/OrgChange/Index";
        public const string FunctionAdd = "ORGCHANGE-ADD";
        public const string FunctionEdit = "ORGCHANGE-EDIT";
        public const string FunctionCancel = "ORGCHANGE-CANCEL";
        public const string FunctionDeactivate = "ORGCHANGE-DEACTIVATE";
        public const string FunctionTransfer = "ORGCHANGE-TRANSFER";
        private const string AttachmentFolder = "/Upload/ATTACHMENT/ORG_CHANGE/";
        private static readonly string[] AttachmentTypes = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx", ".msg", ".eml" };

        private readonly DBContext db;
        private readonly IWebHostEnvironment env;

        public OrgChangeController(DBContext db, IWebHostEnvironment env)
        {
            this.db = db;
            this.env = env;
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
            ViewData["Cancel"] = Has(FunctionCancel);
            ViewData["Deactivate"] = Has(FunctionDeactivate);
            ViewData["Transfer"] = Has(FunctionTransfer);
            ViewData["Title"] = "Organization Change";
            return View();
        }

        private JsonResult DenyWithoutMenu() =>
            GetLoginUsername() == null ? Json(new { status = false, message = "Session expired. Please log in again." })
            : !HasMenuAccess(MenuUrl) ? Json(new { status = false, message = "You are not authorized to use this feature." })
            : null;

        private JsonResult Fail(string message) => Json(new { status = false, message });

        // ------------------------------------------------------------------ data

        /// <summary>Units of the master (active and inactive) for the code pickers.</summary>
        [HttpGet]
        public JsonResult Options()
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;

            object Unit(Dictionary<string, object> r) => new
            {
                code = r["CODE"] as string,
                name = r["NAME"] as string,
                parent = r["PARENT"] as string,
                active = Convert.ToInt32(r["ACTIVE"]) == 1
            };
            var divisions = OrgChangeService.Query(db, @"SELECT DIVISION_CODE AS CODE, DIVISION_NAME AS NAME, NULL AS PARENT,
                    CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 1 ELSE 0 END AS ACTIVE
                FROM dbo.TB_M_DIVISION WHERE ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY DIVISION_CODE").Select(Unit);
            var departments = OrgChangeService.Query(db, @"SELECT DEPARTMENT_CODE AS CODE, DEPARTMENT_NAME AS NAME, DIVISION AS PARENT,
                    CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 1 ELSE 0 END AS ACTIVE
                FROM dbo.TB_M_DEPARTMENT WHERE ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY DEPARTMENT_CODE").Select(Unit);
            var sections = OrgChangeService.Query(db, @"SELECT SECTION_CODE AS CODE, SECTION_NAME AS NAME, DEPARTMENT_CODE AS PARENT,
                    CASE WHEN ISNULL(DELETE_FLAG, 0) = 0 THEN 1 ELSE 0 END AS ACTIVE
                FROM dbo.TB_M_SECTION WHERE ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY DEPARTMENT_CODE, SECTION_CODE").Select(Unit);
            var types = OrgChangeService.Types.Select(t => new { id = t, name = OrgChangeService.TypeLabel(t) });
            return Json(new { status = true, divisions, departments, sections, types });
        }

        [HttpPost]
        public JsonResult Search(bool includeCancelled)
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;
            return Json(new { status = true, data = OrgChangeService.List(db, includeCancelled) });
        }

        [HttpGet]
        public JsonResult History(int id)
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;
            var rows = OrgChangeService.Query(db, @"
                SELECT L.[ACTION], L.DETAIL, ISNULL(U.FULL_NAME, L.ACTION_BY) AS ACTION_BY_NAME, L.ACTION_DT
                FROM dbo.TB_R_ORG_CHANGE_LOG L LEFT JOIN dbo.TB_M_USER U ON U.USERNAME = L.ACTION_BY
                WHERE L.ORG_CHANGE_ID = @ID ORDER BY L.ACTION_DT DESC, L.LOG_ID DESC", ("@ID", id)).Select(r => new
            {
                action = r["ACTION"] as string,
                detail = r["DETAIL"] as string,
                by = r["ACTION_BY_NAME"] as string,
                at = Convert.ToDateTime(r["ACTION_DT"])
            });
            return Json(new { status = true, data = rows });
        }

        /// <summary>The HR memo file. Only through here: /Upload/ATTACHMENT/ORG_CHANGE is closed to direct links.</summary>
        [HttpGet]
        public IActionResult Attachment(int id)
        {
            bool allowed = GetLoginUsername() != null
                && (HasMenuAccess(MenuUrl) || HasMenuAccess(OrgChangeReportController.MenuUrl));
            if (!allowed) return StatusCode(403);
            var r = OrgChangeService.Query(db, "SELECT ATTACHMENT_PATH, ATTACHMENT_NAME FROM dbo.TB_R_ORG_CHANGE WHERE ORG_CHANGE_ID = @ID", ("@ID", id)).FirstOrDefault();
            string full = UploadPath.Resolve(env.WebRootPath, r?["ATTACHMENT_PATH"] as string);
            if (full == null || !System.IO.File.Exists(full)) return NotFound();
            string name = r["ATTACHMENT_NAME"] as string ?? Path.GetFileName(full);
            new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider().TryGetContentType(name, out string contentType);
            return PhysicalFile(full, contentType ?? "application/octet-stream", name);
        }

        // ------------------------------------------------------------------ save

        public class MapInput
        {
            public string OldCode { get; set; }
            public string OldName { get; set; }
            public string OldParent { get; set; }
            public string NewCode { get; set; }
            public string NewParent { get; set; }
            public bool IsOwner { get; set; }
        }

        /// <summary>Add or edit one change. Multipart: maps as JSON (mapsJson), optional memo file.</summary>
        [HttpPost]
        [RequestSizeLimit(25 * 1024 * 1024)]
        public JsonResult Save(int? id, string changeType, string orgLevel, DateTime? effectiveDate, string hrReference,
            string description, string mapsJson, IFormFile attachment, bool removeAttachment)
        {
            JsonResult denied = DenyWithoutMenu() ?? DenyWithoutFunction(id == null ? FunctionAdd : FunctionEdit);
            if (denied != null) return denied;

            changeType = (changeType ?? "").Trim().ToUpperInvariant();
            orgLevel = (orgLevel ?? "").Trim().ToUpperInvariant();
            hrReference = (hrReference ?? "").Trim();
            description = (description ?? "").Trim();
            if (!OrgChangeService.Types.Contains(changeType)) return Fail("Choose the type of change.");
            if (!OrgChangeService.Levels.Contains(orgLevel)) return Fail("Choose the level (division, department or section).");
            if (changeType == "DOWNGRADE" && orgLevel != "DEPARTMENT") return Fail("'Department becomes a section' starts from a department.");
            if (effectiveDate == null) return Fail("Effective date is required (from the HR announcement).");
            if (hrReference.Length == 0) return Fail("HR reference is required, e.g. the memo or SK number.");
            if (hrReference.Length > 200) return Fail("HR reference is limited to 200 characters.");
            if (description.Length > 1000) return Fail("Description is limited to 1000 characters.");
            if (attachment != null)
            {
                string ext = Path.GetExtension(attachment.FileName ?? "").ToLowerInvariant();
                if (!AttachmentTypes.Contains(ext)) return Fail("The memo file must be PDF, image, Word, Excel or e-mail.");
            }

            List<MapInput> inputs;
            try
            {
                inputs = System.Text.Json.JsonSerializer.Deserialize<List<MapInput>>(mapsJson ?? "[]",
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<MapInput>();
            }
            catch (Exception)
            {
                return Fail("The code list could not be read. Refresh the page and try again.");
            }
            string Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
            inputs = inputs.Select(m => new MapInput
            {
                OldCode = Clean(m.OldCode), OldName = string.IsNullOrWhiteSpace(m.OldName) ? null : m.OldName.Trim(),
                OldParent = Clean(m.OldParent), NewCode = Clean(m.NewCode), NewParent = Clean(m.NewParent), IsOwner = m.IsOwner
            }).Where(m => m.OldCode != null || m.NewCode != null).ToList();

            string newLevel = changeType == "DOWNGRADE" ? "SECTION" : orgLevel;
            string problem = Validate(changeType, orgLevel, newLevel, inputs);
            if (problem != null) return Fail(problem);

            // names and parents from the master; an old code that is no longer there keeps the typed name
            var maps = new List<OrgChangeService.Map>();
            int seq = 1;
            foreach (var m in inputs)
            {
                var oldUnit = m.OldCode == null ? null : FindUnit(orgLevel, m.OldCode, m.OldParent);
                // a move: the master still has the unit under its old parent
                var newUnit = m.NewCode == null ? null : FindUnit(newLevel, m.NewCode, changeType == "MOVE" ? m.OldParent : m.NewParent);
                if (m.NewCode != null && newUnit == null)
                    return Fail("New code " + m.NewCode + (m.NewParent != null ? " (" + m.NewParent + ")" : "") + " is not in the "
                        + OrgChangeService.LevelLabel(newLevel) + " master. Add it there first, then record the change.");
                if (m.OldCode != null && oldUnit == null && orgLevel == "SECTION" && m.OldParent == null)
                    return Fail("Give the department of old section " + m.OldCode + ".");
                maps.Add(new OrgChangeService.Map
                {
                    Seq = seq++,
                    OldCode = m.OldCode,
                    OldName = oldUnit?.Name ?? m.OldName,
                    OldParent = m.OldParent ?? oldUnit?.Parent,
                    NewLevel = m.NewCode == null ? null : newLevel,
                    NewCode = m.NewCode,
                    NewName = newUnit?.Name,
                    // MOVE: the new parent is what the user chose; otherwise the master's
                    NewParent = changeType == "MOVE" ? m.NewParent : newUnit?.Parent,
                    IsOwner = m.NewCode != null && (changeType == "SPLIT" ? m.IsOwner : true)
                });
            }
            if (changeType == "MOVE" && maps.Any(m => string.Equals(m.OldParent, m.NewParent, StringComparison.OrdinalIgnoreCase)))
                return Fail("For a move, the new " + (orgLevel == "SECTION" ? "department" : "division") + " must differ from the current one.");

            string user = GetLoginUsername();
            try
            {
                using var tx = db.Database.BeginTransaction();
                if (id != null)
                {
                    var current = OrgChangeService.Query(db, "SELECT [STATUS] FROM dbo.TB_R_ORG_CHANGE WITH (UPDLOCK) WHERE ORG_CHANGE_ID = @ID", ("@ID", id)).FirstOrDefault();
                    if (current == null) return Fail("This change no longer exists. Refresh the list.");
                    if (Convert.ToInt32(current["STATUS"]) != 1) return Fail("A cancelled change cannot be edited.");
                }

                string path = null, fileName = null;
                if (attachment != null && attachment.Length > 0)
                {
                    fileName = Path.GetFileName(attachment.FileName);
                    path = AttachmentFolder + DateTime.Now.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8)
                         + Path.GetExtension(fileName).ToLowerInvariant();
                    string full = UploadPath.Resolve(env.WebRootPath, path);
                    if (full == null) return Fail("The memo file could not be stored.");
                    Directory.CreateDirectory(Path.GetDirectoryName(full));
                    using (var fs = System.IO.File.Create(full)) attachment.CopyTo(fs);
                }

                int savedId;
                if (id == null)
                {
                    savedId = Convert.ToInt32(OrgChangeService.Scalar(db, @"
                        INSERT INTO dbo.TB_R_ORG_CHANGE (CHANGE_TYPE, ORG_LEVEL, EFFECTIVE_DATE, HR_REFERENCE, DESCRIPTION, ATTACHMENT_PATH, ATTACHMENT_NAME, [STATUS], CREATED_BY, CREATED_DT)
                        VALUES (@T, @L, @E, @H, @D, @P, @N, 1, @U, GETDATE());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        ("@T", changeType), ("@L", orgLevel), ("@E", effectiveDate.Value.Date), ("@H", hrReference),
                        ("@D", description.Length > 0 ? description : null), ("@P", path), ("@N", fileName), ("@U", user)));
                }
                else
                {
                    savedId = id.Value;
                    OrgChangeService.Execute(db, @"
                        UPDATE dbo.TB_R_ORG_CHANGE
                        SET CHANGE_TYPE = @T, ORG_LEVEL = @L, EFFECTIVE_DATE = @E, HR_REFERENCE = @H, DESCRIPTION = @D,
                            ATTACHMENT_PATH = CASE WHEN @P IS NOT NULL THEN @P WHEN @RM = 1 THEN NULL ELSE ATTACHMENT_PATH END,
                            ATTACHMENT_NAME = CASE WHEN @P IS NOT NULL THEN @N WHEN @RM = 1 THEN NULL ELSE ATTACHMENT_NAME END,
                            CHANGED_BY = @U, CHANGED_DT = GETDATE()
                        WHERE ORG_CHANGE_ID = @ID",
                        ("@T", changeType), ("@L", orgLevel), ("@E", effectiveDate.Value.Date), ("@H", hrReference),
                        ("@D", description.Length > 0 ? description : null), ("@P", path), ("@N", fileName),
                        ("@RM", removeAttachment ? 1 : 0), ("@U", user), ("@ID", savedId));
                    OrgChangeService.Execute(db, "DELETE FROM dbo.TB_R_ORG_CHANGE_MAP WHERE ORG_CHANGE_ID = @ID", ("@ID", savedId));
                }

                foreach (var m in maps)
                {
                    OrgChangeService.Execute(db, @"
                        INSERT INTO dbo.TB_R_ORG_CHANGE_MAP (ORG_CHANGE_ID, SEQ, OLD_CODE, OLD_NAME, OLD_PARENT, NEW_LEVEL, NEW_CODE, NEW_NAME, NEW_PARENT, IS_OWNER)
                        VALUES (@ID, @S, @OC, @ON, @OP, @NL, @NC, @NN, @NP, @OW)",
                        ("@ID", savedId), ("@S", m.Seq), ("@OC", m.OldCode), ("@ON", m.OldName), ("@OP", m.OldParent),
                        ("@NL", m.NewLevel), ("@NC", m.NewCode), ("@NN", m.NewName), ("@NP", m.NewParent), ("@OW", m.IsOwner));
                }

                var saved = OrgChangeService.List(db, true, savedId).First();
                OrgChangeService.WriteLog(db, savedId, id == null ? "CREATE" : "EDIT",
                    saved.TypeLabel + " (" + saved.LevelLabel + "): " + OrgChangeService.Summary(saved)
                    + ", effective " + effectiveDate.Value.ToString("dd-MM-yyyy") + ", HR " + hrReference
                    + (fileName != null ? ", memo file " + fileName : removeAttachment ? ", memo file removed" : ""), user);
                tx.Commit();
                return Json(new { status = true, message = id == null ? "Organization change recorded." : "Organization change updated.", id = savedId });
            }
            catch (Exception ex)
            {
                return Fail("Saving failed: " + ex.Message);
            }
        }

        // Pair counts per type (QMS cases: rename, merger, split, move, department -> section, new, closed).
        private static string Validate(string type, string level, string newLevel, List<MapInput> maps)
        {
            int olds = maps.Count(m => m.OldCode != null), news = maps.Count(m => m.NewCode != null);
            bool everyPaired = maps.All(m => m.OldCode != null && m.NewCode != null);
            string unit = OrgChangeService.LevelLabel(level).ToLowerInvariant();
            switch (type)
            {
                case "RENAME":
                    return maps.Count == 1 && everyPaired ? null : "A rename has exactly one old code and one new code.";
                case "MERGE":
                    if (!everyPaired) return "Every row of a merger needs an old and a new code.";
                    if (maps.Select(m => m.NewCode).Distinct().Count() != 1) return "A merger goes into exactly one new " + unit + ".";
                    return olds >= 2 ? null : "A merger has at least two old codes.";
                case "SPLIT":
                    if (!everyPaired) return "Every row of a split needs an old and a new code.";
                    if (maps.Select(m => m.OldCode).Distinct().Count() != 1) return "A split starts from exactly one old " + unit + ".";
                    if (news < 2) return "A split has at least two new codes.";
                    return maps.Count(m => m.IsOwner) == 1 ? null : "Mark exactly one new " + unit + " as the default owner of the documents.";
                case "MOVE":
                    if (level == "DIVISION") return "A division cannot move; use rename or merger.";
                    if (!everyPaired || maps.Any(m => m.OldCode != m.NewCode)) return "A move keeps the code: fill the same code as old and new.";
                    return maps.All(m => m.NewParent != null) ? null : "Choose the new " + (level == "SECTION" ? "department" : "division") + ".";
                case "DOWNGRADE":
                    return maps.Count == 1 && everyPaired ? null : "One department becomes one section: fill the department and the new section.";
                case "NEW":
                    return olds == 0 && news >= 1 ? null : "A new unit has only new codes.";
                case "CLOSE":
                    return news == 0 && olds >= 1 ? null : "A closed unit has only old codes.";
            }
            return "Unknown type of change.";
        }

        private class Unit { public string Name; public string Parent; }

        private Unit FindUnit(string level, string code, string parent)
        {
            string sql = level switch
            {
                "DIVISION" => "SELECT TOP 1 DIVISION_NAME AS NAME, NULL AS PARENT FROM dbo.TB_M_DIVISION WHERE DIVISION_CODE = @C AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)",
                "SECTION" => "SELECT TOP 1 SECTION_NAME AS NAME, DEPARTMENT_CODE AS PARENT FROM dbo.TB_M_SECTION WHERE SECTION_CODE = @C AND (@P IS NULL OR DEPARTMENT_CODE = @P) AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)",
                _ => "SELECT TOP 1 DEPARTMENT_NAME AS NAME, DIVISION AS PARENT FROM dbo.TB_M_DEPARTMENT WHERE DEPARTMENT_CODE = @C AND ISNULL(DELETE_FLAG, 0) <> 2 ORDER BY ISNULL(DELETE_FLAG, 0)"
            };
            var r = OrgChangeService.Query(db, sql, ("@C", code), ("@P", parent)).FirstOrDefault();
            return r == null ? null : new Unit { Name = r["NAME"] as string, Parent = r["PARENT"] as string };
        }

        // ------------------------------------------------------------------ cancel / deactivate

        [HttpPost]
        public JsonResult Cancel(int id, string reason)
        {
            JsonResult denied = DenyWithoutMenu() ?? DenyWithoutFunction(FunctionCancel);
            if (denied != null) return denied;
            reason = (reason ?? "").Trim();
            if (reason.Length == 0) return Fail("Write why this change is cancelled.");
            if (reason.Length > 500) return Fail("Reason is limited to 500 characters.");
            try
            {
                using var tx = db.Database.BeginTransaction();
                string user = GetLoginUsername();
                int n = OrgChangeService.Execute(db, @"UPDATE dbo.TB_R_ORG_CHANGE SET [STATUS] = 0, CANCEL_REASON = @R, CHANGED_BY = @U, CHANGED_DT = GETDATE()
                                                    WHERE ORG_CHANGE_ID = @ID AND [STATUS] = 1", ("@R", reason), ("@U", user), ("@ID", id));
                if (n == 0) return Fail("This change is already cancelled or no longer exists. Refresh the list.");
                OrgChangeService.WriteLog(db, id, "CANCEL", "Cancelled: " + reason
                    + ". Codes already deactivated stay inactive; reactivate them in the master if needed.", user);
                tx.Commit();
                return Json(new { status = true, message = "Organization change cancelled. It is no longer used by the Master List import." });
            }
            catch (Exception ex)
            {
                return Fail("Cancelling failed: " + ex.Message);
            }
        }

        [HttpPost]
        public JsonResult Deactivate(int id)
        {
            JsonResult denied = DenyWithoutMenu() ?? DenyWithoutFunction(FunctionDeactivate);
            if (denied != null) return denied;
            try
            {
                using var tx = db.Database.BeginTransaction();
                var change = OrgChangeService.List(db, true, id).FirstOrDefault();
                if (change == null) return Fail("This change no longer exists. Refresh the list.");
                string user = GetLoginUsername();
                var done = OrgChangeService.Deactivate(db, change, user, out string error);
                if (error != null) return Fail(error);
                string validTo = change.EffectiveDate.Value.AddDays(-1).ToString("dd-MM-yyyy");
                OrgChangeService.WriteLog(db, id, "DEACTIVATE", "Deactivated in the " + change.LevelLabel + " master, valid until "
                    + validTo + ": " + string.Join(", ", done), user);
                tx.Commit();
                return Json(new { status = true, message = "Deactivated " + string.Join(", ", done) + " (valid until " + validTo + ")." });
            }
            catch (Exception ex)
            {
                return Fail("Deactivating failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ transfer (tahap 2)

        /// <summary>What still hangs on the old units, per kind, with the default new unit.</summary>
        [HttpGet]
        public JsonResult TransferPlan(int id)
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;
            return Json(new { status = true, plan = OrgTransferService.BuildPlan(db, id) });
        }

        /// <summary>Moves the ticked items (selectionsJson = [{Kind, Key, Target}]) in one transaction.</summary>
        [HttpPost]
        public JsonResult Transfer(int id, string selectionsJson)
        {
            JsonResult denied = DenyWithoutMenu() ?? DenyWithoutFunction(FunctionTransfer);
            if (denied != null) return denied;
            List<OrgTransferService.Selection> selections;
            try
            {
                selections = System.Text.Json.JsonSerializer.Deserialize<List<OrgTransferService.Selection>>(selectionsJson ?? "[]",
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception)
            {
                return Fail("The selection could not be read. Reload the list and try again.");
            }
            try
            {
                using var tx = db.Database.BeginTransaction();
                var result = OrgTransferService.Apply(db, id, selections, GetLoginUsername(), out string error);
                if (error != null) return Fail(error);
                tx.Commit();
                return Json(new
                {
                    status = true,
                    message = "Moved: " + string.Join(", ", result.Moved.Select(kv => kv.Value + " " + OrgTransferService.KindLabel(kv.Key)))
                              + ". Every change is kept in the move log of this organization change."
                });
            }
            catch (Exception ex)
            {
                return Fail("Moving failed, nothing was changed: " + ex.Message);
            }
        }

        /// <summary>Rows moved by this change with their old and new value.</summary>
        [HttpGet]
        public JsonResult Moves(int id)
        {
            JsonResult denied = DenyWithoutMenu();
            if (denied != null) return denied;
            var rows = OrgTransferService.Moves(db, id).Select(r => new
            {
                batch = Convert.ToInt32(r["BATCH_NO"]),
                kind = r["ITEM_KIND"] as string,
                label = r["ITEM_LABEL"] as string,
                table = r["TABLE_NAME"] as string,
                key = r["ROW_KEY"] as string,
                oldValue = r["OLD_VALUE"] as string,
                newValue = r["NEW_VALUE"] as string,
                by = r["MOVED_BY"] as string,
                at = Convert.ToDateTime(r["MOVED_DT"])
            });
            return Json(new { status = true, data = rows });
        }
    }
}
