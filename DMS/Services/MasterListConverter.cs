using DMS.Models.DB;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DMS.Services
{
    /// <summary>
    /// Turns the departments' own master lists (form QCD/FR-QMS-00/004 "Master List Dokumen
    /// Internal": one row per document, revision dates side by side under "Revisi Ke-") into rows
    /// of the Document Import template (one row per revision), so nobody has to retype them
    /// (request Hendra 2026-11-12).
    ///
    /// The form is read by its header texts, not by fixed cells, so a list with other columns or
    /// another page size still works. Category, Division, Department and Section come from the
    /// document number (e.g. ITD/SOP-APP-00/001) and are checked against the master and against
    /// the Division/Department written above the table. File names are matched from the names of
    /// the documents the user picked (only names, the files are not uploaded here). Nothing is
    /// saved: the result is a template to check and then upload through the normal import.
    /// </summary>
    public static class MasterListConverter
    {
        public class Masters
        {
            public IList<DocumentMaster> Categories { get; set; } = new List<DocumentMaster>();
            public IList<DivisionMaster> Divisions { get; set; } = new List<DivisionMaster>();
            public IList<DepartmentMaster> Departments { get; set; } = new List<DepartmentMaster>();
            public IList<SectionMaster> Sections { get; set; } = new List<SectionMaster>();
            public IList<MSystem> Classifications { get; set; } = new List<MSystem>();
            public IList<MSystem> CompanyCodes { get; set; } = new List<MSystem>();
            public IList<MSystem> ProcessCodes { get; set; } = new List<MSystem>();
            /// <summary>Document Code (upper case) -> revision of the active document.</summary>
            public Dictionary<string, int> ExistingRevisions { get; set; } = new Dictionary<string, int>();
            /// <summary>
            /// Former department code (upper case) -> its current departments, from System Parameters
            /// type DEPT_SUCCESSOR (2026-11-16), since 2026-11-19 the Organization Change register. A department that was split has several successors:
            /// a distribution to the old code goes to all of them, the document owner to the first.
            /// The document number itself is never changed.
            /// </summary>
            public Dictionary<string, string[]> Successors { get; set; } = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        }

        private const string SuccessorHint = " If it was renamed, merged or split in a reorganisation, record it in Master Data > Organization Change (old code -> new code).";

        /// <summary>Active departments that replace a former department code; empty when none is set.</summary>
        private static List<DepartmentMaster> SuccessorsOf(string code, Masters m)
        {
            if (code == null || !m.Successors.TryGetValue(code.Trim(), out string[] next)) return new List<DepartmentMaster>();
            return next.Select(n => m.Departments.FirstOrDefault(d => Eq(d.DEPARTMENT_CODE, n))).Where(d => d != null).ToList();
        }

        public class OutRow
        {
            public string FileName { get; set; }
            public string DocumentCode { get; set; }
            public string DocumentName { get; set; }
            public string CategoryCode { get; set; }
            public string DivisionCode { get; set; }
            public string DepartmentCode { get; set; }
            public string ClassificationCode { get; set; }
            public int? Revision { get; set; }
            public DateTime? DocumentDate { get; set; }
            public string Reason { get; set; }
            // "APP:12-04-2017; INF:12-04-2017" - only on the latest revision row.
            public string Distribution { get; set; }
            public string Source { get; set; }
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
        }

        public class FileSummary
        {
            public string Name { get; set; }
            public string Division { get; set; }
            public string Department { get; set; }
            public int Documents { get; set; }
            public int Rows { get; set; }
            public string Problem { get; set; }
        }

        /// <summary>
        /// A code used in the master lists that the system does not know, collected over ALL
        /// lists of one conversion (request Hendra 2026-11-17), so every code that still needs
        /// a master entry or a DEPT_SUCCESSOR mapping is listed at once instead of being found
        /// one conversion at a time.
        /// </summary>
        public class MissingCode
        {
            public string Type { get; set; }        // Department, Division, Category, Section, Company, Process
            public string Code { get; set; }
            public bool IsError { get; set; }       // false = only a warning (row can still be imported)
            public SortedSet<string> UsedIn { get; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            public SortedSet<string> Documents { get; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            public SortedSet<string> MasterLists { get; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public class Result
        {
            public List<OutRow> Rows { get; } = new List<OutRow>();
            public List<FileSummary> Files { get; } = new List<FileSummary>();
            public List<string> UnusedFiles { get; } = new List<string>();
            public bool FilesGiven { get; set; }
            public int ErrorRows => Rows.Count(r => r.Errors.Count > 0);
            public int WarningRows => Rows.Count(r => r.Errors.Count == 0 && r.Warnings.Count > 0);

            private readonly Dictionary<string, MissingCode> missing = new Dictionary<string, MissingCode>(StringComparer.OrdinalIgnoreCase);

            /// <summary>Errors first, then by type and code.</summary>
            public IEnumerable<MissingCode> MissingCodes => missing.Values
                .OrderBy(x => x.IsError ? 0 : 1).ThenBy(x => x.Type).ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase);

            public void AddMissing(string type, string code, string usedIn, string document, string source, bool isError)
            {
                if (string.IsNullOrWhiteSpace(code)) return;
                code = code.Trim().ToUpperInvariant();
                if (!missing.TryGetValue(type + "|" + code, out MissingCode entry))
                    missing[type + "|" + code] = entry = new MissingCode { Type = type, Code = code };
                entry.IsError |= isError;
                entry.UsedIn.Add(usedIn);
                if (!string.IsNullOrEmpty(document)) entry.Documents.Add(document);
                // "List.xlsx [Sheet1]" -> the file name only
                if (!string.IsNullOrEmpty(source)) entry.MasterLists.Add(Regex.Replace(source, @"\s*\[[^\]]*\]$", ""));
            }
        }

        // One document as read from a master list, before it is split into revision rows.
        private class Doc
        {
            public string Source;
            public int ExcelRow;
            public string Code;
            public string Name;
            public string Type;
            public string StatusLabel;
            public bool StatusTwice;
            public string HeaderDivision;
            public string HeaderDepartment;
            public List<(int Revision, DateTime? Date, string Raw, string Comment)> Revisions = new();
            // Cells under "Distribusi Dokumen": column header (a division code) and the text.
            public List<(string Column, string Raw)> Distribution = new();
        }

        private const string Sep = @"[\s._/\-]+";

        public static Result Convert(IEnumerable<(string Name, Stream Content)> masterLists, IList<string> documentFiles, Masters m)
        {
            var result = new Result { FilesGiven = documentFiles != null && documentFiles.Count > 0 };
            var docs = new List<Doc>();

            foreach (var (name, content) in masterLists)
            {
                var summary = new FileSummary { Name = name };
                result.Files.Add(summary);
                try
                {
                    IWorkbook workbook = WorkbookFactory.Create(content);
                    int before = docs.Count;
                    for (int s = 0; s < workbook.NumberOfSheets; s++)
                    {
                        ISheet sheet = workbook.GetSheetAt(s);
                        if (workbook.IsSheetHidden(s) || workbook.IsSheetVeryHidden(s)) continue;
                        ReadSheet(sheet, workbook.NumberOfSheets > 1 ? name + " [" + sheet.SheetName + "]" : name, docs);
                    }
                    var mine = docs.Skip(before).ToList();
                    summary.Documents = mine.Count;
                    summary.Division = mine.Select(d => d.HeaderDivision).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                    summary.Department = mine.Select(d => d.HeaderDepartment).FirstOrDefault(x => !string.IsNullOrEmpty(x));
                    if (mine.Count == 0)
                        summary.Problem = "No document rows found. The table needs a \"No. Dokumen\" header.";
                }
                catch (Exception ex)
                {
                    summary.Problem = "Could not be read: " + ex.Message;
                }
            }

            // File names: keep the name only (the import looks files up by name inside the ZIP).
            var files = (documentFiles ?? new List<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => new { Path = f.Replace('\\', '/'), Name = Path.GetFileName(f.Replace('\\', '/')) })
                .Where(f => !string.IsNullOrEmpty(f.Name))
                .ToList();
            var duplicateNames = new HashSet<string>(files.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            var fileNames = files.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var usedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var seenCodes = new Dictionary<string, Doc>(StringComparer.OrdinalIgnoreCase);

            foreach (Doc doc in docs)
            {
                string source = doc.Source + ", row " + doc.ExcelRow;
                var shared = new OutRow { DocumentCode = doc.Code, DocumentName = doc.Name, Source = source };
                Resolve(doc, shared, m, result);

                if (seenCodes.TryGetValue(doc.Code, out Doc first))
                    shared.Errors.Add("Document number is also listed in " + first.Source + ", row " + first.ExcelRow + ". Keep it in one master list only.");
                else
                    seenCodes[doc.Code] = doc;

                var revisions = doc.Revisions.OrderBy(r => r.Revision).ToList();
                if (revisions.Count == 0)
                {
                    var row = Copy(shared);
                    row.Errors.Add("No revision date under \"Revisi Ke-\".");
                    result.Rows.Add(row);
                    continue;
                }

                int latest = revisions.Max(r => r.Revision);
                bool exists = m.ExistingRevisions.TryGetValue(doc.Code.ToUpperInvariant(), out int activeRevision);
                var candidates = MatchFiles(doc.Code, fileNames, usedFiles);

                foreach (var rev in revisions)
                {
                    OutRow row = Copy(shared);
                    row.Revision = rev.Revision;
                    row.DocumentDate = rev.Date;
                    row.Reason = rev.Comment;
                    if (rev.Date == null)
                        row.Errors.Add("Revision " + rev.Revision + " date \"" + rev.Raw + "\" is not a date.");

                    if (exists)
                    {
                        if (rev.Revision >= activeRevision)
                            row.Errors.Add("Already in the system at revision " + activeRevision + ". Revision " + rev.Revision + " cannot be imported; revise it in Document Maintenance.");
                        else
                            row.Warnings.Add("Already in the system at revision " + activeRevision + "; this row is only added to its history.");
                    }

                    bool isCurrent = rev.Revision == latest && !exists;
                    var exact = candidates.Where(c => c.Revision == rev.Revision).ToList();
                    if (exact.Count == 1)
                    {
                        row.FileName = exact[0].Name;
                    }
                    else if (exact.Count > 1)
                    {
                        if (isCurrent) row.Errors.Add("More than one file for revision " + rev.Revision + ": " + string.Join(", ", exact.Select(c => c.Name)) + ".");
                    }
                    else if (isCurrent)
                    {
                        var unnumbered = candidates.Where(c => c.Revision == null).ToList();
                        if (unnumbered.Count == 1)
                        {
                            row.FileName = unnumbered[0].Name;
                            row.Warnings.Add("The file name has no revision number; check that it is revision " + rev.Revision + ".");
                        }
                        else if (unnumbered.Count > 1)
                            row.Errors.Add("More than one file for this document: " + string.Join(", ", unnumbered.Select(c => c.Name)) + ".");
                        else if (candidates.Count > 0)
                            row.Errors.Add("The master list says the latest revision is " + rev.Revision + ", but the file found is " +
                                string.Join(", ", candidates.Select(c => c.Name)) + ". Update the master list or the file.");
                        else if (result.FilesGiven)
                            row.Errors.Add("No file for this document in the selected folder.");
                        else
                            row.Errors.Add("File Name is empty. Select the document folder before converting, or type the file name.");
                    }

                    if (isCurrent)
                    {
                        var newer = candidates.Where(c => c.Revision > rev.Revision).Select(c => c.Name).ToList();
                        if (newer.Count > 0)
                            row.Warnings.Add("The folder also has " + string.Join(", ", newer) + ", a newer revision than the master list (" + rev.Revision + "). Is the master list up to date?");
                    }

                    if (isCurrent)
                        row.Distribution = Distribute(doc, rev.Revision, rev.Date, row, m, result);

                    if (row.FileName != null)
                    {
                        usedFiles.Add(row.FileName);
                        if (duplicateNames.Contains(row.FileName))
                            row.Errors.Add("Two files in the folder are named \"" + row.FileName + "\". Rename one; the import matches files by name.");
                    }

                    result.Rows.Add(row);
                }

                if (exists && doc.Distribution.Count > 0)
                    result.Rows.Last().Warnings.Add("The distribution in the master list is not imported for a document already in the system; use P4D Maintenance.");

                var missing = Enumerable.Range(0, latest + 1).Except(revisions.Select(r => r.Revision)).ToList();
                if (missing.Count > 0)
                    result.Rows.Last().Warnings.Add("No date for revision " + string.Join(", ", missing) + " in the master list.");
            }

            foreach (FileSummary f in result.Files)
                f.Rows = result.Rows.Count(r => r.Source.StartsWith(f.Name, StringComparison.Ordinal));
            result.UnusedFiles.AddRange(fileNames.Where(n => !usedFiles.Contains(n)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            return result;
        }

        private static OutRow Copy(OutRow s)
        {
            var row = new OutRow
            {
                DocumentCode = s.DocumentCode, DocumentName = s.DocumentName, CategoryCode = s.CategoryCode,
                DivisionCode = s.DivisionCode, DepartmentCode = s.DepartmentCode, ClassificationCode = s.ClassificationCode,
                Source = s.Source
            };
            row.Errors.AddRange(s.Errors);
            row.Warnings.AddRange(s.Warnings);
            return row;
        }

        // ---------------------------------------------------------------- reading the form

        private static void ReadSheet(ISheet sheet, string source, List<Doc> docs)
        {
            int headerRow = -1, codeCol = -1;
            for (int r = sheet.FirstRowNum; r <= Math.Min(sheet.LastRowNum, 80) && headerRow < 0; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row == null) continue;
                foreach (ICell cell in row.Cells)
                {
                    string n = Norm(Text(cell));
                    if (n == "no dokumen" || n == "nomor dokumen" || n == "no document" || n == "document no" || n == "document number")
                    {
                        headerRow = r; codeCol = cell.ColumnIndex; break;
                    }
                }
            }
            if (headerRow < 0) return;

            int nameCol = -1, typeCol = -1;
            (int First, int Last) statusSpan = (-1, -1), revisionSpan = (-1, -1), distributionSpan = (-1, -1);
            foreach (ICell cell in sheet.GetRow(headerRow).Cells)
            {
                string n = Norm(Text(cell));
                if (n.StartsWith("nama dokumen") || n == "document name" || n == "document title") nameCol = cell.ColumnIndex;
                else if (n.StartsWith("jenis dokumen") || n == "document type") typeCol = cell.ColumnIndex;
                else if (n.StartsWith("status dokumen") || n == "classification") statusSpan = Span(sheet, headerRow, cell.ColumnIndex);
                else if (n.StartsWith("revisi") || n.StartsWith("revision")) revisionSpan = Span(sheet, headerRow, cell.ColumnIndex);
                else if (n.StartsWith("distribusi") || n.StartsWith("distribution")) distributionSpan = Span(sheet, headerRow, cell.ColumnIndex);
            }

            // Sub headers in the next row: revision numbers 0, 1, 2 ... and Umum / Rahasia.
            IRow sub = sheet.GetRow(headerRow + 1);
            var revisionCols = new List<(int Col, int Revision)>();
            var statusCols = new List<(int First, int Last, string Label)>();
            if (sub != null && revisionSpan.First >= 0)
            {
                for (int c = revisionSpan.First; c <= revisionSpan.Last; c++)
                {
                    string t = Text(sub.GetCell(c));
                    if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rev) && rev >= 0)
                        revisionCols.Add((c, rev));
                }
            }
            if (sub != null && statusSpan.First >= 0)
            {
                for (int c = statusSpan.First; c <= statusSpan.Last; c++)
                {
                    string t = Text(sub.GetCell(c));
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    var span = Span(sheet, headerRow + 1, c);
                    statusCols.Add((c, Math.Max(c, span.Last), t.Trim()));
                    c = Math.Max(c, span.Last);
                }
            }
            var distributionCols = new List<(int Col, string Division)>();
            if (sub != null && distributionSpan.First >= 0)
            {
                for (int c = distributionSpan.First; c <= distributionSpan.Last; c++)
                {
                    string t = Clean(Text(sub.GetCell(c)));
                    if (!string.IsNullOrEmpty(t)) distributionCols.Add((c, t.ToUpperInvariant()));
                }
            }
            int dataStart = revisionCols.Count > 0 || statusCols.Count > 0 ? headerRow + 2 : headerRow + 1;

            string division = LabelValue(sheet, headerRow, "division", "divisi");
            string department = LabelValue(sheet, headerRow, "department", "departemen", "dept");

            for (int r = dataStart; r <= sheet.LastRowNum; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row == null) continue;
                string code = Regex.Replace(Text(row.GetCell(codeCol)) ?? "", @"\s+", "").Trim();
                if (code.Length == 0 || !Regex.IsMatch(code, @"\d")) continue;

                var doc = new Doc
                {
                    Source = source,
                    ExcelRow = r + 1,
                    Code = code.ToUpperInvariant(),
                    Name = nameCol >= 0 ? Clean(Text(row.GetCell(nameCol))) : null,
                    Type = typeCol >= 0 ? Clean(Text(row.GetCell(typeCol))) : null,
                    HeaderDivision = division,
                    HeaderDepartment = department
                };

                foreach (var s in statusCols)
                {
                    bool marked = false;
                    for (int c = s.First; c <= s.Last && !marked; c++)
                        marked = !string.IsNullOrWhiteSpace(Text(row.GetCell(c)));
                    if (!marked) continue;
                    if (doc.StatusLabel != null) doc.StatusTwice = true;
                    doc.StatusLabel ??= s.Label;
                }

                foreach (var (col, rev) in revisionCols)
                {
                    ICell cell = row.GetCell(col);
                    string raw = Text(cell);
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    doc.Revisions.Add((rev, DateOf(cell), raw.Trim(), CommentOf(cell)));
                }

                foreach (var (col, column) in distributionCols)
                {
                    string raw = Clean(Text(row.GetCell(col)));
                    if (!string.IsNullOrEmpty(raw)) doc.Distribution.Add((column, raw));
                }

                docs.Add(doc);
            }
        }

        // Value written after a label above the table, e.g. "Department  :  Application".
        private static string LabelValue(ISheet sheet, int beforeRow, params string[] labels)
        {
            for (int r = sheet.FirstRowNum; r < beforeRow; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row == null) continue;
                var cells = row.Cells.OrderBy(c => c.ColumnIndex).ToList();
                for (int i = 0; i < cells.Count; i++)
                {
                    string text = Text(cells[i]) ?? "";
                    string label = text.Split(':')[0];
                    if (!labels.Contains(Norm(label))) continue;
                    if (text.Contains(':') && text.Split(':', 2)[1].Trim().Length > 0)
                        return Clean(text.Split(':', 2)[1]);
                    for (int j = i + 1; j < cells.Count; j++)
                    {
                        string v = (Text(cells[j]) ?? "").Trim().TrimStart(':').Trim();
                        if (v.Length > 0) return Clean(v);
                    }
                }
            }
            return null;
        }

        private static (int First, int Last) Span(ISheet sheet, int row, int col)
        {
            for (int i = 0; i < sheet.NumMergedRegions; i++)
            {
                CellRangeAddress range = sheet.GetMergedRegion(i);
                if (range.IsInRange(row, col)) return (range.FirstColumn, range.LastColumn);
            }
            return (col, col);
        }

        private static string Text(ICell cell)
        {
            if (cell == null) return null;
            CellType type = cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
            switch (type)
            {
                case CellType.String: return cell.StringCellValue;
                case CellType.Numeric: return cell.NumericCellValue.ToString(CultureInfo.InvariantCulture);
                case CellType.Boolean: return cell.BooleanCellValue ? "TRUE" : "FALSE";
                default: return null;
            }
        }

        private static readonly string[] DateFormats =
        {
            "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "yyyy-MM-dd",
            "dd-MMM-yy", "d-MMM-yy", "dd-MMM-yyyy", "d-MMM-yyyy", "dd MMM yyyy", "d MMM yyyy", "d MMMM yyyy"
        };

        private static DateTime? DateOf(ICell cell)
        {
            if (cell == null) return null;
            CellType type = cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
            if (type == CellType.Numeric)
            {
                double v = cell.NumericCellValue;
                // Dates are kept as serial numbers; a list with the date format lost still
                // carries them (e.g. 42821 = 27-03-2017).
                if (v >= 1 && v < 2958466) return DateTime.FromOADate(v).Date;
                return null;
            }
            string s = Text(cell)?.Trim();
            if (string.IsNullOrEmpty(s)) return null;
            foreach (var culture in new[] { new CultureInfo("id-ID"), CultureInfo.InvariantCulture })
                if (DateTime.TryParseExact(s, DateFormats, culture, DateTimeStyles.None, out DateTime d)) return d;
            return null;
        }

        // Reason of a revision is kept as a cell comment in the lists; drop the "Author:" line.
        private static string CommentOf(ICell cell)
        {
            string text = cell?.CellComment?.String?.String;
            if (string.IsNullOrWhiteSpace(text)) return null;
            var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (lines.Count > 1 && lines[0].EndsWith(":")) lines.RemoveAt(0);
            string reason = Clean(string.Join(" ", lines));
            return reason.Length > 255 ? reason.Substring(0, 255) : reason;
        }

        private static string Clean(string s) => s == null ? null : Regex.Replace(s, @"\s+", " ").Trim();

        private static string Norm(string s) =>
            s == null ? "" : Regex.Replace(Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9&]+", " "), @"\s+", " ").Trim();

        // ---------------------------------------------------------------- master data

        private static void Resolve(Doc doc, OutRow row, Masters m, Result result)
        {
            void Missing(string type, string code, bool isError) =>
                result.AddMissing(type, code, "document number", doc.Code, doc.Source, isError);

            // Document number: DIV/CAT-DEPT-SECTION/NNN (level 3-4) or COMPANY/CAT/XXX/NNN (level 1-2).
            Match l34 = Regex.Match(doc.Code, @"^([A-Z0-9]+)/([A-Z0-9]+)-([A-Z0-9]+)-([A-Z0-9]+)/(\d+)$");
            Match l12 = Regex.Match(doc.Code, @"^([A-Z0-9]+)/([A-Z0-9]+)/([A-Z0-9]+)/(\d+)$");
            string codeCategory = l34.Success ? l34.Groups[2].Value : l12.Success ? l12.Groups[2].Value : null;
            string codeDivision = l34.Success ? l34.Groups[1].Value : null;
            string codeDepartment = l34.Success ? l34.Groups[3].Value : null;
            string codeSection = l34.Success ? l34.Groups[4].Value : null;
            if (!l34.Success && !l12.Success)
                row.Warnings.Add("The document number does not follow the system format (DIV/CAT-DEPT-SECTION/001 or HMMI/CAT/XXX/001); check Category, Division and Department.");
            // Level 1/2: COMPANY/CAT/PROCESS/NNN (level 2) or COMPANY/CAT/YEAR/NNN (level 1).
            if (l12.Success && m.CompanyCodes.Count > 0 && !m.CompanyCodes.Any(c => Eq(c.SYSTEM_CODE, l12.Groups[1].Value)))
            {
                row.Warnings.Add("Company " + l12.Groups[1].Value + " is not in Company Code master.");
                Missing("Company", l12.Groups[1].Value, false);
            }
            if (l12.Success && !Regex.IsMatch(l12.Groups[3].Value, @"^\d{4}$") && m.ProcessCodes.Count > 0 && !m.ProcessCodes.Any(c => Eq(c.SYSTEM_CODE, l12.Groups[3].Value)))
            {
                row.Warnings.Add("Process " + l12.Groups[3].Value + " is not in Process Code master (" + string.Join("/", m.ProcessCodes.Select(c => c.SYSTEM_CODE)) + "). The number is imported as written, but new documents cannot continue this numbering.");
                Missing("Process", l12.Groups[3].Value, false);
            }
            if (doc.Code.Length > 25)
                row.Errors.Add("Document number is longer than 25 characters.");
            if (string.IsNullOrWhiteSpace(doc.Name))
                row.Errors.Add("Document name is empty.");

            // Category
            DocumentMaster byCode = codeCategory == null ? null : m.Categories.FirstOrDefault(c => Eq(c.DOCUMENT_CODE, codeCategory));
            DocumentMaster byType = string.IsNullOrEmpty(doc.Type) ? null
                : m.Categories.FirstOrDefault(c => Eq(c.DOCUMENT_CODE, doc.Type)) ?? m.Categories.FirstOrDefault(c => Norm(c.DOCUMENT_NAME) == Norm(doc.Type));
            DocumentMaster category = byCode ?? byType;
            if (category == null)
            {
                row.Errors.Add("Category \"" + (codeCategory ?? doc.Type) + "\" is not in Document Category master.");
                Missing("Category", codeCategory ?? doc.Type, true);
            }
            else
            {
                row.CategoryCode = category.DOCUMENT_CODE;
                if (byCode != null && byType != null && byCode.DOCUMENT_ID != byType.DOCUMENT_ID)
                    row.Warnings.Add("Document type \"" + doc.Type + "\" differs from the category in the number (" + byCode.DOCUMENT_CODE + ").");
                if (l34.Success && category.LEVEL is 1 or 2 || l12.Success && category.LEVEL is 3 or 4)
                    row.Warnings.Add("Category " + category.DOCUMENT_CODE + " is level " + category.LEVEL + "; the number format belongs to another level.");
            }

            // Division
            DivisionMaster headerDivision = MatchName(doc.HeaderDivision, m.Divisions, d => d.DIVISION_CODE, d => d.DIVISION_NAME);
            DivisionMaster division = codeDivision != null ? m.Divisions.FirstOrDefault(d => Eq(d.DIVISION_CODE, codeDivision)) : headerDivision;
            if (division == null)
            {
                string hint = headerDivision != null ? " Did you mean " + headerDivision.DIVISION_CODE + " - " + headerDivision.DIVISION_NAME + "?" : "";
                row.Errors.Add(codeDivision != null
                    ? "Division \"" + codeDivision + "\" is not in Division master (or is inactive)." + hint
                    : "Division \"" + doc.HeaderDivision + "\" could not be matched to Division master. Type the Division Code.");
                if (codeDivision != null) Missing("Division", codeDivision, true);
            }
            else
            {
                row.DivisionCode = division.DIVISION_CODE;
                if (codeDivision != null && !string.IsNullOrEmpty(doc.HeaderDivision) && headerDivision != null && headerDivision.DIVISION_CODE != division.DIVISION_CODE)
                    row.Warnings.Add("The list header says division \"" + doc.HeaderDivision + "\" (" + headerDivision.DIVISION_CODE + "), the number says " + division.DIVISION_CODE + ".");
            }

            // Department
            var departmentsOfDivision = division == null ? m.Departments : m.Departments.Where(d => Eq(DivisionOf(d), division.DIVISION_CODE)).ToList();
            DepartmentMaster headerDepartment = MatchName(doc.HeaderDepartment, departmentsOfDivision, d => d.DEPARTMENT_CODE, d => d.DEPARTMENT_NAME)
                ?? MatchName(doc.HeaderDepartment, m.Departments, d => d.DEPARTMENT_CODE, d => d.DEPARTMENT_NAME);
            DepartmentMaster department = codeDepartment != null ? m.Departments.FirstOrDefault(d => Eq(d.DEPARTMENT_CODE, codeDepartment)) : headerDepartment;

            // A department that no longer exists after a reorganisation: the number keeps the old
            // code, the document is owned by the successor department (and its division).
            bool bySuccessor = false;
            if (department == null && codeDepartment != null)
            {
                DepartmentMaster successor = SuccessorsOf(codeDepartment, m).FirstOrDefault();
                if (successor != null)
                {
                    department = successor;
                    bySuccessor = true;
                    row.DivisionCode = DivisionOf(successor);
                    row.Warnings.Add("Department " + codeDepartment + " no longer exists; owner set to its successor "
                        + successor.DEPARTMENT_CODE + " (" + DivisionOf(successor) + "). The document number is kept.");
                }
            }

            if (department == null)
            {
                string hint = headerDepartment != null ? " Did you mean " + headerDepartment.DEPARTMENT_CODE + " - " + headerDepartment.DEPARTMENT_NAME + "?" : "";
                row.Errors.Add(codeDepartment != null
                    ? "Department \"" + codeDepartment + "\" is not in Department master (or is inactive)." + hint + SuccessorHint
                    : "Department \"" + doc.HeaderDepartment + "\" could not be matched to Department master. Type the Department Code.");
                if (codeDepartment != null) Missing("Department", codeDepartment, true);
            }
            else
            {
                row.DepartmentCode = department.DEPARTMENT_CODE;
                if (division != null && !bySuccessor && !Eq(DivisionOf(department), division.DIVISION_CODE))
                    row.Errors.Add("Department " + department.DEPARTMENT_CODE + " belongs to division " + DivisionOf(department) + ", not " + division.DIVISION_CODE + ".");
                if (codeDepartment != null && !string.IsNullOrEmpty(doc.HeaderDepartment) && headerDepartment != null && headerDepartment.DEPARTMENT_CODE != department.DEPARTMENT_CODE)
                    row.Warnings.Add("The list header says department \"" + doc.HeaderDepartment + "\" (" + headerDepartment.DEPARTMENT_CODE + "), the number says " + department.DEPARTMENT_CODE + ".");
                if (codeSection != null && !m.Sections.Any(s => Eq(s.DEPARTMENT_CODE, department.DEPARTMENT_CODE) && Eq(s.SECTION_CODE, codeSection)))
                {
                    row.Warnings.Add("Section " + codeSection + " is not in Section master for department " + department.DEPARTMENT_CODE + ".");
                    Missing("Section", department.DEPARTMENT_CODE + "-" + codeSection, false);
                }
            }

            // Classification: the column ticked under "Status Dokumen" (Umum / Rahasia).
            if (doc.StatusTwice)
                row.Errors.Add("Both classifications are ticked.");
            MSystem classification = doc.StatusLabel == null ? null
                : m.Classifications.FirstOrDefault(c => Norm(c.SYSTEM_VALUE) == Norm(doc.StatusLabel) || Eq(c.SYSTEM_CODE, doc.StatusLabel));
            if (classification != null)
                row.ClassificationCode = classification.SYSTEM_CODE;
            else if (doc.StatusLabel != null)
                row.Errors.Add("Classification \"" + doc.StatusLabel + "\" is not in the master.");
            else
            {
                MSystem general = m.Classifications.FirstOrDefault(c => Norm(c.SYSTEM_VALUE) == "umum") ?? m.Classifications.FirstOrDefault();
                row.ClassificationCode = general?.SYSTEM_CODE;
                row.Warnings.Add("No classification ticked; set to " + (general?.SYSTEM_VALUE ?? "-") + ".");
            }
        }

        private static string DivisionOf(DepartmentMaster d) => d.DIVISION?.Split(" - ", 2)[0].Trim();

        private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static readonly HashSet<string> NoiseWords = new HashSet<string> { "div", "division", "divisi", "dept", "department", "departemen", "and", "dan", "the", "&", "it" };

        // Master item for a name written by hand: same code, same name, one name containing the
        // other, or all words of the text found in the name ("Information Technology" ->
        // "Information & Technology Div"). Only a single match counts.
        private static T MatchName<T>(string text, IEnumerable<T> items, Func<T, string> code, Func<T, string> name) where T : class
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var list = items.ToList();
            string n = Norm(text);
            T Single(IEnumerable<T> found) { var f = found.Take(2).ToList(); return f.Count == 1 ? f[0] : null; }

            var hit = Single(list.Where(x => Eq(code(x), text))) ?? Single(list.Where(x => Norm(name(x)) == n));
            if (hit != null) return hit;
            if (n.Length >= 3)
            {
                hit = Single(list.Where(x => Norm(name(x)).Contains(n) || (Norm(name(x)).Length >= 3 && n.Contains(Norm(name(x))))));
                if (hit != null) return hit;
            }
            var words = n.Split(' ').Where(w => w.Length >= 3 && !NoiseWords.Contains(w)).ToList();
            if (words.Count == 0) return null;
            return Single(list.Where(x => { var nameWords = Norm(name(x)).Split(' '); return words.All(w => nameWords.Contains(w)); }));
        }

        // ---------------------------------------------------------------- distribution

        // "0:ITD-12Apr17", several per cell ("1:APP-02Jun26, 1:INF-03Jun26", or one per line) and
        // several codes per entry ("1:APP,INF-02Jun26"). Agreed with QMS 2026-11-12: the code in
        // the cell is the recipient. A division code means every department of that division got
        // it (one binder for the division), a department code only that department. The column
        // header is not checked against a department: the lists keep old columns after
        // reorganisations (EID under PPE and later FAD in ListMasterSMK3.xlsx).
        // Tolerates the typos seen in the lists: "ENV20Aug24" (no dash), "UD2-020Aug24".
        private static readonly Regex DistributionEntry = new Regex(
            @"(\d+)\s*:\s*([A-Z][A-Z0-9#]*?(?:\s*[,&/+]\s*[A-Z][A-Z0-9#]*?)*)\s*[-–]?\s*0*(\d{1,2})\s*[-./ ]?\s*([A-Z]{3,9})\.?\s*[-./ ]?\s*(\d{2,4})(?!\d)",
            RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, int> Months = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["jan"] = 1, ["feb"] = 2, ["mar"] = 3, ["apr"] = 4, ["may"] = 5, ["mei"] = 5, ["jun"] = 6, ["jul"] = 7,
            ["aug"] = 8, ["agu"] = 8, ["agt"] = 8, ["ags"] = 8, ["sep"] = 9, ["oct"] = 10, ["okt"] = 10,
            ["nov"] = 11, ["dec"] = 12, ["des"] = 12
        };

        private static string Distribute(Doc doc, int revision, DateTime? revisionDate, OutRow row, Masters m, Result result)
        {
            var received = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            var older = new SortedSet<int>();

            foreach (var (column, raw) in doc.Distribution)
            {
                MatchCollection matches = DistributionEntry.Matches(raw);
                string rest = Regex.Replace(DistributionEntry.Replace(raw, " "), @"[\s,;/&+\-]+", "");
                if (matches.Count == 0 || rest.Length > 0)
                    row.Warnings.Add("Distribution \"" + raw + "\" under " + column + " could not be read completely (expected e.g. 0:" + column + "-12Apr17).");

                foreach (Match match in matches)
                {
                    int rev = int.Parse(match.Groups[1].Value);
                    DateTime? date = null;
                    if (Months.TryGetValue(match.Groups[4].Value.Substring(0, 3), out int month))
                    {
                        int year = int.Parse(match.Groups[5].Value);
                        if (year < 100) year += 2000;
                        int day = int.Parse(match.Groups[3].Value);
                        if (day >= 1 && day <= DateTime.DaysInMonth(year, month)) date = new DateTime(year, month, day);
                    }
                    string text = match.Value.Trim();

                    if (date == null) { row.Errors.Add("Distribution \"" + text + "\" under " + column + " has no valid date."); continue; }
                    if (rev > revision) { row.Errors.Add("Distribution \"" + text + "\" is for revision " + rev + ", but the latest revision in the list is " + revision + "."); continue; }
                    if (rev < revision) { older.Add(rev); continue; }
                    if (date > DateTime.Today) { row.Errors.Add("Distribution date in \"" + text + "\" is in the future."); continue; }
                    if (revisionDate != null && date < revisionDate)
                        row.Warnings.Add("Distribution \"" + text + "\" is dated before revision " + revision + " (" + revisionDate.Value.ToString("dd-MM-yyyy") + ").");

                    foreach (string code in Regex.Split(match.Groups[2].Value, @"\s*[,&/+]\s*").Select(c => c.Trim().ToUpperInvariant()).Where(c => c.Length > 0))
                    {
                        // Division written under its own column (IAD is both), else a department,
                        // else a division written elsewhere.
                        List<DepartmentMaster> ofDivision = m.Departments.Where(d => Eq(DivisionOf(d), code)).ToList();
                        DepartmentMaster department = m.Departments.FirstOrDefault(d => Eq(d.DEPARTMENT_CODE, code));
                        List<DepartmentMaster> recipients =
                            Eq(code, column) && ofDivision.Count > 0 ? ofDivision
                            : department != null ? new List<DepartmentMaster> { department }
                            : ofDivision;
                        // former department: the copy is now held by its successor(s)
                        if (recipients.Count == 0)
                        {
                            recipients = SuccessorsOf(code, m);
                            if (recipients.Count > 0)
                                row.Warnings.Add("Distribution " + code + " is a former department; registered to its successor "
                                    + string.Join(", ", recipients.Select(r => r.DEPARTMENT_CODE)) + ".");
                        }
                        if (recipients.Count == 0)
                        {
                            row.Errors.Add("Distribution code " + code + " (under " + column + ") is not a department or division with active departments in the master." + SuccessorHint);
                            result.AddMissing("Department", code, "distribution (under " + column + ")", doc.Code, doc.Source, true);
                            continue;
                        }

                        foreach (DepartmentMaster d in recipients)
                            if (!received.TryGetValue(d.DEPARTMENT_CODE, out DateTime existing) || date < existing)
                                received[d.DEPARTMENT_CODE] = date.Value;
                    }
                }
            }

            if (older.Count > 0)
                row.Warnings.Add("The master list still shows revision " + string.Join(", ", older) + " for some recipients; revision " + revision + " is not registered as distributed to them.");
            if (received.Count == 0)
            {
                if (!row.Errors.Any(e => e.StartsWith("Distribution")))
                    row.Warnings.Add("No distribution for revision " + revision + " in the master list. Register it in P4D Maintenance after the import; a document must be in P4D before it can be revised.");
                return null;
            }
            return string.Join("; ", received.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => x.Key + ":" + x.Value.ToString("dd-MM-yyyy")));
        }

        // ---------------------------------------------------------------- files

        private static List<(string Name, int? Revision)> MatchFiles(string code, IList<string> fileNames, HashSet<string> used)
        {
            var parts = Regex.Split(code, @"[/\-\s]+").Where(p => p.Length > 0).ToList();
            if (parts.Count < 2 || !int.TryParse(parts[^1], out int number)) return new List<(string, int?)>();

            // "ITD/SOP-APP-00/001" also matches "SOP-APP-00.001 - User Acceptance Test Rev.00.pdf":
            // any separator, first part optional, leading zeros of the number free.
            string middle = string.Join(Sep, parts.Skip(1).Take(parts.Count - 2).Select(Regex.Escape));
            string pattern = @"(?<![A-Z0-9])(?:" + Regex.Escape(parts[0]) + Sep + ")?" + (middle.Length > 0 ? middle + Sep : "") + "0*" + number + @"(?!\d)";
            if (parts.Count == 2)
                pattern = @"(?<![A-Z0-9])" + Regex.Escape(parts[0]) + Sep + "0*" + number + @"(?!\d)";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            return fileNames
                .Where(f => !used.Contains(f) && regex.IsMatch(Path.GetFileNameWithoutExtension(f)))
                .Select(f => (f, RevisionOf(f)))
                .ToList();
        }

        private static int? RevisionOf(string fileName)
        {
            var matches = Regex.Matches(Path.GetFileNameWithoutExtension(fileName), @"(?<![A-Z])rev(?:isi|ision)?[\s._\-]*0*(\d{1,3})(?!\d)", RegexOptions.IgnoreCase);
            return matches.Count == 0 ? null : int.Parse(matches[^1].Groups[1].Value);
        }

        // ---------------------------------------------------------------- writing the template

        /// <summary>
        /// Writes the rows under the template header (columns A-L are read by the import,
        /// M Source and N Check are for the user) and adds a Conversion sheet.
        /// </summary>
        public static void Write(IWorkbook workbook, ISheet template, Result result)
        {
            ICellStyle date = workbook.CreateCellStyle();
            date.DataFormat = workbook.CreateDataFormat().GetFormat("dd-mm-yyyy");
            ICellStyle errorStyle = Fill(workbook, NPOI.HSSF.Util.HSSFColor.Rose.Index);
            ICellStyle warningStyle = Fill(workbook, NPOI.HSSF.Util.HSSFColor.LightYellow.Index);
            ICellStyle errorDate = workbook.CreateCellStyle(); errorDate.CloneStyleFrom(errorStyle); errorDate.DataFormat = date.DataFormat;
            ICellStyle warningDate = workbook.CreateCellStyle(); warningDate.CloneStyleFrom(warningStyle); warningDate.DataFormat = date.DataFormat;

            IRow header = template.GetRow(0);
            header.CreateCell(12).SetCellValue("Source (not imported)");
            header.CreateCell(13).SetCellValue("Check (not imported)");
            template.SetColumnWidth(11, 40 * 256);
            template.SetColumnWidth(12, 34 * 256);
            template.SetColumnWidth(13, 80 * 256);

            int r = 1;
            foreach (OutRow o in result.Rows)
            {
                IRow row = template.CreateRow(r++);
                ICellStyle style = o.Errors.Count > 0 ? errorStyle : o.Warnings.Count > 0 ? warningStyle : null;
                ICellStyle dateStyle = o.Errors.Count > 0 ? errorDate : o.Warnings.Count > 0 ? warningDate : date;

                void Put(int col, string value)
                {
                    ICell c = row.CreateCell(col);
                    if (value != null) c.SetCellValue(value);
                    if (style != null) c.CellStyle = style;
                }

                Put(0, o.FileName);
                Put(1, o.DocumentCode);
                Put(2, o.DocumentName);
                Put(3, o.CategoryCode);
                Put(4, o.DivisionCode);
                Put(5, o.DepartmentCode);
                Put(6, o.ClassificationCode);
                ICell rev = row.CreateCell(7);
                if (o.Revision != null) rev.SetCellValue(o.Revision.Value);
                if (style != null) rev.CellStyle = style;
                ICell d = row.CreateCell(8);
                if (o.DocumentDate != null) d.SetCellValue(o.DocumentDate.Value);
                d.CellStyle = dateStyle;
                Put(9, null);
                Put(10, o.Reason);
                Put(11, o.Distribution);
                Put(12, o.Source);
                Put(13, string.Join(" ", o.Errors.Select(e => "ERROR: " + e).Concat(o.Warnings.Select(w => "CHECK: " + w))));
            }
            template.CreateFreezePane(0, 1);
            template.SetAutoFilter(new CellRangeAddress(0, Math.Max(0, r - 1), 0, 13));

            ISheet sheet = workbook.CreateSheet("Conversion");
            workbook.SetSheetOrder("Conversion", 1);
            int s = 0;
            sheet.CreateRow(s++).CreateCell(0).SetCellValue("Converted " + DateTime.Now.ToString("dd-MM-yyyy HH:mm") + ": " + result.Rows.Count + " rows, "
                + result.ErrorRows + " with errors (red), " + result.WarningRows + " to check (yellow).");
            sheet.CreateRow(s++).CreateCell(0).SetCellValue("Fix the red rows (column N says why), check the yellow ones, then upload the Template sheet with the ZIP of the documents. Columns M and N are not imported.");
            s++;
            IRow h = sheet.CreateRow(s++);
            string[] titles = { "Master List", "Division (header)", "Department (header)", "Documents", "Rows", "Problem" };
            for (int i = 0; i < titles.Length; i++) h.CreateCell(i).SetCellValue(titles[i]);
            foreach (FileSummary f in result.Files)
            {
                IRow row = sheet.CreateRow(s++);
                row.CreateCell(0).SetCellValue(f.Name);
                row.CreateCell(1).SetCellValue(f.Division ?? "");
                row.CreateCell(2).SetCellValue(f.Department ?? "");
                row.CreateCell(3).SetCellValue(f.Documents);
                row.CreateCell(4).SetCellValue(f.Rows);
                row.CreateCell(5).SetCellValue(f.Problem ?? "");
            }
            s++;

            // Every code missing from the masters, over all lists (2026-11-17).
            var missingCodes = result.MissingCodes.ToList();
            sheet.CreateRow(s++).CreateCell(0).SetCellValue(missingCodes.Count == 0
                ? "All codes used in the master lists are in the system."
                : "Codes not in the system (" + missingCodes.Count + "):");
            if (missingCodes.Count > 0)
            {
                IRow mh = sheet.CreateRow(s++);
                string[] missingTitles = { "Type", "Code", "Severity", "Used in", "Documents", "Master Lists" };
                for (int i = 0; i < missingTitles.Length; i++) mh.CreateCell(i).SetCellValue(missingTitles[i]);
                foreach (MissingCode mc in missingCodes)
                {
                    IRow row = sheet.CreateRow(s++);
                    row.CreateCell(0).SetCellValue(mc.Type);
                    row.CreateCell(1).SetCellValue(mc.Code);
                    row.CreateCell(2).SetCellValue(mc.IsError ? "Error" : "Warning");
                    row.CreateCell(3).SetCellValue(string.Join(", ", mc.UsedIn));
                    row.CreateCell(4).SetCellValue(mc.Documents.Count);
                    row.CreateCell(5).SetCellValue(string.Join(", ", mc.MasterLists));
                }
            }
            s++;
            if (!result.FilesGiven)
                sheet.CreateRow(s++).CreateCell(0).SetCellValue("No document folder was selected, so File Name is empty.");
            else
            {
                sheet.CreateRow(s++).CreateCell(0).SetCellValue("Files in the folder not matched to any document (" + result.UnusedFiles.Count + "):");
                foreach (string f in result.UnusedFiles)
                    sheet.CreateRow(s++).CreateCell(0).SetCellValue(f);
            }
            sheet.SetColumnWidth(0, 48 * 256);
            sheet.SetColumnWidth(1, 26 * 256);
            sheet.SetColumnWidth(2, 26 * 256);
            sheet.SetColumnWidth(5, 60 * 256);
            workbook.SetActiveSheet(0);
        }

        private static ICellStyle Fill(IWorkbook workbook, short color)
        {
            ICellStyle style = workbook.CreateCellStyle();
            style.FillForegroundColor = color;
            style.FillPattern = FillPattern.SolidForeground;
            return style;
        }
    }
}
