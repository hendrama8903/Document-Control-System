using DevExpress.Spreadsheet;
using DMS.Models.DB;

namespace DMS.Services
{
    /// <summary>
    /// "Tanda Terima Dokumen" (form QCD/FR-QMS-00/006) as Excel and PDF, in the same style
    /// as the Master List (2026-11-19, QmsFormSheet). One sheet per receiving department,
    /// documents from the smallest running number; Paraf, Jumlah, Keterangan and Paraf QMS
    /// stay empty for handwriting.
    /// </summary>
    public static class DocumentReceiptFormService
    {
        public const string FormNumber = "QCD/FR-QMS-00/006";
        public const string FormTitle = "TANDA TERIMA DOKUMEN";
        public const string FormRevision = "REVISI: 00";

        // columns (0-based): A margin, B No., C No. Dokumen, D Nama Dokumen, E No. Revisi,
        // F-I Diterima Oleh (Tanggal, Div./Dept.OF, Nama, Paraf), J-L Penarikan Dokumen Lama
        // (Ya, Tidak, Jumlah), M Keterangan, N Paraf QMS
        private const int ColNo = 1, ColCode = 2, ColName = 3, ColRev = 4, ColDate = 5, ColDept = 6, ColReceiver = 7,
            ColSign = 8, ColYes = 9, ColNo2 = 10, ColQty = 11, ColNote = 12, ColQmsSign = 13;
        private static readonly double[] Widths = { 1.5, 5, 19, 32, 7, 10, 11, 20, 10, 5, 5, 7, 20, 10 };

        public class Group
        {
            public string Division { get; set; }
            public string DivisionName { get; set; }
            public string DepartmentCode { get; set; }
            public string DepartmentName { get; set; }
            public List<DocumentReceiptReport> Rows { get; set; } = new List<DocumentReceiptReport>();
        }

        /// <summary>Receipts per receiving department, each ordered by document running number then date.</summary>
        public static List<Group> GroupByDepartment(IEnumerable<DocumentReceiptReport> rows)
        {
            return rows
                .GroupBy(r => (r.DIVISION ?? "") + "|" + (r.DEPARTMENT_CODE ?? ""))
                .Select(g => new Group
                {
                    Division = g.First().DIVISION,
                    DivisionName = g.First().DIVISION_NAME,
                    DepartmentCode = g.First().DEPARTMENT_CODE,
                    DepartmentName = g.First().DEPARTMENT_NAME,
                    Rows = g.OrderBy(r => MasterListMatrixService.SequenceNumber(r.DOCUMENT_CODE))
                            .ThenBy(r => r.DOCUMENT_CODE, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(r => r.RECEIVED_DATE)
                            .ToList()
                })
                .OrderBy(g => g.Division, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.DepartmentCode, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static Workbook BuildWorkbook(IEnumerable<DocumentReceiptReport> rows)
        {
            var groups = GroupByDepartment(rows);
            if (groups.Count == 0) groups.Add(new Group());
            DateTime now = DateTime.Now;

            var wb = QmsFormSheet.NewWorkbook();
            wb.BeginUpdate();
            try
            {
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < groups.Count; i++)
                {
                    Worksheet sheet = i == 0 ? wb.Worksheets[0] : wb.Worksheets.Add();
                    sheet.Name = QmsFormSheet.SheetName(groups[i].DepartmentCode ?? "Tanda Terima", usedNames);
                    FillSheet(sheet, groups[i], now);
                }
                wb.Worksheets.ActiveWorksheet = wb.Worksheets[0];
            }
            finally
            {
                wb.EndUpdate();
            }
            return wb;
        }

        private static void FillSheet(Worksheet sheet, Group group, DateTime now)
        {
            int lastCol = Widths.Length - 1;
            QmsFormSheet.WriteHeader(sheet, Widths, FormNumber, FormTitle,
                group.DivisionName ?? group.Division, group.DepartmentName ?? group.DepartmentCode, now);

            QmsFormSheet.HeaderSpan(sheet, ColNo, "No.");
            QmsFormSheet.HeaderSpan(sheet, ColCode, "No. Dokumen");
            QmsFormSheet.HeaderSpan(sheet, ColName, "Nama Dokumen");
            QmsFormSheet.HeaderSpan(sheet, ColRev, "No. Revisi");
            QmsFormSheet.HeaderGroup(sheet, ColDate, ColSign, "Diterima Oleh", new[] { "Tanggal", "Div. / Dept.OF", "Nama", "Paraf" });
            QmsFormSheet.HeaderGroup(sheet, ColYes, ColQty, "Penarikan Dokumen Lama", new[] { "Ya", "Tidak", "Jumlah" });
            QmsFormSheet.HeaderSpan(sheet, ColNote, "Keterangan");
            QmsFormSheet.HeaderSpan(sheet, ColQmsSign, "Paraf QMS");

            int row = QmsFormSheet.RowFirstData;
            int no = 1;
            foreach (var r in group.Rows)
            {
                int rev = r.REVISION ?? 0;
                sheet.Cells[row, ColNo].Value = no++;
                sheet.Cells[row, ColCode].Value = r.DOCUMENT_CODE ?? "";
                sheet.Cells[row, ColName].Value = r.DOCUMENT_NAME ?? "";
                sheet.Cells[row, ColRev].Value = rev;
                if (r.RECEIVED_DATE.HasValue)
                {
                    sheet.Cells[row, ColDate].Value = r.RECEIVED_DATE.Value.Date;
                    sheet.Cells[row, ColDate].NumberFormat = "dd-mmm-yy";
                }
                sheet.Cells[row, ColDept].Value = string.Join(" / ", new[] { r.DIVISION, r.DEPARTMENT_CODE }.Where(s => !string.IsNullOrWhiteSpace(s)));
                sheet.Cells[row, ColReceiver].Value = r.RECEIVED_BY_NAME ?? "";
                // an older revision exists only from Rev. 1 on: that copy has to be withdrawn
                sheet.Cells[row, rev > 0 ? ColYes : ColNo2].Value = QmsFormSheet.Check;
                // room for a handwritten paraf; taller when the document name wraps
                int nameLines = Math.Max(1, (int)Math.Ceiling((r.DOCUMENT_NAME ?? "").Length / 30.0));
                sheet.Rows[row].Height = Math.Max(0.32, nameLines * 0.16 + 0.08);
                row++;
            }
            if (group.Rows.Count == 0) row = QmsFormSheet.EmptyRow(sheet, row, lastCol);

            QmsFormSheet.StyleBody(sheet, row, lastCol);
            int firstData = QmsFormSheet.RowFirstData;
            foreach (int c in new[] { ColNo, ColRev, ColDate, ColDept, ColYes, ColNo2, ColQty })
                sheet.Range.FromLTRB(c, firstData, c, row - 1).Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            CellRange checks = sheet.Range.FromLTRB(ColYes, firstData, ColNo2, row - 1);
            checks.Font.Bold = true;
            checks.Font.Size = 11;

            // freeze pane: header rows + No. .. Nama Dokumen
            QmsFormSheet.Finish(sheet, row, lastCol, null, FormRevision, System.Drawing.Printing.PaperKind.A4, ColName);
        }
    }
}
