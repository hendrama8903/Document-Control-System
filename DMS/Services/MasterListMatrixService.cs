using System.Data;
using System.Drawing;
using System.Globalization;
using DevExpress.Spreadsheet;
using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Services
{
    /// <summary>
    /// Master List matrix in the layout of form QCD/FR-QMS-00/004 (2026-11-19): per owning
    /// department, each valid document with its revision dates (Revisi Ke-) and the full
    /// distribution history per division (Distribusi Dokumen, "rev:DEPT-ddMMMyy").
    /// Load() feeds the on-screen matrix; BuildWorkbook() feeds both the Excel and the PDF
    /// export, so the three always show the same thing.
    /// </summary>
    public static class MasterListMatrixService
    {
        public const string FormNumber = "QCD/FR-QMS-00/004";
        public const string FormTitle = "MASTER LIST DOKUMEN INTERNAL";
        public const string AllCode = "ALL";
        public const string AllDivision = "All Division";
        public const string AllDepartment = "All Department";

        /// <summary>Form title of a sheet: the system is named when it is not QMS, e.g. "... (K3L)".</summary>
        public static string TitleFor(Group g) => g == null || string.IsNullOrEmpty(g.MgmtSystem) || g.MgmtSystem == "QMS"
            ? FormTitle : FormTitle + " (" + g.MgmtSystem + ")";
        public const string FormRevision = "REVISI: 00";
        private const int MinRevisionColumns = 6;   // Revisi Ke- 0..5, as on the paper form

        // Column order of the paper form; a division not listed here goes after, A-Z.
        private static readonly string[] DivisionOrder =
        {
            "IAD", "HRD", "GAD", "ITD", "CED", "FAD", "BPD", "PPP", "PED", "EGD",
            "LCD", "PAD", "ASY", "BDY", "MUD", "QCD", "QAD", "PUD", "SHE"
        };

        public class Report
        {
            public DateTime GeneratedAt { get; set; }
            public int RevisionColumns { get; set; }
            public List<Division> Divisions { get; set; } = new List<Division>();
            public List<Group> Groups { get; set; } = new List<Group>();
        }

        public class Division
        {
            public string Code { get; set; }
            public string Name { get; set; }
        }

        public class Group
        {
            /// <summary>Management System of the sheet (QMS / K3L / ..., 2026-11-19).</summary>
            public string MgmtSystem { get; set; } = "QMS";
            /// <summary>QMS manuals and procedures (Level 1-2): one company-wide sheet, "All Division / All Department".</summary>
            public bool CompanyWide { get; set; }
            public string Division { get; set; }
            public string DivisionName { get; set; }
            public string DepartmentCode { get; set; }
            public string DepartmentName { get; set; }
            public List<Doc> Documents { get; set; } = new List<Doc>();
        }

        public class Doc
        {
            public int TransactionId { get; set; }
            public string Code { get; set; }
            /// <summary>Management System (QMS / K3L / ..., 2026-11-19).</summary>
            public string MgmtSystem { get; set; }
            /// <summary>Numbers it had before a renumbering revision ("QCD/SOP-QPD-00/002"), or null.</summary>
            public string OldCodes { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public bool Confidential { get; set; }
            public int Revision { get; set; }
            /// <summary>Revision number -> effective date and reason.</summary>
            public Dictionary<int, Rev> Revisions { get; set; } = new Dictionary<int, Rev>();
            /// <summary>Division code -> every copy sent to that division.</summary>
            public Dictionary<string, List<Dist>> Distribution { get; set; } = new Dictionary<string, List<Dist>>();
        }

        public class Rev
        {
            public DateTime? Date { get; set; }
            public string Reason { get; set; }
        }

        public class Dist
        {
            public int Revision { get; set; }
            public string Department { get; set; }
            public DateTime? Date { get; set; }
            /// <summary>The department's newest copy is older than the current revision: an old copy not yet replaced.</summary>
            public bool Outdated { get; set; }
            public string Text => Revision + ":" + Department + (Date.HasValue ? "-" + FormatShortDate(Date.Value) : "");
        }

        public static string FormatShortDate(DateTime d) => d.ToString("ddMMMyy", CultureInfo.InvariantCulture);

        /// <summary>Paper-form column order of the divisions, the rest A-Z.</summary>
        public static List<Division> OrderDivisions(IEnumerable<Division> divisions) => divisions
            .OrderBy(d => { int i = Array.IndexOf(DivisionOrder, d.Code); return i < 0 ? int.MaxValue : i; })
            .ThenBy(d => d.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        /// <summary>The running number at the end of a document code ("QCD/SOP-PQA-00/002" -> 2).</summary>
        public static int SequenceNumber(string code)
        {
            if (string.IsNullOrEmpty(code)) return int.MaxValue;
            int end = code.Length, start = end;
            while (start > 0 && char.IsDigit(code[start - 1])) start--;
            return start < end && int.TryParse(code.Substring(start, Math.Min(9, end - start)), out int n) ? n : int.MaxValue;
        }

        /// <summary>
        /// Orders the documents of one department block. sort: "number" (default, the running
        /// number at the end of No. Dokumen, then the code), "code", "name" or "type".
        /// </summary>
        public static void Sort(List<Doc> docs, string sort, bool descending)
        {
            Comparison<Doc> byNumber = (a, b) =>
            {
                int c = SequenceNumber(a.Code).CompareTo(SequenceNumber(b.Code));
                return c != 0 ? c : string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase);
            };
            Comparison<Doc> cmp = (sort ?? "").ToLowerInvariant() switch
            {
                "code" => (a, b) => string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase),
                "name" => (a, b) => { int c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : byNumber(a, b); },
                "type" => (a, b) => { int c = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : byNumber(a, b); },
                _ => byNumber
            };
            docs.Sort(descending ? (a, b) => cmp(b, a) : cmp);
        }

        // ------------------------------------------------------------------ data

        public static Report Load(DBContext db, string division = null, string department = null, string mgmtSystem = null)
        {
            var sets = Query(db, "sp_DocumentMasterlist_Matrix", new[]
            {
                new SqlParameter("@DIVISION", (object)Blank(division) ?? DBNull.Value),
                new SqlParameter("@DEPARTMENT_CODE", (object)Blank(department) ?? DBNull.Value),
                new SqlParameter("@MGMT_SYSTEM", (object)Blank(mgmtSystem) ?? DBNull.Value),
            });

            var report = new Report { GeneratedAt = DateTime.Now };
            var docsByCode = new Dictionary<string, Doc>(StringComparer.OrdinalIgnoreCase);
            var groups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in sets[0])
            {
                string div = Str(r, "DIVISION"), dept = Str(r, "DEPARTMENT_CODE");
                // Management System (2026-11-19): a sheet per system; QMS Level 1-2 (manuals and
                // procedures) belong to the whole company - one "All Division / All Department" sheet,
                // as on the paper QMS master list. Other systems keep their owning department.
                string system = Str(r, "MGMT_SYSTEM") ?? "QMS";
                bool companyWide = system == "QMS" && r["CATEGORY_LEVEL"] != null && Convert.ToInt32(r["CATEGORY_LEVEL"]) <= 2;
                string key = system + "|" + (companyWide ? "*" : div + "|" + dept);
                if (!groups.TryGetValue(key, out var g))
                {
                    g = companyWide
                        ? new Group { MgmtSystem = system, CompanyWide = true, Division = AllCode, DivisionName = AllDivision, DepartmentCode = AllCode, DepartmentName = AllDepartment }
                        : new Group
                        {
                            MgmtSystem = system,
                            Division = div,
                            DivisionName = Str(r, "DIVISION_NAME"),
                            DepartmentCode = dept,
                            DepartmentName = Str(r, "DEPARTMENT_NAME")
                        };
                    groups[key] = g;
                    report.Groups.Add(g);
                }

                var doc = new Doc
                {
                    TransactionId = Convert.ToInt32(r["DOCUMENT_TRANSACTION_ID"]),
                    Code = Str(r, "DOCUMENT_CODE"),
                    OldCodes = Str(r, "OLD_CODES"),
                    MgmtSystem = Str(r, "MGMT_SYSTEM"),
                    Name = Str(r, "DOCUMENT_TRANSACTION_NAME"),
                    Category = Str(r, "CATEGORY_CODE"),
                    Confidential = Str(r, "CLASSIFIED") == "2",
                    Revision = Convert.ToInt32(r["REVISION"])
                };
                g.Documents.Add(doc);
                docsByCode[doc.Code] = doc;
            }

            // revisions: the current row comes first, history never overwrites it
            foreach (var r in sets[1])
            {
                if (!docsByCode.TryGetValue(Str(r, "DOCUMENT_CODE"), out var doc)) continue;
                int rev = Convert.ToInt32(r["REVISION"]);
                if (doc.Revisions.ContainsKey(rev)) continue;
                string reason = string.Join(" - ", new[] { Str(r, "ITEM_CHANGED"), Str(r, "REASON") }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                doc.Revisions[rev] = new Rev { Date = r["DOCUMENT_DATE"] as DateTime?, Reason = reason };
            }

            var seenDivisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in sets[2])
            {
                if (!docsByCode.TryGetValue(Str(r, "DOCUMENT_CODE"), out var doc)) continue;
                string div = Str(r, "DIVISION") ?? "-";
                seenDivisions.Add(div);
                if (!doc.Distribution.TryGetValue(div, out var list))
                {
                    list = new List<Dist>();
                    doc.Distribution[div] = list;
                }
                list.Add(new Dist
                {
                    Revision = Convert.ToInt32(r["REVISION"]),
                    Department = Str(r, "DEPARTMENT_CODE") ?? "-",
                    Date = r["DISTRIBUTION_DATE"] as DateTime?
                });
            }

            // flag departments whose newest copy is an older revision than the current one
            foreach (var doc in docsByCode.Values)
            {
                var all = doc.Distribution.Values.SelectMany(x => x).ToList();
                var newestPerDept = all.GroupBy(x => x.Department, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Max(d => d.Revision), StringComparer.OrdinalIgnoreCase);
                foreach (var d in all) d.Outdated = newestPerDept[d.Department] < doc.Revision;
                foreach (var list in doc.Distribution.Values)
                {
                    list.Sort((a, b) =>
                    {
                        int c = a.Revision.CompareTo(b.Revision);
                        if (c == 0) c = Nullable.Compare(a.Date, b.Date);
                        return c != 0 ? c : string.Compare(a.Department, b.Department, StringComparison.OrdinalIgnoreCase);
                    });
                }
            }

            // division columns: the active master plus any division that only old copies point to
            var divisions = sets[3].Select(r => new Division { Code = Str(r, "DIVISION_CODE"), Name = Str(r, "DIVISION_NAME") })
                .Where(d => d.Code != null).ToList();
            foreach (var code in seenDivisions.Where(c => !divisions.Any(d => string.Equals(d.Code, c, StringComparison.OrdinalIgnoreCase))))
                divisions.Add(new Division { Code = code, Name = code });
            report.Divisions = OrderDivisions(divisions);

            int maxRev = docsByCode.Values.SelectMany(d => d.Revisions.Keys.Append(d.Revision)).DefaultIfEmpty(0).Max();
            report.RevisionColumns = Math.Max(MinRevisionColumns, maxRev + 1);
            foreach (var g in report.Groups) Sort(g.Documents, null, false);
            // QMS first, its company-wide sheet before the departments, then the other systems
            report.Groups = report.Groups
                .OrderBy(g => g.MgmtSystem == "QMS" ? 0 : 1).ThenBy(g => g.MgmtSystem, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.CompanyWide ? 0 : 1).ToList();
            return report;
        }

        private static List<List<Dictionary<string, object>>> Query(DBContext db, string procedure, SqlParameter[] parameters)
        {
            var sets = new List<List<Dictionary<string, object>>>();
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "[dbo].[" + procedure + "]";
                command.CommandType = CommandType.StoredProcedure;
                var transaction = db.Database.CurrentTransaction;   // join an open EF transaction (tests, callers in a tx)
                if (transaction != null) command.Transaction = Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction(transaction);
                foreach (var p in parameters) command.Parameters.Add(p);
                using var rd = command.ExecuteReader();
                do
                {
                    var rows = new List<Dictionary<string, object>>();
                    while (rd.Read())
                    {
                        var row = new Dictionary<string, object>(rd.FieldCount, StringComparer.OrdinalIgnoreCase);
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

        private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        private static string Str(Dictionary<string, object> r, string key) =>
            r.TryGetValue(key, out var v) && v != null ? Convert.ToString(v)?.Trim() : null;

        // ------------------------------------------------------------------ workbook
        // Header, page setup and styling come from QmsFormSheet (shared with the receipt form).

        private static readonly Color OutdatedColor = Color.FromArgb(0xC6, 0x28, 0x28);

        // fixed columns (0-based): A margin, B No., C No. Dokumen, D Nama, E Jenis, F Umum, G Rahasia
        private const int ColNo = 1, ColCode = 2, ColName = 3, ColType = 4, ColPublic = 5, ColSecret = 6, ColFirstRev = 7;

        /// <summary>One sheet per owning department, A3 landscape, header rows repeated on every page.</summary>
        public static Workbook BuildWorkbook(Report report)
        {
            var wb = QmsFormSheet.NewWorkbook();
            wb.BeginUpdate();
            try
            {
                var groups = report.Groups.Count > 0 ? report.Groups : new List<Group> { new Group() };
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < groups.Count; i++)
                {
                    Worksheet sheet = i == 0 ? wb.Worksheets[0] : wb.Worksheets.Add();
                    var grp = groups[i];
                    string baseName = grp.CompanyWide ? "All Division" : (grp.DepartmentCode ?? grp.Division ?? "Master List");
                    if (!string.IsNullOrEmpty(grp.MgmtSystem) && grp.MgmtSystem != "QMS") baseName = grp.MgmtSystem + " " + baseName;
                    else if (grp.CompanyWide) baseName = "QMS " + baseName;
                    sheet.Name = QmsFormSheet.SheetName(baseName, usedNames);
                    FillSheet(sheet, report, groups[i]);
                }
                wb.Worksheets.ActiveWorksheet = wb.Worksheets[0];
            }
            finally
            {
                wb.EndUpdate();
            }
            return wb;
        }

        private static void FillSheet(Worksheet sheet, Report report, Group group)
        {
            int revCols = report.RevisionColumns;
            int colFirstDiv = ColFirstRev + revCols;
            int lastCol = colFirstDiv + Math.Max(report.Divisions.Count, 1) - 1;

            var widths = new List<double> { 1.5, 5, 19, 32, 8, 7, 7 };
            widths.AddRange(Enumerable.Repeat(9.0, revCols));
            widths.AddRange(Enumerable.Repeat(11.0, lastCol - colFirstDiv + 1));
            QmsFormSheet.WriteHeader(sheet, widths.ToArray(), FormNumber, TitleFor(group),
                group.DivisionName ?? group.Division, group.DepartmentName ?? group.DepartmentCode, report.GeneratedAt);

            // ---- two-level table header
            QmsFormSheet.HeaderSpan(sheet, ColNo, "No.");
            QmsFormSheet.HeaderSpan(sheet, ColCode, "No. Dokumen");
            QmsFormSheet.HeaderSpan(sheet, ColName, "Nama Dokumen");
            QmsFormSheet.HeaderSpan(sheet, ColType, "Jenis Dokumen");
            QmsFormSheet.HeaderGroup(sheet, ColPublic, ColSecret, "Status Dokumen", new[] { "Umum", "Rahasia" });
            QmsFormSheet.HeaderGroup(sheet, ColFirstRev, ColFirstRev + revCols - 1, "Revisi Ke-",
                Enumerable.Range(0, revCols).Select(n => n.ToString()).ToArray());
            QmsFormSheet.HeaderGroup(sheet, colFirstDiv, lastCol, "Distribusi Dokumen",
                report.Divisions.Count > 0 ? report.Divisions.Select(d => d.Code).ToArray() : new[] { "-" });

            // ---- data
            int row = QmsFormSheet.RowFirstData;
            int no = 1;
            foreach (var doc in group.Documents)
            {
                sheet.Cells[row, ColNo].Value = no++;
                // the old number stays visible as the reference (QMS, 2026-11-19)
                sheet.Cells[row, ColCode].Value = doc.Code + (string.IsNullOrEmpty(doc.OldCodes) ? "" : "\n(dulu: " + doc.OldCodes + ")");
                sheet.Cells[row, ColName].Value = doc.Name;
                sheet.Cells[row, ColType].Value = doc.Category;
                sheet.Cells[row, doc.Confidential ? ColSecret : ColPublic].Value = QmsFormSheet.Check;

                for (int rv = 0; rv < revCols; rv++)
                {
                    if (!doc.Revisions.TryGetValue(rv, out var rev) || !rev.Date.HasValue) continue;
                    Cell c = sheet.Cells[row, ColFirstRev + rv];
                    c.Value = rev.Date.Value;
                    c.NumberFormat = "dd-mmm-yy";
                    if (rv == doc.Revision) c.Font.Bold = true;
                    if (rv > 0 && !string.IsNullOrWhiteSpace(rev.Reason))
                        sheet.Comments.Add(c, "DMS", rev.Reason);
                }

                for (int d = 0; d < report.Divisions.Count; d++)
                {
                    if (!doc.Distribution.TryGetValue(report.Divisions[d].Code, out var list) || list.Count == 0) continue;
                    var rich = new RichTextString();
                    for (int i = 0; i < list.Count; i++)
                    {
                        string text = list[i].Text + (i < list.Count - 1 ? "\n" : "");
                        rich.AddTextRun(text, new RichTextRunFont(QmsFormSheet.FontName, 7.5, list[i].Outdated ? OutdatedColor : Color.Black)
                        { Bold = list[i].Outdated });
                    }
                    sheet.Cells[row, colFirstDiv + d].SetRichText(rich);
                }
                row++;
            }
            if (group.Documents.Count == 0) row = QmsFormSheet.EmptyRow(sheet, row, lastCol);

            QmsFormSheet.StyleBody(sheet, row, lastCol);
            int firstData = QmsFormSheet.RowFirstData;
            sheet.Range.FromLTRB(ColNo, firstData, ColNo, row - 1).Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            sheet.Range.FromLTRB(ColType, firstData, ColFirstRev + revCols - 1, row - 1).Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            CellRange checks = sheet.Range.FromLTRB(ColPublic, firstData, ColSecret, row - 1);
            checks.Font.Bold = true;
            checks.Font.Size = 11;
            // AutoFit does not size rich-text multi-line cells: set the height from the line count
            for (int r = firstData, i = 0; r < row && i < group.Documents.Count; r++, i++)
            {
                var doc = group.Documents[i];
                int distLines = doc.Distribution.Values.Select(l => l.Count).DefaultIfEmpty(1).Max();
                int nameLines = Math.Max(string.IsNullOrEmpty(doc.OldCodes) ? 1 : 3, (int)Math.Ceiling((doc.Name ?? "").Length / 30.0));
                sheet.Rows[r].Height = Math.Max(0.22, Math.Max(distLines * 0.125, nameLines * 0.16) + 0.06);
            }

            // freeze pane: header rows + No. .. Status Dokumen stay visible when scrolling
            QmsFormSheet.Finish(sheet, row, lastCol,
                "Distribusi: Revisi:Departemen-Tanggal terima. Merah = departemen masih memegang revisi lama (salinan belum diganti).",
                FormRevision, System.Drawing.Printing.PaperKind.A3, ColSecret);
        }
    }
}
