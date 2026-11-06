using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.RegularExpressions;
using DevExpress.Spreadsheet;
using DMS.Models.DB;

namespace DMS.Services
{
    /// <summary>
    /// Penanda pengesahan berbasis Named Range "DCS_" di file Excel template/dokumen
    /// (opsi B, keputusan Hendra 2026-11-06). Posisi field & kotak tanda tangan dibaca
    /// dari file itu sendiri, bukan dari koordinat ROW/COL di TB_M_EXCEL_TEMPLATE, jadi
    /// template boleh digeser/diganti tanpa mengubah database atau kode.
    ///
    /// Konvensi nama (scope Workbook ATAU Sheet, dua-duanya dibaca):
    ///   DCS_F_&lt;PROPERTI&gt;   isi field DocumentMaintenance (mis. DCS_F_DOCUMENT_CODE).
    ///                       Properti tanggal diformat dd-MMM-yy (id-ID), kosong = "-".
    ///   DCS_SIGN_&lt;KOTAK&gt;   area gambar tanda tangan
    ///   DCS_NAME_&lt;KOTAK&gt;   baris nama    (opsional - tidak ada = nama tidak dicetak)
    ///   DCS_TITLE_&lt;KOTAK&gt;  baris jabatan (opsional - tidak ada = jabatan tidak dicetak)
    /// KOTAK: DIBUAT | DIPERIKSA (=STEP1) | DISETUJUI (=STEP2) | STEP&lt;n&gt; | LAST |
    ///        POS&lt;POSITION_ID&gt; (kotak khusus jabatan, mis. POS3 = Dept. Head).
    /// </summary>
    public static class TemplateMarkerService
    {
        public const string Prefix = "DCS_";

        static readonly string[] BoxPartPrefixes = { "SIGN_", "NAME_", "TITLE_" };
        static readonly Regex BoxKeyPattern = new Regex(@"^(DIBUAT|DIPERIKSA|DISETUJUI|LAST|STEP[1-9][0-9]?|POS[0-9]+)$", RegexOptions.IgnoreCase);

        // Field yang dianjurkan ada di setiap template (header dokumen).
        static readonly string[] RecommendedFields = { "DOCUMENT_CODE", "DOCUMENT_TRANSACTION_NAME", "REVISION", "DOCUMENT_DATE" };

        // ===================================================================
        // Deteksi & pengumpulan penanda
        // ===================================================================

        public static bool HasMarkers(Workbook workbook)
        {
            if (workbook.DefinedNames.Any(dn => IsDcsName(dn.Name))) return true;
            return workbook.Worksheets.Any(ws => ws.DefinedNames.Any(dn => IsDcsName(dn.Name)));
        }

