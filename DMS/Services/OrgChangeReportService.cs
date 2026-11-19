using System.Globalization;
using DevExpress.Spreadsheet;

namespace DMS.Services
{
    /// <summary>
    /// "Riwayat Perubahan Organisasi" (2026-11-19, QMS answer no. 8: a report is enough): the valid
    /// changes of the Organization Change register as Excel / PDF in the Master List style.
    /// One row per change; old and new units one per line; the last column says whether each
    /// old code is still active and what still depends on it.
    /// </summary>
    public static class OrgChangeReportService
    {
        public const string Title = "RIWAYAT PERUBAHAN ORGANISASI";

        // columns (0-based): A margin, B No., C Tgl Efektif, D Jenis, E Level, F Kode Lama, G Kode Baru,
        // H Referensi HR, I Keterangan, J Status Kode Lama, K Dicatat
        private const int ColNo = 1, ColDate = 2, ColType = 3, ColLevel = 4, ColOld = 5, ColNew = 6, ColHr = 7, ColNote = 8, ColState = 9, ColBy = 10;
        private static readonly double[] Widths = { 1.5, 5, 11, 20, 11, 30, 30, 18, 34, 30, 16 };

        public static string FormatDate(DateTime? d) => d.HasValue ? d.Value.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) : "";

        public static string OldText(OrgChangeService.Change c) => string.Join("\n", c.Maps.Where(m => m.OldCode != null)
            .GroupBy(m => m.OldCode + "|" + m.OldParent).Select(g => g.First())
            .Select(m => Unit(m.OldCode, m.OldName, m.OldParent, c.Type == "MOVE" || c.Level == "SECTION")));

        public static string NewText(OrgChangeService.Change c) => string.Join("\n", c.Maps.Where(m => m.NewCode != null)
            .GroupBy(m => m.NewCode + "|" + m.NewParent).Select(g => g.First())
            .Select(m => Unit(m.NewCode, m.NewName, m.NewParent, c.Type == "MOVE" || (m.NewLevel ?? c.Level) == "SECTION")
                         + (c.Type == "SPLIT" && m.IsOwner ? " [pemilik dokumen]" : "")
                         + (c.Type == "DOWNGRADE" ? " (section)" : "")));

        private static string Unit(string code, string name, string parent, bool showParent) =>
            (showParent && !string.IsNullOrEmpty(parent) ? parent + " / " : "") + code + (string.IsNullOrEmpty(name) ? "" : " - " + name);

        /// <summary>"QPD: nonaktif s/d 31-Dec-2025" or "QPD: aktif - 3 document(s) owned, 2 user(s)".</summary>
        public static string StateText(OrgChangeService.Change c) => string.Join("\n", c.Impacts.Select(i =>
            i.Code + ": " + (i.CodeKept ? "kode tetap dipakai"
                : i.MasterState == "NotInMaster" ? "tidak ada di master"
                : i.MasterState == "Inactive" ? "nonaktif" + (i.ValidTo.HasValue && i.ValidTo.Value.Year < 9999 ? " s/d " + FormatDate(i.ValidTo) : "")
                : "aktif" + (i.Blocked ? " - " + OrgChangeService.Describe(i) : ""))
            + (i.DocumentsNumbered > 0 ? "; " + i.DocumentsNumbered + " nomor dokumen masih memakai kode ini" : "")))
            + (string.IsNullOrEmpty(c.Moved) ? "" : "\nDipindahkan: " + c.Moved);

        public static Workbook BuildWorkbook(List<OrgChangeService.Change> changes, DateTime? from, DateTime? to)
        {
            var wb = QmsFormSheet.NewWorkbook();
            wb.BeginUpdate();
            try
            {
                Worksheet sheet = wb.Worksheets[0];
                sheet.Name = "Perubahan Organisasi";
                int lastCol = Widths.Length - 1;
                QmsFormSheet.WriteHeader(sheet, Widths, "", Title, null, null, DateTime.Now, orgLines: false);
                QmsFormSheet.WriteRightInfo(sheet, lastCol, "Periode Efektif:", PrintRequestMatrixService.PeriodText(from, to));

                QmsFormSheet.HeaderSpan(sheet, ColNo, "No.");
                QmsFormSheet.HeaderSpan(sheet, ColDate, "Tanggal Efektif");
                QmsFormSheet.HeaderSpan(sheet, ColType, "Jenis Perubahan");
                QmsFormSheet.HeaderSpan(sheet, ColLevel, "Level");
                QmsFormSheet.HeaderGroup(sheet, ColOld, ColNew, "Unit Organisasi", new[] { "Lama", "Baru" });
                QmsFormSheet.HeaderSpan(sheet, ColHr, "Referensi HR");
                QmsFormSheet.HeaderSpan(sheet, ColNote, "Keterangan");
                QmsFormSheet.HeaderSpan(sheet, ColState, "Status Kode Lama");
                QmsFormSheet.HeaderSpan(sheet, ColBy, "Dicatat Oleh");

                int row = QmsFormSheet.RowFirstData, no = 1;
                foreach (var c in changes)
                {
                    sheet.Cells[row, ColNo].Value = no++;
                    if (c.EffectiveDate.HasValue)
                    {
                        sheet.Cells[row, ColDate].Value = c.EffectiveDate.Value;
                        sheet.Cells[row, ColDate].NumberFormat = "dd-mmm-yyyy";
                    }
                    else
                    {
                        sheet.Cells[row, ColDate].Value = "(belum diisi)";
                    }
                    sheet.Cells[row, ColType].Value = c.TypeLabel;
                    sheet.Cells[row, ColLevel].Value = c.LevelLabel;
                    sheet.Cells[row, ColOld].Value = OldText(c);
                    sheet.Cells[row, ColNew].Value = NewText(c);
                    sheet.Cells[row, ColHr].Value = (c.HrReference ?? "") + (c.HasAttachment ? "\n(memo terlampir)" : "");
                    sheet.Cells[row, ColNote].Value = c.Description ?? "";
                    sheet.Cells[row, ColState].Value = StateText(c);
                    sheet.Cells[row, ColBy].Value = c.CreatedBy + "\n" + FormatDate(c.CreatedDt);

                    int lines = new[]
                    {
                        Math.Max(1, c.Maps.Count(m => m.OldCode != null)), Math.Max(1, c.Maps.Count(m => m.NewCode != null)),
                        Math.Max(1, c.Impacts.Count), (int)Math.Ceiling((c.Description ?? "").Length / 36.0), 2
                    }.Max();
                    sheet.Rows[row].Height = Math.Max(0.3, lines * 0.16 + 0.06);
                    row++;
                }
                if (changes.Count == 0)
                {
                    CellRange empty = sheet.Range.FromLTRB(QmsFormSheet.FirstCol, row, lastCol, row);
                    empty.Merge();
                    empty.Value = "Tidak ada perubahan organisasi pada periode ini";
                    empty.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
                    row++;
                }

                QmsFormSheet.StyleBody(sheet, row, lastCol);
                foreach (int col in new[] { ColNo, ColDate, ColLevel })
                    sheet.Range.FromLTRB(col, QmsFormSheet.RowFirstData, col, row - 1).Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;

                QmsFormSheet.Finish(sheet, row, lastCol,
                    "Sumber: Master Data > Organization Change. Nomor dokumen lama tetap berlaku; nomor baru boleh dipakai saat revisi dengan rujukan ke nomor lama (keputusan QMS).",
                    null, System.Drawing.Printing.PaperKind.A4, ColLevel);
            }
            finally
            {
                wb.EndUpdate();
            }
            return wb;
        }
    }
}
