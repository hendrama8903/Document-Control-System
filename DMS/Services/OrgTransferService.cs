using DMS.Models;

namespace DMS.Services
{
    /// <summary>
    /// Organization Change, tahap 2 (2026-11-19): move what still hangs on an old unit to the new
    /// one, run by Document Control. QMS: the department of origin decides where each document of
    /// a split goes; copies only need their record moved (no withdraw and reissue); approvals may
    /// go on with the old approver (Reassign Approver is there to change it); users are only
    /// listed - HR moves people, an admin updates User Management.
    ///
    /// <see cref="BuildPlan"/> lists the items per kind with a default target; <see cref="Apply"/>
    /// moves the chosen ones in one transaction and keeps every row's old and new value in
    /// TB_R_ORG_CHANGE_MOVE (SQL/OrgChange_Transfer_Migration.sql). Document numbers never change.
    /// </summary>
    public static class OrgTransferService
    {
        // ------------------------------------------------------------------ model

        public class Target
        {
            public string Key { get; set; }
            public string Label { get; set; }
            public int? DeptId { get; set; }
            public string DeptCode { get; set; }
            public string DeptName { get; set; }
            public string Division { get; set; }
            public string DivisionName { get; set; }
            public string SectionCode { get; set; }
            public string SectionName { get; set; }
        }

        public class Item
        {
            public string Kind { get; set; }
            public string Key { get; set; }
            public string Label { get; set; }
            public string Detail { get; set; }
            public string OldUnit { get; set; }
            public string DefaultTarget { get; set; }
        }

        public class Group
        {
            public string Kind { get; set; }
            public string Title { get; set; }
            public string Hint { get; set; }
            public bool InfoOnly { get; set; }
            public List<Item> Items { get; set; } = new List<Item>();
        }

        public class Plan
        {
            public int ChangeId { get; set; }
            public string Summary { get; set; }
            public string Level { get; set; }
            public string Type { get; set; }
            public bool Supported { get; set; } = true;
            public string Message { get; set; }
            /// <summary>The effective date is still ahead: moving now is allowed but early.</summary>
            public bool BeforeEffective { get; set; }
            /// <summary>Several targets: the user may pick one per item (split, closed unit).</summary>
            public bool ChooseTarget { get; set; }
            public List<Target> Targets { get; set; } = new List<Target>();
            public List<Group> Groups { get; set; } = new List<Group>();
        }

        public class Selection
        {
            public string Kind { get; set; }
            public string Key { get; set; }
            public string Target { get; set; }
        }

        private static readonly string[] MovableKinds = { "DOCUMENT", "EXTDOC", "COPY", "BINDER", "PICSCOPE", "REFACCESS", "MASTER", "DEPARTMENT" };

        private static string S(object o) => o == null || o == DBNull.Value ? null : Convert.ToString(o);
        private static string D(object o) => o is DateTime d ? d.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture) : "";
        private static List<Dictionary<string, object>> Q(DBContext db, string sql, params (string, object)[] p) => OrgChangeService.Query(db, sql, p);
        private static int X(DBContext db, string sql, params (string, object)[] p) => OrgChangeService.Execute(db, sql, p);

        private static string StatusName(string s) => s switch
        {
            "0" => "Waiting Approval", "1" => "Approved", "2" => "Rejected", "5" => "Published", "6" => "Waiting Acknowledgment", _ => s
        };

        // ------------------------------------------------------------------ targets

