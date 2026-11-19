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
    /// Print Request Report (2026-11-19) in the Master List layout: one row per document +
    /// revision, one column per requesting division, each approved request as a line
    /// "QTY:DEPT-ddMMMyy" (request date; "2W" = 2 colour copies). Blocks per department that owns
    /// the document. Lines not printed yet are grey. Screen, Excel and PDF share Build().
    /// </summary>
    public static class PrintRequestMatrixService
    {
        public const string Title = "LAPORAN PERMINTAAN PRINT DOKUMEN";

        public class Line
        {
            public string RequestNo { get; set; }
            public DateTime? RequestDate { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public int Revision { get; set; }
            public string Category { get; set; }
            public bool Colour { get; set; }
            public int Qty { get; set; }
            public bool Printed { get; set; }
            public DateTime? PrintedDate { get; set; }
            public string ReqDivision { get; set; }
            public string ReqDepartment { get; set; }
            public string OwnDivision { get; set; }
            public string OwnDivisionName { get; set; }
            public string OwnDepartment { get; set; }
            public string OwnDepartmentName { get; set; }
        }

        public class Report
        {
            public DateTime GeneratedAt { get; set; }
            public DateTime? DateFrom { get; set; }
            public DateTime? DateTo { get; set; }
            public List<MasterListMatrixService.Division> Divisions { get; set; } = new List<MasterListMatrixService.Division>();
            public List<Group> Groups { get; set; } = new List<Group>();
        }

        public class Group
        {
            public string Division { get; set; }
            public string DivisionName { get; set; }
            public string DepartmentCode { get; set; }
            public string DepartmentName { get; set; }
            public List<Row> Rows { get; set; } = new List<Row>();
        }

        public class Row
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public int Revision { get; set; }
            public int TotalQty { get; set; }
            /// <summary>Requesting division -> its request lines.</summary>
            public Dictionary<string, List<Req>> Requests { get; set; } = new Dictionary<string, List<Req>>();
        }

        public class Req
        {
            public string RequestNo { get; set; }
            public int Qty { get; set; }
            public string Department { get; set; }
            public DateTime? Date { get; set; }
            public bool Colour { get; set; }
            public bool Printed { get; set; }
            public DateTime? PrintedDate { get; set; }
            public string Text => Qty + (Colour ? "W" : "") + ":" + Department + (Date.HasValue ? "-" + MasterListMatrixService.FormatShortDate(Date.Value) : "");
        }

        // ------------------------------------------------------------------ data

        public static Report Load(DBContext db, DateTime? from, DateTime? to, string division = null, string department = null)
        {
            var sets = new List<List<Dictionary<string, object>>>();
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "[dbo].[sp_PrintRequestReport_Matrix]";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@DATE_FROM", SqlDbType.Date) { Value = (object)from?.Date ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DATE_TO", SqlDbType.Date) { Value = (object)to?.Date ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DIVISION", (object)Blank(division) ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@DEPARTMENT_CODE", (object)Blank(department) ?? DBNull.Value));
                using var rd = command.ExecuteReader();
                do
                {
                    var rows = new List<Dictionary<string, object>>();
                    while (rd.Read())
                    {
                        var row = new Dictionary<string, object>(rd.FieldCount, StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < rd.FieldCount; i++) row[rd.GetName(i)] = rd.IsDBNull(i) ? null : rd.GetValue(i);
                        rows.Add(row);
                    }
                    sets.Add(rows);
                } while (rd.NextResult());
            }
            finally
            {
                if (wasClosed) connection.Close();
            }

            var lines = sets[0].Select(r => new Line
            {
                RequestNo = Str(r, "REQUEST_NO"),
                RequestDate = r["REQUEST_DATE"] as DateTime?,
                Code = Str(r, "DOCUMENT_CODE"),
                Name = Str(r, "DOCUMENT_NAME"),
                Revision = Convert.ToInt32(r["REVISION"]),
                Category = Str(r, "CATEGORY_CODE"),
                Colour = Str(r, "COPY_TYPE") == "2",
                Qty = Convert.ToInt32(r["COPY_QTY"]),
                Printed = Str(r, "PRINT_STATUS") == "1",
                PrintedDate = r["PRINTED_DT"] as DateTime?,
                ReqDivision = Str(r, "REQ_DIVISION"),
                ReqDepartment = Str(r, "REQ_DEPARTMENT_CODE"),
                OwnDivision = Str(r, "OWN_DIVISION"),
                OwnDivisionName = Str(r, "OWN_DIVISION_NAME"),
                OwnDepartment = Str(r, "OWN_DEPARTMENT_CODE"),
                OwnDepartmentName = Str(r, "OWN_DEPARTMENT_NAME")
            }).ToList();
            var divisions = sets[1].Select(r => new MasterListMatrixService.Division { Code = Str(r, "DIVISION_CODE"), Name = Str(r, "DIVISION_NAME") })
                .Where(d => d.Code != null).ToList();

            return Build(lines, divisions, from, to);
        }

        /// <summary>Groups the request lines into the matrix (also used with sample data in tests).</summary>
        public static Report Build(List<Line> lines, List<MasterListMatrixService.Division> divisions, DateTime? from, DateTime? to)
        {
            var report = new Report { GeneratedAt = DateTime.Now, DateFrom = from, DateTo = to };

            foreach (var gl in lines.GroupBy(l => (l.OwnDivision ?? "") + "|" + (l.OwnDepartment ?? "")))
            {
                var first = gl.First();
                var group = new Group
                {
                    Division = first.OwnDivision,
                    DivisionName = first.OwnDivisionName,
                    DepartmentCode = first.OwnDepartment,
                    DepartmentName = first.OwnDepartmentName
                };
                foreach (var dl in gl.GroupBy(l => (l.Code ?? "") + "|" + l.Revision, StringComparer.OrdinalIgnoreCase))
                {
                    var d0 = dl.First();
                    var row = new Row { Code = d0.Code, Name = d0.Name, Category = d0.Category, Revision = d0.Revision, TotalQty = dl.Sum(x => x.Qty) };
                    foreach (var l in dl.OrderBy(x => x.RequestDate).ThenBy(x => x.ReqDepartment, StringComparer.OrdinalIgnoreCase))
                    {
                        string div = l.ReqDivision ?? "-";
                        if (!row.Requests.TryGetValue(div, out var list)) row.Requests[div] = list = new List<Req>();
                        list.Add(new Req
                        {
                            RequestNo = l.RequestNo, Qty = l.Qty, Department = l.ReqDepartment ?? "-", Date = l.RequestDate,
                            Colour = l.Colour, Printed = l.Printed, PrintedDate = l.PrintedDate
                        });
                    }
                    group.Rows.Add(row);
                }
                Sort(group.Rows, null, false);
                report.Groups.Add(group);
            }
            report.Groups = report.Groups
                .OrderBy(g => g.Division, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.DepartmentCode, StringComparer.OrdinalIgnoreCase).ToList();

            // columns: the active divisions plus any division that only old requests point to
            var all = divisions.ToList();
            foreach (var code in lines.Select(l => l.ReqDivision ?? "-").Distinct(StringComparer.OrdinalIgnoreCase)
                         .Where(c => !all.Any(d => string.Equals(d.Code, c, StringComparison.OrdinalIgnoreCase))))
                all.Add(new MasterListMatrixService.Division { Code = code, Name = code });
            report.Divisions = MasterListMatrixService.OrderDivisions(all);
            return report;
        }

        /// <summary>Same orders as the Master List: "number" (default), "code", "name", "type"; "qty" = total QTY.</summary>
        public static void Sort(List<Row> rows, string sort, bool descending)
        {
            Comparison<Row> byNumber = (a, b) =>
            {
                int c = MasterListMatrixService.SequenceNumber(a.Code).CompareTo(MasterListMatrixService.SequenceNumber(b.Code));
                if (c == 0) c = string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : a.Revision.CompareTo(b.Revision);
            };
            Comparison<Row> cmp = (sort ?? "").ToLowerInvariant() switch
            {
                "code" => (a, b) => { int c = string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : a.Revision.CompareTo(b.Revision); },
                "name" => (a, b) => { int c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : byNumber(a, b); },
                "type" => (a, b) => { int c = string.Compare(a.Category, b.Category, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : byNumber(a, b); },
                "qty" => (a, b) => { int c = a.TotalQty.CompareTo(b.TotalQty); return c != 0 ? c : byNumber(a, b); },
                _ => byNumber
            };
            rows.Sort(descending ? (a, b) => cmp(b, a) : cmp);
        }

        private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        private static string Str(Dictionary<string, object> r, string key) =>
            r.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture)?.Trim() : null;

        public static string PeriodText(DateTime? from, DateTime? to)
        {
            string f(DateTime? d) => d.HasValue ? d.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : "...";
            return !from.HasValue && !to.HasValue ? "Semua" : f(from) + " s/d " + f(to);
        }

        // ------------------------------------------------------------------ workbook

        private static readonly Color PendingColor = Color.FromArgb(0x98, 0xA2, 0xB3);

        // columns (0-based): A margin, B No., C No. Dokumen, D Nama, E Jenis, F No. Revisi, G Total QTY, H.. divisions
        private const int ColNo = 1, ColCode = 2, ColName = 3, ColType = 4, ColRev = 5, ColTotal = 6, ColFirstDiv = 7;

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
                    sheet.Name = QmsFormSheet.SheetName(groups[i].DepartmentCode ?? "Print Request", usedNames);
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
            int lastCol = ColFirstDiv + Math.Max(report.Divisions.Count, 1) - 1;
            var widths = new List<double> { 1.5, 5, 19, 32, 8, 7, 8 };
            widths.AddRange(Enumerable.Repeat(12.5, lastCol - ColFirstDiv + 1));   // "12W:QCP-12Nov26" in italic fits on one line
            QmsFormSheet.WriteHeader(sheet, widths.ToArray(), "", Title,
                group.DivisionName ?? group.Division, group.DepartmentName ?? group.DepartmentCode, report.GeneratedAt);
            QmsFormSheet.WriteRightInfo(sheet, lastCol, "Periode Request:", PeriodText(report.DateFrom, report.DateTo));

            QmsFormSheet.HeaderSpan(sheet, ColNo, "No.");
            QmsFormSheet.HeaderSpan(sheet, ColCode, "No. Dokumen");
            QmsFormSheet.HeaderSpan(sheet, ColName, "Nama Dokumen");
            QmsFormSheet.HeaderSpan(sheet, ColType, "Jenis Dokumen");
            QmsFormSheet.HeaderSpan(sheet, ColRev, "No. Revisi");
            QmsFormSheet.HeaderSpan(sheet, ColTotal, "Total QTY");
            QmsFormSheet.HeaderGroup(sheet, ColFirstDiv, lastCol, "Permintaan Print per Divisi (QTY:Dept-Tgl Request)",
                report.Divisions.Count > 0 ? report.Divisions.Select(d => d.Code).ToArray() : new[] { "-" });

            int row = QmsFormSheet.RowFirstData;
            int no = 1;
            foreach (var r in group.Rows)
            {
                sheet.Cells[row, ColNo].Value = no++;
                sheet.Cells[row, ColCode].Value = r.Code ?? "";
                sheet.Cells[row, ColName].Value = r.Name ?? "";
                sheet.Cells[row, ColType].Value = r.Category ?? "";
                sheet.Cells[row, ColRev].Value = r.Revision;
                sheet.Cells[row, ColTotal].Value = r.TotalQty;
                sheet.Cells[row, ColTotal].Font.Bold = true;

                for (int d = 0; d < report.Divisions.Count; d++)
                {
                    if (!r.Requests.TryGetValue(report.Divisions[d].Code, out var list) || list.Count == 0) continue;
                    var rich = new RichTextString();
                    for (int i = 0; i < list.Count; i++)
                    {
                        string text = list[i].Text + (i < list.Count - 1 ? "\n" : "");
                        rich.AddTextRun(text, new RichTextRunFont(QmsFormSheet.FontName, 7.5, list[i].Printed ? Color.Black : PendingColor)
                        { Italic = !list[i].Printed });
                    }
                    Cell cell = sheet.Cells[row, ColFirstDiv + d];
                    cell.SetRichText(rich);
                    sheet.Comments.Add(cell, "DMS", string.Join("\n", list.Select(x =>
                        x.RequestNo + ": " + (x.Printed
                            ? "dicetak " + (x.PrintedDate.HasValue ? x.PrintedDate.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : "")
                            : "belum dicetak"))));
                }
                int lines = r.Requests.Values.Select(l => l.Count).DefaultIfEmpty(1).Max();
                int nameLines = Math.Max(1, (int)Math.Ceiling((r.Name ?? "").Length / 30.0));
                sheet.Rows[row].Height = Math.Max(0.22, Math.Max(lines * 0.125, nameLines * 0.16) + 0.06);
                row++;
            }
            if (group.Rows.Count == 0) row = QmsFormSheet.EmptyRow(sheet, row, lastCol);

            QmsFormSheet.StyleBody(sheet, row, lastCol);
            int firstData = QmsFormSheet.RowFirstData;
            sheet.Range.FromLTRB(ColNo, firstData, ColNo, row - 1).Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            sheet.Range.FromLTRB(ColType, firstData, ColTotal, row - 1).Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;

            QmsFormSheet.Finish(sheet, row, lastCol,
                "Isi sel: QTY:Departemen peminta-Tanggal request, W setelah QTY = warna. Abu-abu miring = belum dicetak. Hanya request yang sudah Approved.",
                null, System.Drawing.Printing.PaperKind.A3, ColTotal);
        }
    }
}