        static bool IsDcsName(string name) => name != null && name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Penanda DCS_ yang menunjuk ke sheet ini, key = nama tanpa prefix "DCS_".
        /// Penanda rusak (#REF! karena sel targetnya dihapus) dilewati.
        /// </summary>
        public static Dictionary<string, CellRange> Collect(Workbook workbook, Worksheet sheet)
        {
            var markers = new Dictionary<string, CellRange>(StringComparer.OrdinalIgnoreCase);

            void CollectFrom(IEnumerable<DefinedName> names)
            {
                foreach (DefinedName dn in names)
                {
                    if (!IsDcsName(dn.Name)) continue;
                    CellRange range = TryGetRange(dn);
                    if (range == null || !string.Equals(range.Worksheet.Name, sheet.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    markers[dn.Name.Substring(Prefix.Length)] = range;
                }
            }

            CollectFrom(workbook.DefinedNames);
            CollectFrom(sheet.DefinedNames); // scope sheet menimpa scope workbook bila namanya sama
            return markers;
        }

        static CellRange TryGetRange(DefinedName dn)
        {
            try
            {
                CellRange range = dn.Range;
                if (range == null) return null;
                int touch = range.TopRowIndex + range.LeftColumnIndex;
                return range;
            }
            catch
            {
                return null;
            }
        }

        // ===================================================================
        // Pemeriksaan template (dipakai saat upload template di Document Category)
        // ===================================================================

        public class Report
        {
            public bool HasMarkers { get; set; }
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Fields { get; } = new List<string>();      // mis. "DOCUMENT_CODE (COVER!AC8)"
            public List<string> Boxes { get; } = new List<string>();       // mis. "DIBUAT: tanda tangan, nama, jabatan (COVER)"
            public bool IsValid => Errors.Count == 0;
        }

        public static Report Analyze(Workbook workbook)
        {
            var report = new Report { HasMarkers = HasMarkers(workbook) };
            if (!report.HasMarkers) return report;

            // Penanda rusak & penanda ganda (nama sama di dua sheet berbeda tidak masalah,
            // tapi dicatat supaya pemilik template sadar).
            var allNames = workbook.DefinedNames.Cast<DefinedName>()
                .Concat(workbook.Worksheets.SelectMany(ws => ws.DefinedNames.Cast<DefinedName>()))
                .Where(dn => IsDcsName(dn.Name))
                .ToList();

            foreach (DefinedName dn in allNames.Where(dn => TryGetRange(dn) == null))
            {
                report.Errors.Add($"Penanda {dn.Name} rusak (sel tujuannya sudah dihapus). Hapus lalu buat ulang penanda ini di Excel.");
            }

            var fieldsSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var boxParts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var boxSheets = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (Worksheet sheet in workbook.Worksheets)
            {
                foreach (KeyValuePair<string, CellRange> marker in Collect(workbook, sheet))
                {
                    string key = marker.Key.ToUpperInvariant();
                    string where = $"{sheet.Name}!{marker.Value.GetReferenceA1()}";

                    if (key.StartsWith("F_"))
                    {
                        string property = key.Substring(2);
                        if (FindProperty(property) == null)
                        {
                            report.Errors.Add($"Penanda DCS_{key} tidak dikenal: field \"{property}\" tidak ada di data dokumen. Periksa ejaan nama penanda.");
                            continue;
                        }
                        fieldsSeen.Add(property);
                        report.Fields.Add($"{property} ({where})");
                        continue;
                    }

                    string part = BoxPartPrefixes.FirstOrDefault(p => key.StartsWith(p));
                    if (part == null)
                    {
                        report.Errors.Add($"Penanda DCS_{key} tidak dikenal. Gunakan DCS_F_..., DCS_SIGN_..., DCS_NAME_... atau DCS_TITLE_...");
                        continue;
                    }

                    string boxKey = key.Substring(part.Length);
                    if (!BoxKeyPattern.IsMatch(boxKey))
                    {
                        report.Errors.Add($"Penanda DCS_{key}: kotak \"{boxKey}\" tidak dikenal. Gunakan DIBUAT, DIPERIKSA, DISETUJUI, STEP1..n, LAST, atau POS<id jabatan>.");
                        continue;
                    }

                    if (!boxParts.ContainsKey(boxKey))
                    {
                        boxParts[boxKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        boxSheets[boxKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    boxParts[boxKey].Add(part.TrimEnd('_'));
                    boxSheets[boxKey].Add(sheet.Name);
                }
            }

            foreach (KeyValuePair<string, HashSet<string>> box in boxParts.OrderBy(b => BoxSortOrder(b.Key)))
            {
                var parts = new List<string>();
                if (box.Value.Contains("SIGN")) parts.Add("tanda tangan");
                if (box.Value.Contains("NAME")) parts.Add("nama");
                if (box.Value.Contains("TITLE")) parts.Add("jabatan");
                report.Boxes.Add($"{box.Key}: {string.Join(", ", parts)} ({string.Join(", ", boxSheets[box.Key])})");

                if (!box.Value.Contains("SIGN"))
                {
                    report.Warnings.Add($"Kotak {box.Key} punya penanda nama/jabatan tetapi tidak punya DCS_SIGN_{box.Key}, jadi gambar tanda tangan tidak akan tercetak di kotak ini.");
                }
            }

            if (!boxParts.Values.Any(p => p.Contains("SIGN")))
            {
                report.Errors.Add("Template belum punya kotak tanda tangan sama sekali (minimal satu penanda DCS_SIGN_..., mis. DCS_SIGN_DIBUAT).");
            }
            else if (!boxParts.ContainsKey("DIBUAT") && !boxParts.Keys.Any(k => k.StartsWith("POS", StringComparison.OrdinalIgnoreCase)))
            {
                report.Warnings.Add("Tidak ada kotak DIBUAT (pembuat dokumen). Tanda tangan pembuat tidak akan tercetak.");
            }

            foreach (string field in RecommendedFields.Where(f => !fieldsSeen.Contains(f)))
            {
                report.Warnings.Add($"Field {field} belum diberi penanda (DCS_F_{field}), jadi tidak akan terisi otomatis.");
            }

            return report;
        }

        static int BoxSortOrder(string key)
        {
            string k = key.ToUpperInvariant();
            if (k == "DIBUAT") return 0;
            if (k == "DIPERIKSA") return 1;
            if (k == "DISETUJUI") return 2;
            if (k.StartsWith("STEP")) return 10;
            if (k.StartsWith("POS")) return 20;
            return 30; // LAST
        }

        static PropertyInfo FindProperty(string name) =>
            typeof(DocumentMaintenance).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        // ===================================================================
        // Penulisan
        // ===================================================================

        /// <summary>Isi kotak tanda tangan: siapa orangnya, dan apakah gambar tanda tangannya ditampilkan.</summary>
        public class BoxFill
        {
            public string FullName { get; set; }
            public string PositionName { get; set; }
            public string SignatureFullPath { get; set; }   // path lengkap file gambar, null = tanpa gambar
        }

        /// <summary>
        /// Tulis semua penanda di satu sheet. resolveBox dipanggil per kotak (key tanpa
        /// prefix SIGN_/NAME_/TITLE_, huruf besar); null = kotak dibiarkan kosong.
        /// </summary>
        public static void Write(Workbook workbook, Worksheet sheet, DocumentMaintenance document, Func<string, BoxFill> resolveBox)
        {
            Dictionary<string, CellRange> markers = Collect(workbook, sheet);
            if (markers.Count == 0) return;

            foreach (KeyValuePair<string, CellRange> marker in markers.Where(m => m.Key.StartsWith("F_", StringComparison.OrdinalIgnoreCase)))
            {
                PropertyInfo property = FindProperty(marker.Key.Substring(2));
                if (property == null) continue;

                Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                object value = property.GetValue(document);
                Cell cell = ResolveMergeAnchor(sheet[marker.Value.TopRowIndex, marker.Value.LeftColumnIndex]);

                if (type == typeof(DateTime))
                {
                    if (value is DateTime date)
                    {
                        // Tanggal ditulis sebagai nilai tanggal sungguhan dan MENGIKUTI format
                        // tanggal yang dipilih pemilik template di sel itu (mis. dd-mmm-yy);
                        // sel berformat General diberi dd-mmm-yy. Kalau formatnya terlalu lebar
                        // untuk selnya, Excel menampilkan ##### - kelihatan lewat Preview.
                        cell.Value = date;
                        if (!IsDateNumberFormat(cell.NumberFormat)) cell.NumberFormat = "dd-mmm-yy";
                        cell.Font.Color = Color.Black;
                    }
                    else
                    {
                        SetValueBlackFont(cell, "-");
                    }
                    continue;
                }

                if (value == null) continue;
                SetValueBlackFont(cell, value.ToString());
            }

            var boxKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string key in markers.Keys)
            {
                string part = BoxPartPrefixes.FirstOrDefault(p => key.StartsWith(p, StringComparison.OrdinalIgnoreCase));
                if (part != null && key.Length > part.Length) boxKeys.Add(key.Substring(part.Length).ToUpperInvariant());
            }

            foreach (string boxKey in boxKeys)
            {
                BoxFill fill = resolveBox(boxKey);
                if (fill == null) continue;

                if (markers.TryGetValue("NAME_" + boxKey, out CellRange nameRange))
                {
                    WriteNameCellSafely(sheet, nameRange.TopRowIndex, nameRange.LeftColumnIndex, nameRange.ColumnCount, fill.FullName);
                }

                if (markers.TryGetValue("TITLE_" + boxKey, out CellRange titleRange))
                {
                    SetValueBlackFontBottomAligned(ResolveMergeAnchor(sheet[titleRange.TopRowIndex, titleRange.LeftColumnIndex]), fill.PositionName);
                }

                if (!string.IsNullOrEmpty(fill.SignatureFullPath)
                    && File.Exists(fill.SignatureFullPath)
                    && markers.TryGetValue("SIGN_" + boxKey, out CellRange signRange))
                {
                    AddPicture(sheet, fill.SignatureFullPath, signRange);
                }
            }
        }

        // Format angka Excel yang berisi token tanggal (d, y, atau mmm) di luar bagian
        // [..] (locale/warna) dan "teks literal".
        static bool IsDateNumberFormat(string numberFormat)
        {
            if (string.IsNullOrEmpty(numberFormat)) return false;
            string stripped = Regex.Replace(numberFormat.ToLowerInvariant(), "\\[[^\\]]*\\]|\"[^\"]*\"", "");
            return Regex.IsMatch(stripped, "[dy]|mmm");
        }

        /// <summary>Kotak POS&lt;id&gt; -> POSITION_ID, selain itu null.</summary>
        public static int? PositionIdOfBox(string boxKey) =>
            boxKey != null && boxKey.StartsWith("POS", StringComparison.OrdinalIgnoreCase) && int.TryParse(boxKey.Substring(3), out int id)
                ? id
                : (int?)null;

        // ===================================================================
        // Preview template dengan data contoh
        // ===================================================================

        /// <summary>
        /// Isi template dengan data & tanda tangan contoh lalu export ke PDF - dipakai
        /// tombol Preview di Document Category supaya admin bisa melihat hasil pengesahan
        /// sebelum template dipakai user.
        /// </summary>
        public static void RenderPreviewPdf(Workbook workbook, string outputPdfPath, Func<int, string> positionName)
        {
            var sample = new DocumentMaintenance
            {
                DOCUMENT_CODE = "HMMI/CONTOH/001",
                DOCUMENT_TRANSACTION_NAME = "Contoh Judul Dokumen",
                REVISION = 1,
                DOCUMENT_DATE = DateTime.Today,
                DOCUMENT_REVISION_0_DATE = DateTime.Today.AddYears(-1),
                DEPARTMENT_CODE = "DEPT",
                SECTION_NAME = "Contoh Section",
            };

            string signaturePath = Path.Combine(Path.GetTempPath(), "dcs_sample_signature.png");
            CreateSampleSignature(signaturePath);

            BoxFill Resolve(string boxKey)
            {
                int? posId = PositionIdOfBox(boxKey);
                // Teks contoh sengaja pendek supaya muat di kotak sempit - nama asli
                // yang panjang tetap perlu dicek manual di template.
                string name = boxKey switch
                {
                    "DIBUAT" => "Nama Pembuat",
                    "DIPERIKSA" => "Nama Approver 1",
                    "DISETUJUI" => "Nama Approver 2",
                    "LAST" => "Nama Approver Akhir",
                    _ when boxKey.StartsWith("STEP") => "Nama Approver " + boxKey.Substring(4),
                    _ => "Nama Pejabat",
                };
                return new BoxFill
                {
                    FullName = name,
                    PositionName = posId != null ? (positionName(posId.Value) ?? "Jabatan") : "Jabatan",
                    SignatureFullPath = signaturePath,
                };
            }

            foreach (Worksheet sheet in workbook.Worksheets)
            {
                sheet.PrintOptions.FitToWidth = 1;
                sheet.PrintOptions.FitToHeight = 1;
                sheet.PrintOptions.FitToPage = true;
                Write(workbook, sheet, sample, Resolve);
            }

            workbook.Calculate();
            workbook.ExportToPdf(outputPdfPath);
        }

        static void CreateSampleSignature(string path)
        {
            using (var bmp = new Bitmap(360, 120))
            using (var g = Graphics.FromImage(bmp))
            using (var pen = new Pen(Color.FromArgb(30, 60, 160), 3))
            using (var font = new Font("Segoe Script", 26, FontStyle.Italic))
            using (var brush = new SolidBrush(Color.FromArgb(30, 60, 160)))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.DrawString("Contoh TTD", font, brush, 10, 25);
                g.DrawBezier(pen, 15, 95, 120, 70, 220, 115, 340, 80);
                bmp.Save(path, ImageFormat.Png);
            }
        }

        // ===================================================================
        // Helper sel (dipakai juga oleh jalur ROW/COL lama di DocumentMaintenanceController)
        // ===================================================================

        public static void SetValueBlackFont(Cell cell, string value)
        {
            cell.SetValueFromText(value);
            cell.Font.Color = Color.Black;
        }

        // Kalau sel target ternyata bagian NON-ANCHOR dari merged region, tulis ke anchor
        // (kiri-atas) merge itu - menulis ke sel non-anchor tidak dirender.
        public static Cell ResolveMergeAnchor(Cell cell)
        {
            IList<CellRange> merges = cell.GetMergedRanges();
            if (merges.Count > 0)
            {
                CellRange anchor = merges[0];
                return cell.Worksheet[anchor.TopRowIndex, anchor.LeftColumnIndex];
            }
            return cell;
        }

        // Baris nama & jabatan dipaksa konsisten (center, bawah, non-bold) karena sering
        // menimpa teks contoh lama di template yang formatnya beda-beda.
        public static void SetValueBlackFontBottomAligned(Cell cell, string value)
        {
            SetValueBlackFont(cell, value);
            cell.Font.Bold = false;
            cell.Alignment.Horizontal = SpreadsheetHorizontalAlignment.Center;
            cell.Alignment.Vertical = SpreadsheetVerticalAlignment.Bottom;
        }

        /// <summary>
        /// Tulis nama ke sel - kalau sel itu bagian dari merged region (mis. area gambar
        /// tanda tangan + baris nama digabung jadi satu kotak), value dialihkan ke anchor
        /// merge itu. Kalau belum ada merge, buat baru selebar mergeCellCol.
        /// </summary>
        public static void WriteNameCellSafely(Worksheet sheet, int row, int col, int mergeCellCol, string value)
        {
            IList<CellRange> existingMerges = sheet[row, col].GetMergedRanges();
            if (existingMerges.Count > 0)
            {
                CellRange existing = existingMerges[0];
                SetValueBlackFontBottomAligned(sheet[existing.TopRowIndex, existing.LeftColumnIndex], value);
            }
            else
            {
                SetValueBlackFontBottomAligned(sheet[row, col], value);
                sheet.MergeCells(sheet.Range.FromLTRB(col, row, col + mergeCellCol - 1, row));
            }
        }

        public static void AddPicture(Worksheet sheet, string imagePath, CellRange targetRange)
        {
            sheet.Pictures.AddPicture(SpreadsheetImageSource.FromFile(imagePath), targetRange, false);
        }
    }
}
