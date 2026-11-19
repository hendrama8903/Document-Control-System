using System.Drawing;
using DevExpress.Spreadsheet;

namespace DMS.Services
{
    /// <summary>
    /// Shared layout of the printed QMS forms (2026-11-19): form number, title, Division /
    /// Department / Update Tanggal, a two-level table header, "REVISI: 00", page numbers in
    /// the footer, header rows repeated on every page and a freeze pane. No logo.
    /// Used by the Master List (QCD/FR-QMS-00/004) and the Tanda Terima Dokumen
    /// (QCD/FR-QMS-00/006), so both print the same way in Excel and PDF.
    /// Column A is a narrow margin; the table starts at column B (<see cref="FirstCol"/>).
    /// </summary>
    public static class QmsFormSheet
    {
        public const string FontName = "Arial";
        public static readonly Color HeaderFill = Color.FromArgb(0xD9, 0xE1, 0xF2);
        public static readonly Color BorderColor = Color.FromArgb(0x40, 0x40, 0x40);
        public const string Check = "√";

        public const int FirstCol = 1;
        public const int RowFormNo = 0, RowTitle = 2, RowDivision = 4, RowDepartment = 5, RowHead1 = 7, RowHead2 = 8, RowFirstData = 9;

        public static Workbook NewWorkbook()
        {
            var wb = new Workbook();
            wb.Unit = DevExpress.Office.DocumentUnit.Inch;
            return wb;
        }

        /// <summary>A valid, unique sheet name (max 31 chars, no \ / ? * [ ] :).</summary>
        public static string SheetName(string wanted, HashSet<string> used)
        {
            string baseName = string.IsNullOrWhiteSpace(wanted) ? "Sheet" : wanted.Trim();
            foreach (char c in new[] { '\\', '/', '?', '*', '[', ']', ':' }) baseName = baseName.Replace(c, '-');
            if (baseName.Length > 28) baseName = baseName.Substring(0, 28);
            string name = baseName;
            for (int n = 2; used.Contains(name); n++) name = baseName + " (" + n + ")";
            used.Add(name);
            return name;
        }

        /// <summary>Base font, column widths (index 0 = column A) and the form header block.</summary>
        public static void WriteHeader(Worksheet sheet, double[] columnWidths, string formNumber, string title,
            string division, string department, DateTime updated, bool orgLines = true)
        {
            int lastCol = columnWidths.Length - 1;
            sheet.Cells.Font.Name = FontName;
            sheet.Cells.Font.Size = 9;
            sheet.ActiveView.ShowGridlines = false;
            for (int c = 0; c < columnWidths.Length; c++) sheet.Columns[c].WidthInCharacters = columnWidths[c];

            Cell formNo = sheet.Cells[RowFormNo, FirstCol];
            formNo.Value = formNumber;
            formNo.Font.Size = 8;

            CellRange titleRange = sheet.Range.FromLTRB(FirstCol, RowTitle, lastCol, RowTitle);
            titleRange.Merge();
            titleRange.Value = title;
            titleRange.Font.Bold = true;
            titleRange.Font.Size = 16;
            titleRange.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            sheet.Rows[RowTitle].Height = 0.4;

            if (orgLines)   // a report across the whole organisation has no Division / Department
            {
                LabelValue(sheet, RowDivision, "Division", division);
                LabelValue(sheet, RowDepartment, "Department", department);
            }

            Cell updLabel = sheet.Cells[RowDivision, lastCol - 1];
            updLabel.Value = "Update Tanggal:";
            updLabel.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Right;
            Cell updValue = sheet.Cells[RowDivision, lastCol];
            updValue.Value = updated.Date;
            updValue.NumberFormat = "dd-mmm-yyyy";
            updValue.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Left;

            sheet.Rows[RowHead1].Height = 0.34;   // room for a caption that wraps (Penarikan Dokumen Lama)
            sheet.Rows[RowHead2].Height = 0.3;
        }

        /// <summary>An extra "label: value" on the right, under Update Tanggal (e.g. Periode).</summary>
        public static void WriteRightInfo(Worksheet sheet, int lastCol, string label, string value)
        {
            // one right-aligned text over the last four columns: a long value stays inside the page
            CellRange r = sheet.Range.FromLTRB(Math.Max(FirstCol, lastCol - 3), RowDepartment, lastCol, RowDepartment);
            r.Merge();
            r.Value = label + " " + value;
            r.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Right;
        }