        private static Target DeptTarget(DBContext db, string code)
        {
            var r = Q(db, @"SELECT TOP 1 D.DEPARTMENT_ID, D.DEPARTMENT_CODE, D.DEPARTMENT_NAME, D.DIVISION, V.DIVISION_NAME
                            FROM dbo.TB_M_DEPARTMENT D LEFT JOIN dbo.TB_M_DIVISION V ON V.DIVISION_CODE = D.DIVISION AND ISNULL(V.DELETE_FLAG, 0) <> 2
                            WHERE D.DEPARTMENT_CODE = @C AND ISNULL(D.DELETE_FLAG, 0) = 0", ("@C", code)).FirstOrDefault();
            return r == null ? null : new Target
            {
                Key = S(r["DEPARTMENT_CODE"]),
                Label = S(r["DEPARTMENT_CODE"]) + " - " + S(r["DEPARTMENT_NAME"]) + " (" + S(r["DIVISION"]) + ")",
                DeptId = Convert.ToInt32(r["DEPARTMENT_ID"]),
                DeptCode = S(r["DEPARTMENT_CODE"]),
                DeptName = S(r["DEPARTMENT_NAME"]),
                Division = S(r["DIVISION"]),
                DivisionName = S(r["DIVISION_NAME"])
            };
        }

        private static Target DivisionTarget(DBContext db, string code)
        {
            var r = Q(db, "SELECT TOP 1 DIVISION_CODE, DIVISION_NAME FROM dbo.TB_M_DIVISION WHERE DIVISION_CODE = @C AND ISNULL(DELETE_FLAG, 0) = 0", ("@C", code)).FirstOrDefault();
            return r == null ? null : new Target { Key = S(r["DIVISION_CODE"]), Label = S(r["DIVISION_CODE"]) + " - " + S(r["DIVISION_NAME"]), Division = S(r["DIVISION_CODE"]), DivisionName = S(r["DIVISION_NAME"]) };
        }

        private static Target SectionTarget(DBContext db, string dept, string code)
        {
            var d = DeptTarget(db, dept);
            if (d == null) return null;
            var r = Q(db, "SELECT TOP 1 SECTION_CODE, SECTION_NAME FROM dbo.TB_M_SECTION WHERE DEPARTMENT_CODE = @D AND SECTION_CODE = @C AND ISNULL(DELETE_FLAG, 0) = 0",
                ("@D", dept), ("@C", code)).FirstOrDefault();
            if (r == null) return null;
            d.Key = dept + "/" + S(r["SECTION_CODE"]);
            d.Label = dept + " / " + S(r["SECTION_CODE"]) + " - " + S(r["SECTION_NAME"]);
            d.SectionCode = S(r["SECTION_CODE"]);
            d.SectionName = S(r["SECTION_NAME"]);
            return d;
        }

        private static List<Target> AllTargets(DBContext db, string level, string sameDept)
        {
            string sql = level switch
            {
                "DIVISION" => "SELECT DIVISION_CODE AS C, NULL AS P FROM dbo.TB_M_DIVISION WHERE ISNULL(DELETE_FLAG, 0) = 0 ORDER BY DIVISION_CODE",
                "SECTION" => "SELECT SECTION_CODE AS C, DEPARTMENT_CODE AS P FROM dbo.TB_M_SECTION WHERE ISNULL(DELETE_FLAG, 0) = 0 AND DEPARTMENT_CODE = @P ORDER BY SECTION_CODE",
                _ => "SELECT DEPARTMENT_CODE AS C, NULL AS P FROM dbo.TB_M_DEPARTMENT WHERE ISNULL(DELETE_FLAG, 0) = 0 ORDER BY DEPARTMENT_CODE"
            };
            return Q(db, sql, ("@P", sameDept)).Select(r => level == "DIVISION" ? DivisionTarget(db, S(r["C"]))
                : level == "SECTION" ? SectionTarget(db, S(r["P"]), S(r["C"])) : DeptTarget(db, S(r["C"])))
                .Where(t => t != null).ToList();
        }

        // ------------------------------------------------------------------ plan

        public static Plan BuildPlan(DBContext db, int changeId)
        {
            var change = OrgChangeService.List(db, true, changeId).FirstOrDefault();
            if (change == null) return new Plan { ChangeId = changeId, Supported = false, Message = "This change no longer exists." };
            var plan = new Plan
            {
                ChangeId = changeId,
                Summary = change.TypeLabel + " (" + change.LevelLabel + "): " + OrgChangeService.Summary(change),
                Level = change.Level,
                Type = change.Type,
                BeforeEffective = change.EffectiveDate.HasValue && change.EffectiveDate.Value.Date > DateTime.Today
            };
            if (change.Status != 1) { plan.Supported = false; plan.Message = "This change is cancelled."; return plan; }
            if (change.Type == "NEW") { plan.Supported = false; plan.Message = "A new unit has nothing to move."; return plan; }
            if (change.Type == "MOVE" && change.Level == "SECTION")
            {
                plan.Supported = false;
                plan.Message = "A section that moves to another department is changed in Section Master (department of the section); its documents keep their department.";
                return plan;
            }

            // targets and the default target per old code
            var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (change.Type == "CLOSE")
            {
                string sameDept = change.Level == "SECTION" ? change.Maps.Select(m => m.OldParent).FirstOrDefault(p => p != null) : null;
                plan.Targets = AllTargets(db, change.Level, sameDept);
                plan.ChooseTarget = true;
            }
            else
            {
                foreach (var m in change.Maps.Where(m => m.NewCode != null))
                {
                    Target t = change.Type == "MOVE" ? (change.Level == "DEPARTMENT" ? MoveTarget(db, m) : null)
                        : change.Type == "DOWNGRADE" ? SectionTarget(db, m.NewParent, m.NewCode)
                        : change.Level == "DIVISION" ? DivisionTarget(db, m.NewCode)
                        : change.Level == "SECTION" ? SectionTarget(db, m.NewParent, m.NewCode)
                        : DeptTarget(db, m.NewCode);
                    if (t == null) continue;
                    if (!plan.Targets.Any(x => x.Key == t.Key)) plan.Targets.Add(t);
                    string oldKey = OldKey(change.Level, m.OldCode, m.OldParent);
                    if (m.OldCode != null && (m.IsOwner || !defaults.ContainsKey(oldKey))) defaults[oldKey] = t.Key;
                }
                plan.ChooseTarget = plan.Targets.Count > 1;
                if (plan.Targets.Count == 0)
                {
                    plan.Supported = false;
                    plan.Message = "The new unit of this change is not active in the master. Activate it (or fix the change) first.";
                    return plan;
                }
            }

            foreach (var m in change.Maps.Where(m => m.OldCode != null)
                         .GroupBy(m => OldKey(change.Level, m.OldCode, m.OldParent), StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            {
                string key = OldKey(change.Level, m.OldCode, m.OldParent);
                defaults.TryGetValue(key, out string def);
                if (change.Level == "DEPARTMENT") AddDepartmentItems(db, plan, change, m.OldCode, def);
                else if (change.Level == "DIVISION") AddDivisionItems(db, plan, m.OldCode, def);
                else AddSectionItems(db, plan, m.OldParent, m.OldCode, def);
            }
            plan.Groups = plan.Groups.Where(g => g.Items.Count > 0).ToList();
            return plan;
        }

        private static string OldKey(string level, string code, string parent) => level == "SECTION" ? parent + "/" + code : code;

        // a department that moves to another division keeps its code; the target carries the new division
        private static Target MoveTarget(DBContext db, OrgChangeService.Map m)
        {
            var t = Q(db, @"SELECT TOP 1 D.DEPARTMENT_ID, D.DEPARTMENT_CODE, D.DEPARTMENT_NAME FROM dbo.TB_M_DEPARTMENT D
                            WHERE D.DEPARTMENT_CODE = @C AND ISNULL(D.DELETE_FLAG, 0) = 0", ("@C", m.NewCode)).FirstOrDefault();
            var v = DivisionTarget(db, m.NewParent);
            if (t == null || v == null) return null;
            return new Target
            {
                Key = S(t["DEPARTMENT_CODE"]) + "@" + v.Division,
                Label = S(t["DEPARTMENT_CODE"]) + " - " + S(t["DEPARTMENT_NAME"]) + " (to " + v.Division + ")",
                DeptId = Convert.ToInt32(t["DEPARTMENT_ID"]),
                DeptCode = S(t["DEPARTMENT_CODE"]),
                DeptName = S(t["DEPARTMENT_NAME"]),
                Division = v.Division,
                DivisionName = v.DivisionName
            };
        }

        private static Group GroupOf(Plan plan, string kind, string title, string hint, bool infoOnly = false)
        {
            var g = plan.Groups.FirstOrDefault(x => x.Kind == kind);
            if (g == null) plan.Groups.Add(g = new Group { Kind = kind, Title = title, Hint = hint, InfoOnly = infoOnly });
            return g;
        }

        // the department's rows in the master (it may be inactive, or gone: then only snapshots match)
        private const string OldDeptIds = "SELECT DEPARTMENT_ID FROM dbo.TB_M_DEPARTMENT WHERE DEPARTMENT_CODE = @C";

        private static void AddDepartmentItems(DBContext db, Plan plan, OrgChangeService.Change change, string code, string def)
        {
            bool move = change.Type == "MOVE";
            string docHint = move ? "The documents stay with the department; their division becomes the new one."
                : change.Type == "DOWNGRADE" ? "The documents go to the department that now holds the section, with that section."
                : "Owner of the document (who revises and reviews it). The number does not change. With the open P4D registration.";

            // a move keeps the department: the Department Master item carries the new division to its
            // documents, copies and users, so documents are not listed one by one
            if (!move)
            foreach (var r in Q(db, @"
                SELECT D.DOCUMENT_TRANSACTION_ID, D.DOCUMENT_CODE, D.DOCUMENT_TRANSACTION_NAME, D.REVISION, D.[STATUS], D.SECTION_CODE
                FROM dbo.TB_R_DOCUMENT D LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = D.DEPARTMENT
                WHERE ISNULL(D.DEPARTMENT_CODE, M.DEPARTMENT_CODE) = @C AND ISNULL(D.DELETE_FLAG, 0) = 0 AND ISNULL(D.[STATUS], '0') <> '4'
                ORDER BY D.DOCUMENT_CODE", ("@C", code)))
                GroupOf(plan, "DOCUMENT", "Documents Owned", docHint).Items.Add(new Item
                {
                    Kind = "DOCUMENT", Key = S(r["DOCUMENT_TRANSACTION_ID"]), OldUnit = code, DefaultTarget = def,
                    Label = S(r["DOCUMENT_CODE"]) + " - " + S(r["DOCUMENT_TRANSACTION_NAME"]),
                    Detail = "Rev. " + S(r["REVISION"]) + " · " + StatusName(S(r["STATUS"])) + (S(r["SECTION_CODE"]) != null ? " · section " + S(r["SECTION_CODE"]) : "")
                });

            if (!move)
            foreach (var r in Q(db, @"
                SELECT E.EXTERNAL_DOCUMENT_ID, E.DOCUMENT_NAME, E.REVISION_EDITION
                FROM dbo.TB_M_EXTERNAL_DOCUMENT E LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = E.DEPARTMENT_ID
                WHERE ISNULL(E.DEPARTMENT_CODE, M.DEPARTMENT_CODE) = @C AND ISNULL(E.DELETE_FLAG, 0) = 0
                ORDER BY E.DOCUMENT_NAME", ("@C", code)))
                GroupOf(plan, "EXTDOC", "External Documents Owned", "External documents (standards, customer documents) the department keeps and reviews.").Items.Add(new Item
                {
                    Kind = "EXTDOC", Key = S(r["EXTERNAL_DOCUMENT_ID"]), OldUnit = code, DefaultTarget = def,
                    Label = S(r["DOCUMENT_NAME"]), Detail = S(r["REVISION_EDITION"]) != null ? "Edition " + S(r["REVISION_EDITION"]) : null
                });

            if (!move)
            {
                foreach (var r in Q(db, @"
                    SELECT DD.DISTRIBUTION_ID, D.DOCUMENT_CODE, D.REVISION, DD.DISTRIBUTION_DATE, DD.[STATUS],
                           (SELECT MAX(PH.CREATED_DT) FROM dbo.TB_R_PUBLISH_HISTORY PH JOIN dbo.TB_R_CTRL_DOCUMENT CT ON CT.DOCUMENT_CTRL_ID = PH.DOCUMENT_CTRL_ID
                            WHERE CT.DOCUMENT_TRANSACTION_ID = DD.DOCUMENT_TRANSACTION_ID AND PH.DEPARTMENT_ID = DD.DEPARTMENT_ID) AS RECEIVED_DT
                    FROM dbo.TB_R_DOCUMENT_DISTRIBUTION DD
                    JOIN dbo.TB_R_DOCUMENT D ON D.DOCUMENT_TRANSACTION_ID = DD.DOCUMENT_TRANSACTION_ID
                    LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = DD.DEPARTMENT_ID
                    WHERE ISNULL(DD.DEPARTMENT_CODE, M.DEPARTMENT_CODE) = @C AND ISNULL(D.DELETE_FLAG, 0) = 0 AND ISNULL(D.[STATUS], '0') <> '4'
                    ORDER BY D.DOCUMENT_CODE", ("@C", code)))
                    GroupOf(plan, "COPY", "Copies Held", "Only the record moves (QMS): the new department becomes the holder, with its acknowledgment. No withdraw and reissue. A department that already holds the same document then holds two copies.").Items.Add(new Item
                    {
                        Kind = "COPY", Key = S(r["DISTRIBUTION_ID"]), OldUnit = code, DefaultTarget = def,
                        Label = S(r["DOCUMENT_CODE"]) + " Rev. " + S(r["REVISION"]),
                        Detail = (S(r["STATUS"]) == "1" ? "sent " + D(r["DISTRIBUTION_DATE"]) : "registered, not sent yet")
                                 + (r["RECEIVED_DT"] != null ? " · acknowledged " + D(r["RECEIVED_DT"]) : "")
                    });

                foreach (var r in Q(db, @"
                    SELECT F.FOLDER_ID, F.FOLDER_NAME, F.FOLDER_CODE, F.FOLDER_TYPE, F.LOCATION FROM dbo.TB_M_DOCUMENT_FOLDER F
                    WHERE F.DEPARTMENT_ID IN (" + OldDeptIds + ") AND ISNULL(F.DELETE_FLAG, 0) = 0 ORDER BY F.FOLDER_NAME", ("@C", code)))
                    GroupOf(plan, "BINDER", "Binders Held", "Bantex folders whose holder is the old department.").Items.Add(new Item
                    {
                        Kind = "BINDER", Key = S(r["FOLDER_ID"]), OldUnit = code, DefaultTarget = def,
                        Label = (S(r["FOLDER_CODE"]) != null ? S(r["FOLDER_CODE"]) + " - " : "") + S(r["FOLDER_NAME"]),
                        Detail = string.Join(" · ", new[] { S(r["FOLDER_TYPE"]), S(r["LOCATION"]) }.Where(x => !string.IsNullOrEmpty(x)))
                    });

                foreach (var r in Q(db, @"
                    SELECT S.USERNAME, S.DEPARTMENT_ID, U.FULL_NAME FROM dbo.TB_M_USER_DEPT_SCOPE S LEFT JOIN dbo.TB_M_USER U ON U.USERNAME = S.USERNAME
                    WHERE S.DEPARTMENT_ID IN (" + OldDeptIds + ") ORDER BY U.FULL_NAME", ("@C", code)))
                    GroupOf(plan, "PICSCOPE", "PIC Departments Handled", "Users who prepare documents for the old department (Departments Handled) will handle the new one instead.").Items.Add(new Item
                    {
                        Kind = "PICSCOPE", Key = S(r["USERNAME"]) + "|" + S(r["DEPARTMENT_ID"]), OldUnit = code, DefaultTarget = def,
                        Label = (S(r["FULL_NAME"]) ?? S(r["USERNAME"])) + " (" + S(r["USERNAME"]) + ")"
                    });

                foreach (var r in Q(db, @"
                    SELECT RA.REFERENCE_ACCESS_ID, RA.VIEWER_USERNAME, U.FULL_NAME, RA.VALID_TO FROM dbo.TB_M_REFERENCE_ACCESS RA
                    LEFT JOIN dbo.TB_M_USER U ON U.USERNAME = RA.VIEWER_USERNAME
                    WHERE RA.OWNER_DEPARTMENT_ID IN (" + OldDeptIds + ") AND RA.[STATUS] = 1 AND RA.VALID_TO >= CAST(GETDATE() AS DATE)", ("@C", code)))
                    GroupOf(plan, "REFACCESS", "Reference Access", "Readers who may read the old department's documents will read the new owner's instead.").Items.Add(new Item
                    {
                        Kind = "REFACCESS", Key = S(r["REFERENCE_ACCESS_ID"]), OldUnit = code, DefaultTarget = def,
                        Label = (S(r["FULL_NAME"]) ?? S(r["VIEWER_USERNAME"])) + " (" + S(r["VIEWER_USERNAME"]) + ")", Detail = "until " + D(r["VALID_TO"])
                    });
            }
            else
            {
                foreach (var r in Q(db, @"SELECT D.DEPARTMENT_ID, D.DEPARTMENT_CODE, D.DEPARTMENT_NAME, D.DIVISION FROM dbo.TB_M_DEPARTMENT D
                                          WHERE D.DEPARTMENT_CODE = @C AND ISNULL(D.DELETE_FLAG, 0) = 0", ("@C", code)))
                    GroupOf(plan, "MASTER", "Department Master", "The department moves to the new division in Department Master; copies it holds and its users follow.").Items.Add(new Item
                    {
                        Kind = "MASTER", Key = S(r["DEPARTMENT_ID"]), OldUnit = code, DefaultTarget = def,
                        Label = S(r["DEPARTMENT_CODE"]) + " - " + S(r["DEPARTMENT_NAME"]), Detail = "now in " + S(r["DIVISION"])
                    });
            }

            AddApprovalsInfo(db, plan, @"SELECT D.DOCUMENT_TRANSACTION_ID FROM dbo.TB_R_DOCUMENT D LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = D.DEPARTMENT
                                         WHERE ISNULL(D.DEPARTMENT_CODE, M.DEPARTMENT_CODE) = @C", ("@C", code), code);
            AddUsersInfo(db, plan, "M.DEPARTMENT_CODE = @C", ("@C", code), code);
        }

        private static void AddDivisionItems(DBContext db, Plan plan, string code, string def)
        {
            foreach (var r in Q(db, @"
                SELECT D.DEPARTMENT_ID, D.DEPARTMENT_CODE, D.DEPARTMENT_NAME, ISNULL(D.DELETE_FLAG, 0) AS DELETE_FLAG,
                       (SELECT COUNT(*) FROM dbo.TB_R_DOCUMENT X WHERE X.DEPARTMENT = D.DEPARTMENT_ID AND ISNULL(X.DELETE_FLAG, 0) = 0 AND ISNULL(X.[STATUS], '0') <> '4') AS DOCS
                FROM dbo.TB_M_DEPARTMENT D WHERE D.DIVISION = @C AND ISNULL(D.DELETE_FLAG, 0) <> 2 ORDER BY D.DEPARTMENT_CODE", ("@C", code)))
                GroupOf(plan, "DEPARTMENT", "Departments", "Each department moves to the new division in Department Master; its documents, external documents, copies held and user positions get the new division.").Items.Add(new Item
                {
                    Kind = "DEPARTMENT", Key = S(r["DEPARTMENT_ID"]), OldUnit = code, DefaultTarget = def,
                    Label = S(r["DEPARTMENT_CODE"]) + " - " + S(r["DEPARTMENT_NAME"]),
                    Detail = r["DOCS"] + " document(s)" + (Convert.ToInt32(r["DELETE_FLAG"]) == 1 ? " · inactive" : "")
                });
            AddUsersInfo(db, plan, "P.DIVISION = @C", ("@C", code), code);
        }

        private static void AddSectionItems(DBContext db, Plan plan, string dept, string code, string def)
        {
            foreach (var r in Q(db, @"
                SELECT D.DOCUMENT_TRANSACTION_ID, D.DOCUMENT_CODE, D.DOCUMENT_TRANSACTION_NAME, D.REVISION, D.[STATUS]
                FROM dbo.TB_R_DOCUMENT D
                WHERE D.SECTION_CODE = @C AND D.DEPARTMENT_CODE = @P AND ISNULL(D.DELETE_FLAG, 0) = 0 AND ISNULL(D.[STATUS], '0') <> '4'
                ORDER BY D.DOCUMENT_CODE", ("@C", code), ("@P", dept)))
                GroupOf(plan, "DOCUMENT", "Documents of the Section", "The document gets the new section (and its department, when the section belongs to another one). The number does not change.").Items.Add(new Item
                {
                    Kind = "DOCUMENT", Key = S(r["DOCUMENT_TRANSACTION_ID"]), OldUnit = dept + "/" + code, DefaultTarget = def,
                    Label = S(r["DOCUMENT_CODE"]) + " - " + S(r["DOCUMENT_TRANSACTION_NAME"]),
                    Detail = "Rev. " + S(r["REVISION"]) + " · " + StatusName(S(r["STATUS"]))
                });
            AddUsersInfo(db, plan, "EXISTS (SELECT 1 FROM dbo.TB_M_SECTION S WHERE S.SECTION_ID = P.SECTION_ID AND S.SECTION_CODE = @C AND S.DEPARTMENT_CODE = @P)",
                ("@C", code), dept + "/" + code, ("@P", dept));
        }

        private static void AddApprovalsInfo(DBContext db, Plan plan, string docIdsSql, (string, object) p, string oldUnit)
        {
            foreach (var r in Q(db, @"
                SELECT D.DOCUMENT_CODE, D.DOCUMENT_TRANSACTION_NAME, H.CURRENT_SEQ, AD.APPROVER, U.FULL_NAME, AD.LABEL
                FROM dbo.TB_R_DOCUMENT D
                JOIN dbo.TB_R_APPROVAL_H H ON H.APPROVAL_ID = D.APPROVAL_ID
                LEFT JOIN dbo.TB_R_APPROVAL_D AD ON AD.APPROVAL_ID = H.APPROVAL_ID AND AD.WORKFLOW_SEQ = H.CURRENT_SEQ
                LEFT JOIN dbo.TB_M_USER U ON U.USERNAME = AD.APPROVER
                WHERE D.DOCUMENT_TRANSACTION_ID IN (" + docIdsSql + @") AND ISNULL(D.DELETE_FLAG, 0) = 0 AND D.[STATUS] = '0'
                ORDER BY D.DOCUMENT_CODE", p))
                GroupOf(plan, "APPROVAL", "Approvals in Progress", "They go on with the approver already chosen (QMS: old or new, both fine). To give a step to a new approver use Administration > Reassign Approver.", true).Items.Add(new Item
                {
                    Kind = "APPROVAL", OldUnit = oldUnit,
                    Label = S(r["DOCUMENT_CODE"]) + " - " + S(r["DOCUMENT_TRANSACTION_NAME"]),
                    Detail = "step " + S(r["CURRENT_SEQ"]) + (S(r["LABEL"]) != null ? " (" + S(r["LABEL"]) + ")" : "") + ": " + (S(r["FULL_NAME"]) ?? S(r["APPROVER"]) ?? "-")
                });
        }

        private static void AddUsersInfo(DBContext db, Plan plan, string where, (string, object) p, string oldUnit, (string, object)? p2 = null)
        {
            var ps = p2.HasValue ? new[] { p, p2.Value } : new[] { p };
            foreach (var r in Q(db, @"
                SELECT DISTINCT U.USERNAME, U.FULL_NAME, PO.POSITION_NAME, M.DEPARTMENT_CODE, P.DIVISION
                FROM dbo.TB_M_USER_POS P
                JOIN dbo.TB_M_USER U ON U.USERNAME = P.USERNAME AND ISNULL(U.DELETE_FLAG, '0') <> '1'
                LEFT JOIN dbo.TB_M_DEPARTMENT M ON M.DEPARTMENT_ID = P.DEPARTMENT_ID
                LEFT JOIN dbo.TB_M_POSITION PO ON PO.POSITION_ID = P.POSITION_ID
                WHERE " + where + " ORDER BY U.FULL_NAME", ps))
                GroupOf(plan, "USER", "Users", "Not moved here (decision 2026-11-19): HR decides who goes where; update their position in User Management. Users left on an old unit block its deactivation.", true).Items.Add(new Item
                {
                    Kind = "USER", OldUnit = oldUnit,
                    Label = (S(r["FULL_NAME"]) ?? S(r["USERNAME"])) + " (" + S(r["USERNAME"]) + ")",
                    Detail = string.Join(" · ", new[] { S(r["POSITION_NAME"]), S(r["DIVISION"]), S(r["DEPARTMENT_CODE"]) }.Where(x => !string.IsNullOrEmpty(x)))
                });
        }

        // ------------------------------------------------------------------ apply

        public class Result
        {
            public int Batch { get; set; }
            public Dictionary<string, int> Moved { get; set; } = new Dictionary<string, int>();
            public int Total => Moved.Values.Sum();
        }

        /// <summary>
        /// Moves the chosen items. Each selection must be an item of the current plan and its target
        /// one of the plan's targets. Runs inside the caller's transaction.
        /// </summary>
        public static Result Apply(DBContext db, int changeId, List<Selection> selections, string user, out string error)
        {
            error = null;
            var plan = BuildPlan(db, changeId);
            if (!plan.Supported) { error = plan.Message; return null; }
            var items = plan.Groups.Where(g => !g.InfoOnly).SelectMany(g => g.Items).ToDictionary(i => i.Kind + "#" + i.Key);
            var targets = plan.Targets.ToDictionary(t => t.Key, StringComparer.OrdinalIgnoreCase);

            var work = new List<(Item item, Target target)>();
            foreach (var s in selections ?? new List<Selection>())
            {
                if (!MovableKinds.Contains(s.Kind) || !items.TryGetValue(s.Kind + "#" + s.Key, out var item))
                {
                    error = "An item is no longer part of this change (already moved, or changed by someone else). Reload the list.";
                    return null;
                }
                string tk = string.IsNullOrWhiteSpace(s.Target) ? item.DefaultTarget : s.Target.Trim();
                if (tk == null || !targets.TryGetValue(tk, out var target))
                {
                    error = "Choose where " + item.Label + " goes.";
                    return null;
                }
                work.Add((item, target));
            }
            if (work.Count == 0) { error = "Tick at least one item to move."; return null; }

            int batch = Convert.ToInt32(OrgChangeService.Scalar(db, "SELECT ISNULL(MAX(BATCH_NO), 0) + 1 FROM dbo.TB_R_ORG_CHANGE_MOVE WHERE ORG_CHANGE_ID = @ID", ("@ID", changeId)));
            var result = new Result { Batch = batch };
            void Count(string kind, int n) { if (n > 0) result.Moved[kind] = (result.Moved.TryGetValue(kind, out int c) ? c : 0) + n; }

            foreach (var (item, t) in work)
            {
                switch (item.Kind)
                {
                    case "DOCUMENT": Count("DOCUMENT", MoveDocument(db, changeId, batch, plan, item, t, user)); break;
                    case "EXTDOC": Count("EXTDOC", MoveExternal(db, changeId, batch, item, t, user)); break;
                    case "COPY": Count("COPY", MoveCopy(db, changeId, batch, item, t, user)); break;
                    case "BINDER": Count("BINDER", MoveRow(db, changeId, batch, item, user, "TB_M_DOCUMENT_FOLDER", "FOLDER_ID", ("DEPARTMENT_ID", t.DeptId))); break;
                    case "REFACCESS": Count("REFACCESS", MoveRow(db, changeId, batch, item, user, "TB_M_REFERENCE_ACCESS", "REFERENCE_ACCESS_ID", ("OWNER_DEPARTMENT_ID", t.DeptId))); break;
                    case "PICSCOPE": Count("PICSCOPE", MovePicScope(db, changeId, batch, item, t, user)); break;
                    case "MASTER": Count("MASTER", MoveDepartmentDivision(db, changeId, batch, item, t, user)); break;
                    case "DEPARTMENT": Count("DEPARTMENT", MoveDepartmentDivision(db, changeId, batch, item, t, user)); break;
                }
            }
            OrgChangeService.WriteLog(db, changeId, "TRANSFER", "Batch " + batch + ": " +
                string.Join(", ", result.Moved.Select(kv => kv.Value + " " + kv.Key.ToLowerInvariant())), user);
            return result;
        }

        private static void LogMove(DBContext db, int changeId, int batch, string kind, string table, string key, string label, string oldValue, string newValue, string user) =>
            X(db, @"INSERT INTO dbo.TB_R_ORG_CHANGE_MOVE (ORG_CHANGE_ID, BATCH_NO, ITEM_KIND, TABLE_NAME, ROW_KEY, ITEM_LABEL, OLD_VALUE, NEW_VALUE, MOVED_BY, MOVED_DT)
                    VALUES (@ID, @B, @K, @T, @R, @L, @O, @N, @U, GETDATE())",
                ("@ID", changeId), ("@B", batch), ("@K", kind), ("@T", table), ("@R", key), ("@L", Cut(label, 500)),
                ("@O", Cut(oldValue, 1000)), ("@N", Cut(newValue, 1000)), ("@U", user));

        private static string Cut(string s, int n) => s != null && s.Length > n ? s.Substring(0, n) : s;

        // "COL=value; COL=value" of the given columns of one row, for the move log
        private static string Snapshot(DBContext db, string table, string keyColumn, string key, IEnumerable<string> columns)
        {
            var r = Q(db, "SELECT " + string.Join(", ", columns.Select(c => "[" + c + "]")) + " FROM dbo.[" + table + "] WHERE [" + keyColumn + "] = @K", ("@K", key)).FirstOrDefault();
            return r == null ? null : string.Join("; ", columns.Select(c => c + "=" + (S(r[c]) ?? "NULL")));
        }

        /// <summary>Updates one row's columns and logs before/after. Returns 1 when the row was there.</summary>
        private static int UpdateRow(DBContext db, int changeId, int batch, string kind, string table, string keyColumn, string key, string label, string user,
            params (string col, object value)[] sets)
        {
            var cols = sets.Select(s => s.col).ToList();
            string before = Snapshot(db, table, keyColumn, key, cols);
            if (before == null) return 0;
            var ps = sets.Select((s, i) => ("@V" + i, s.value)).ToList();
            ps.Add(("@K", key));
            X(db, "UPDATE dbo.[" + table + "] SET " + string.Join(", ", sets.Select((s, i) => "[" + s.col + "] = @V" + i)) + " WHERE [" + keyColumn + "] = @K", ps.ToArray());
            LogMove(db, changeId, batch, kind, table, key, label, before, Snapshot(db, table, keyColumn, key, cols), user);
            return 1;
        }

        private static int MoveRow(DBContext db, int changeId, int batch, Item item, string user, string table, string keyColumn, (string, object) set) =>
            UpdateRow(db, changeId, batch, item.Kind, table, keyColumn, item.Key, item.Label, user, set);

        private static int MoveDocument(DBContext db, int changeId, int batch, Plan plan, Item item, Target t, string user)
        {
            var sets = new List<(string, object)>
            {
                ("DIVISION", t.Division), ("DIVISION_NAME", t.DivisionName)
            };
            if (t.DeptId.HasValue)
            {
                sets.Add(("DEPARTMENT", t.DeptId)); sets.Add(("DEPARTMENT_CODE", t.DeptCode)); sets.Add(("DEPARTMENT_NAME", t.DeptName));
            }
            if (t.SectionCode != null)
            {
                sets.Add(("SECTION_CODE", t.SectionCode)); sets.Add(("SECTION_NAME", t.SectionName));
            }
            int n = UpdateRow(db, changeId, batch, "DOCUMENT", "TB_R_DOCUMENT", "DOCUMENT_TRANSACTION_ID", item.Key, item.Label, user, sets.ToArray());

            // the open P4D registration of the document follows its owner
            foreach (var r in Q(db, "SELECT DOCUMENT_CTRL_ID FROM dbo.TB_R_CTRL_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @T AND ISNULL(DELETE_FLAG, 0) = 0", ("@T", item.Key)))
            {
                var ctrl = new List<(string, object)> { ("DIVISION", t.Division), ("DIVISION_NAME", t.DivisionName) };
                if (t.DeptId.HasValue) { ctrl.Add(("DEPARTMENT_ID", t.DeptId)); ctrl.Add(("DEPARTMENT_CODE", t.DeptCode)); ctrl.Add(("DEPARTMENT_NAME", t.DeptName)); }
                if (t.SectionCode != null) ctrl.Add(("SECTION_CODE", t.SectionCode));
                UpdateRow(db, changeId, batch, "P4D", "TB_R_CTRL_DOCUMENT", "DOCUMENT_CTRL_ID", S(r["DOCUMENT_CTRL_ID"]), item.Label, user, ctrl.ToArray());
            }
            return n;
        }

        private static int MoveExternal(DBContext db, int changeId, int batch, Item item, Target t, string user) =>
            UpdateRow(db, changeId, batch, "EXTDOC", "TB_M_EXTERNAL_DOCUMENT", "EXTERNAL_DOCUMENT_ID", item.Key, item.Label, user,
                ("DEPARTMENT_ID", t.DeptId), ("DEPARTMENT_CODE", t.DeptCode), ("DEPARTMENT_NAME", t.DeptName), ("DIVISION", t.Division), ("DIVISION_NAME", t.DivisionName));

        // the holder of a copy, and its acknowledgment (matched by department id), go together
        private static int MoveCopy(DBContext db, int changeId, int batch, Item item, Target t, string user)
        {
            var dd = Q(db, "SELECT DOCUMENT_TRANSACTION_ID, DEPARTMENT_ID FROM dbo.TB_R_DOCUMENT_DISTRIBUTION WHERE DISTRIBUTION_ID = @K", ("@K", item.Key)).FirstOrDefault();
            if (dd == null) return 0;
            foreach (var r in Q(db, @"SELECT PH.PUBLISH_HISTORY_ID FROM dbo.TB_R_PUBLISH_HISTORY PH
                                       JOIN dbo.TB_R_CTRL_DOCUMENT CT ON CT.DOCUMENT_CTRL_ID = PH.DOCUMENT_CTRL_ID
                                       WHERE CT.DOCUMENT_TRANSACTION_ID = @T AND PH.DEPARTMENT_ID = @D",
                         ("@T", dd["DOCUMENT_TRANSACTION_ID"]), ("@D", dd["DEPARTMENT_ID"])))
                UpdateRow(db, changeId, batch, "RECEIPT", "TB_R_PUBLISH_HISTORY", "PUBLISH_HISTORY_ID", S(r["PUBLISH_HISTORY_ID"]), item.Label, user,
                    ("DEPARTMENT_ID", t.DeptId), ("DEPARTMENT_CODE", t.DeptCode), ("DEPARTMENT_NAME", t.DeptName));
            return UpdateRow(db, changeId, batch, "COPY", "TB_R_DOCUMENT_DISTRIBUTION", "DISTRIBUTION_ID", item.Key, item.Label, user,
                ("DEPARTMENT_ID", t.DeptId), ("DEPARTMENT_CODE", t.DeptCode), ("DEPARTMENT_NAME", t.DeptName), ("DIVISION", t.Division), ("DIVISION_NAME", t.DivisionName));
        }

        private static int MovePicScope(DBContext db, int changeId, int batch, Item item, Target t, string user)
        {
            var parts = item.Key.Split('|');
            int n = X(db, @"IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_USER_DEPT_SCOPE WHERE USERNAME = @U AND DEPARTMENT_ID = @N)
                                INSERT INTO dbo.TB_M_USER_DEPT_SCOPE (USERNAME, DEPARTMENT_ID, CREATED_BY, CREATED_DT) VALUES (@U, @N, @BY, GETDATE());
                            DELETE FROM dbo.TB_M_USER_DEPT_SCOPE WHERE USERNAME = @U AND DEPARTMENT_ID = @O;",
                ("@U", parts[0]), ("@N", t.DeptId), ("@O", parts[1]), ("@BY", user));
            LogMove(db, changeId, batch, "PICSCOPE", "TB_M_USER_DEPT_SCOPE", item.Key, item.Label, "DEPARTMENT_ID=" + parts[1], "DEPARTMENT_ID=" + t.DeptId, user);
            return 1;
        }

        /// <summary>
        /// A department changes division (MOVE of a department, or a division renamed / merged / split):
        /// the master row, then the division on its documents, P4D registrations, external documents,
        /// copies it holds and its user positions.
        /// </summary>
        private static int MoveDepartmentDivision(DBContext db, int changeId, int batch, Item item, Target t, string user)
        {
            string deptId = item.Key;
            int n = UpdateRow(db, changeId, batch, item.Kind, "TB_M_DEPARTMENT", "DEPARTMENT_ID", deptId, item.Label, user,
                ("DIVISION", t.Division), ("CHANGED_BY", user), ("CHANGED_DT", DateTime.Now));
            if (n == 0) return 0;
            void Each(string kind, string table, string keyCol, string where, params (string, object)[] sets)
            {
                foreach (var r in Q(db, "SELECT [" + keyCol + "] AS K FROM dbo.[" + table + "] WHERE " + where, ("@D", deptId)))
                    UpdateRow(db, changeId, batch, kind, table, keyCol, S(r["K"]), item.Label, user, sets);
            }
            var div = new (string, object)[] { ("DIVISION", t.Division), ("DIVISION_NAME", t.DivisionName) };
            Each("DOCUMENT", "TB_R_DOCUMENT", "DOCUMENT_TRANSACTION_ID", "DEPARTMENT = @D AND ISNULL(DELETE_FLAG, 0) = 0 AND ISNULL([STATUS], '0') <> '4'", div);
            Each("P4D", "TB_R_CTRL_DOCUMENT", "DOCUMENT_CTRL_ID", "DEPARTMENT_ID = @D AND ISNULL(DELETE_FLAG, 0) = 0", div);
            Each("EXTDOC", "TB_M_EXTERNAL_DOCUMENT", "EXTERNAL_DOCUMENT_ID", "DEPARTMENT_ID = @D AND ISNULL(DELETE_FLAG, 0) = 0", div);
            Each("COPY", "TB_R_DOCUMENT_DISTRIBUTION", "DISTRIBUTION_ID",
                "DEPARTMENT_ID = @D AND DOCUMENT_TRANSACTION_ID IN (SELECT DOCUMENT_TRANSACTION_ID FROM dbo.TB_R_DOCUMENT WHERE ISNULL(DELETE_FLAG, 0) = 0 AND ISNULL([STATUS], '0') <> '4')", div);
            Each("USERPOS", "TB_M_USER_POS", "USER_POS_ID", "DEPARTMENT_ID = @D", ("DIVISION", t.Division));
            return 1;
        }

        // ------------------------------------------------------------------ moves (report / screen)

        public static List<Dictionary<string, object>> Moves(DBContext db, int changeId) => Q(db, @"
            SELECT M.BATCH_NO, M.ITEM_KIND, M.TABLE_NAME, M.ROW_KEY, M.ITEM_LABEL, M.OLD_VALUE, M.NEW_VALUE,
                   ISNULL(U.FULL_NAME, M.MOVED_BY) AS MOVED_BY, M.MOVED_DT
            FROM dbo.TB_R_ORG_CHANGE_MOVE M LEFT JOIN dbo.TB_M_USER U ON U.USERNAME = M.MOVED_BY
            WHERE M.ORG_CHANGE_ID = @ID ORDER BY M.BATCH_NO DESC, M.MOVE_ID", ("@ID", changeId));

        /// <summary>Moved rows per kind of one change, e.g. "5 document, 12 copy" (for the report).</summary>
        public static string MovedSummary(DBContext db, int changeId)
        {
            var rows = Q(db, @"SELECT ITEM_KIND, COUNT(*) AS N FROM dbo.TB_R_ORG_CHANGE_MOVE WHERE ORG_CHANGE_ID = @ID
                               AND ITEM_KIND NOT IN ('P4D', 'RECEIPT', 'USERPOS') GROUP BY ITEM_KIND ORDER BY ITEM_KIND", ("@ID", changeId));
            return string.Join(", ", rows.Select(r => r["N"] + " " + KindLabel(S(r["ITEM_KIND"]))));
        }

        public static string KindLabel(string kind) => kind switch
        {
            "DOCUMENT" => "dokumen", "EXTDOC" => "dokumen eksternal", "COPY" => "salinan", "BINDER" => "bantex",
            "PICSCOPE" => "cakupan PIC", "REFACCESS" => "reference access", "MASTER" => "master departemen",
            "DEPARTMENT" => "departemen", _ => kind.ToLowerInvariant()
        };
    }
}