        private static void LabelValue(Worksheet sheet, int row, string label, string value)
        {
            // label over the No. + No. Dokumen columns, value from the Nama Dokumen column on
            CellRange labelRange = sheet.Range.FromLTRB(FirstCol, row, FirstCol + 1, row);
            labelRange.Merge();
            labelRange.Value = label;
            sheet.Cells[row, FirstCol + 2].Value = ": " + (string.IsNullOrWhiteSpace(value) ? "Semua" : value);
            sheet.Cells[row, FirstCol + 2].Font.Bold = true;
        }

        public static void StyleHeader(CellRange range)
        {
            range.Font.Bold = true;
            range.Fill.BackgroundColor = HeaderFill;
            range.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            range.Alignment.Vertical = SpreadsheetVerticalAlignment.Center;
            range.Alignment.WrapText = true;
            range.Borders.SetAllBorders(BorderColor, BorderLineStyle.Thin);
        }

        /// <summary>A header cell spanning both header rows.</summary>
        public static void HeaderSpan(Worksheet sheet, int col, string text)
        {
            CellRange r = sheet.Range.FromLTRB(col, RowHead1, col, RowHead2);
            r.Merge();
            r.Value = text;
            StyleHeader(r);
        }

        /// <summary>A group caption on the first header row with one sub-caption per column below it.</summary>
        public static void HeaderGroup(Worksheet sheet, int firstCol, int lastCol, string caption, string[] subCaptions)
        {
            CellRange top = sheet.Range.FromLTRB(firstCol, RowHead1, lastCol, RowHead1);
            if (lastCol > firstCol) top.Merge();
            top.Value = caption;
            StyleHeader(top);
            for (int i = 0; i < subCaptions.Length && firstCol + i <= lastCol; i++)
                sheet.Cells[RowHead2, firstCol + i].Value = subCaptions[i];
            StyleHeader(sheet.Range.FromLTRB(firstCol, RowHead2, lastCol, RowHead2));
        }

        /// <summary>"Tidak ada dokumen" across the table; returns the next row.</summary>
        public static int EmptyRow(Worksheet sheet, int row, int lastCol)
        {
            CellRange empty = sheet.Range.FromLTRB(FirstCol, row, lastCol, row);
            empty.Merge();
            empty.Value = "Tidak ada dokumen";
            empty.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            return row + 1;
        }

        /// <summary>Borders, top alignment and wrapping for the data rows [RowFirstData, endRow).</summary>
        public static void StyleBody(Worksheet sheet, int endRow, int lastCol)
        {
            if (endRow <= RowFirstData) return;
            CellRange body = sheet.Range.FromLTRB(FirstCol, RowFirstData, lastCol, endRow - 1);
            body.Borders.SetAllBorders(BorderColor, BorderLineStyle.Thin);
            body.Alignment.Vertical = SpreadsheetVerticalAlignment.Top;
            body.Alignment.WrapText = true;
        }

        /// <summary>
        /// Optional italic note, "REVISI: 00", then page setup: landscape, one page wide,
        /// header rows on every page, "Halaman x dari y" in the footer, freeze pane after
        /// <paramref name="freezeLastCol"/>.
        /// </summary>
        public static void Finish(Worksheet sheet, int row, int lastCol, string note, string formRevision,
            System.Drawing.Printing.PaperKind paper, int freezeLastCol)
        {
            row++;
            if (!string.IsNullOrEmpty(note))
            {
                Cell n = sheet.Cells[row, FirstCol];
                n.Value = note;
                n.Font.Size = 8;
                n.Font.Italic = true;
                row += 2;
            }
            if (!string.IsNullOrEmpty(formRevision))   // a report without a QMS form has no form revision
            {
                sheet.Cells[row, FirstCol].Value = formRevision;
                sheet.Cells[row, FirstCol].Font.Size = 8;
            }

            sheet.ActiveView.Orientation = PageOrientation.Landscape;
            sheet.ActiveView.PaperKind = paper;
            sheet.ActiveView.Margins.Left = 0.4f;
            sheet.ActiveView.Margins.Right = 0.4f;
            sheet.ActiveView.Margins.Top = 0.5f;
            sheet.ActiveView.Margins.Bottom = 0.6f;
            sheet.PrintOptions.FitToPage = true;
            sheet.PrintOptions.FitToWidth = 1;
            sheet.PrintOptions.FitToHeight = 0;
            sheet.PrintOptions.CenterHorizontally = true;
            sheet.SetPrintRange(sheet.Range.FromLTRB(0, 0, lastCol, row));
            sheet.PrintOptions.PrintTitles.SetRows(RowFormNo, RowHead2);
            sheet.HeaderFooterOptions.OddFooter.Right = "&8Halaman &P dari &N";
            sheet.FreezePanes(RowHead2, freezeLastCol);
        }
    }
}
