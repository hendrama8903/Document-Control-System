using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace DMS.Services
{
    /// <summary>
    /// Ekstraksi teks isi dokumen untuk full-text search (request Hendra 2026-11-06).
    /// Dijalankan Hangfire tiap 5 menit ("document-content-index", lihat Startup):
    /// mengindeks dokumen yang belum terindeks atau file-nya berganti, menyimpan
    /// teksnya ke TB_R_DOCUMENT_CONTENT. Lihat SQL/DocumentFullTextSearch_Migration.sql.
    /// </summary>
    public class DocumentContentIndexer
    {
        const int MaxCharacters = 2_000_000;   // batas teks per dokumen
        const int BatchSize = 100;
        const int MaxOcrPagesPerDocument = 50;  // batas beban CPU per dokumen scan
        const int MinTextPerPage = 20;          // halaman PDF dengan teks < ini dianggap hasil scan
        const int OcrRenderSize = 2200;         // sisi terpanjang gambar halaman (px), ~260 dpi untuk A4

        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp" };

        /// <summary>Hasil ekstraksi: teks + metode (TEXT / OCR / TEXT+OCR / TEXT/NO-OCR).</summary>
        public class Extracted
        {
            public string Text { get; set; }
            public string Method { get; set; }
            public int OcrPages { get; set; }
            public string Note { get; set; }
        }

        private readonly DBContext db;
        private readonly IWebHostEnvironment environment;

        public DocumentContentIndexer(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            this.environment = environment;
        }

        [Hangfire.DisableConcurrentExecution(timeoutInSeconds: 600)]
        [Hangfire.AutomaticRetry(Attempts = 0)]
        public void IndexPending()
        {
            List<(string type, int id, string path)> pending = LoadPending();
            if (pending.Count == 0) return;

            // Mesin OCR dibuat sekali per putaran, dan hanya kalau ada halaman scan / gambar
            OcrService ocr = null;
            string ocrError = null;
            bool ocrTried = false;
            OcrService GetOcr()
            {
                if (!ocrTried) { ocrTried = true; ocr = OcrService.TryCreate(out ocrError); }
                return ocr;
            }

            try
            {
                foreach (var (type, id, path) in pending)
                {
                    string status, error = null, content = null, method = null;
                    int ocrPages = 0;
                    try
                    {
                        string fullPath = UploadPath.Resolve(environment.WebRootPath, path);
                        if (fullPath == null || !File.Exists(fullPath))
                        {
                            status = "MISSING";
                            error = "File not found";
                        }
                        else
                        {
                            Extracted extracted = Extract(fullPath, GetOcr);
                            content = extracted.Text;
                            method = extracted.Method;
                            ocrPages = extracted.OcrPages;
                            error = method == "TEXT/NO-OCR" ? ocrError : extracted.Note;
                            status = string.IsNullOrWhiteSpace(content) ? "EMPTY" : "OK";
                        }
                    }
                    catch (Exception ex)
                    {
                        status = "ERROR";
                        error = ex.GetType().Name + ": " + ex.Message;
                    }
                    Save(type, id, path, content, status, error, method, ocrPages > 0 ? ocrPages : null);
                }
            }
            finally
            {
                ocr?.Dispose();
            }
        }

        /// <summary>
        /// Teks dari Excel / PDF / Word / gambar. Halaman PDF tanpa teks (hasil scan) dan
        /// file gambar dibaca dengan OCR kalau tersedia (getOcr mengembalikan null kalau tidak).
        /// </summary>
        public static Extracted Extract(string fullPath, Func<OcrService> getOcr)
        {
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            if (extension == ".pdf") return ExtractPdfWithOcr(fullPath, getOcr);

            if (ImageExtensions.Contains(extension))
            {
                OcrService ocr = getOcr();
                if (ocr == null) return new Extracted { Method = "TEXT/NO-OCR" };
                return new Extracted { Text = Normalize(ocr.Read(File.ReadAllBytes(fullPath))), Method = "OCR", OcrPages = 1 };
            }

            return new Extracted { Text = ExtractText(fullPath), Method = "TEXT" };
        }

        static Extracted ExtractPdfWithOcr(string fullPath, Func<OcrService> getOcr)
        {
            var sb = new StringBuilder();
            int textPages = 0, ocrPages = 0, skippedScanPages = 0;
            bool ocrUnavailable = false;

            using var processor = new DevExpress.Pdf.PdfDocumentProcessor();
            processor.LoadDocument(fullPath);
            int pageCount = processor.Document.Pages.Count;

            for (int pageNumber = 1; pageNumber <= pageCount && sb.Length < MaxCharacters; pageNumber++)
            {
                string pageText = processor.GetPageText(pageNumber) ?? "";
                if (pageText.Count(c => !char.IsWhiteSpace(c)) >= MinTextPerPage)
                {
                    sb.Append(pageText).Append('\n');
                    textPages++;
                    continue;
                }

                // Halaman hasil scan (tidak punya lapisan teks)
                OcrService ocr = ocrUnavailable ? null : getOcr();
                if (ocr == null) { ocrUnavailable = true; skippedScanPages++; continue; }
                if (ocrPages >= MaxOcrPagesPerDocument) { skippedScanPages++; continue; }

                using (System.Drawing.Bitmap bitmap = processor.CreateBitmap(pageNumber, OcrRenderSize))
                using (var stream = new MemoryStream())
                {
                    bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    sb.Append(ocr.Read(stream.ToArray())).Append('\n');
                }
                ocrPages++;
            }

            string method = ocrPages > 0 ? (textPages > 0 ? "TEXT+OCR" : "OCR")
                : ocrUnavailable ? "TEXT/NO-OCR" : "TEXT";
            string note = skippedScanPages > 0 && !ocrUnavailable
                ? skippedScanPages + " scanned page(s) beyond the " + MaxOcrPagesPerDocument + "-page OCR limit were not read"
                : null;
            return new Extracted { Text = Normalize(sb.ToString()), Method = method, OcrPages = ocrPages, Note = note };
        }

        /// <summary>Teks polos dari file Excel / PDF / Word (null untuk jenis lain).</summary>
        public static string ExtractText(string fullPath)
        {
            string extension = Path.GetExtension(fullPath).ToLowerInvariant();
            string text = extension switch
            {
                ".xlsx" or ".xls" or ".xlsm" or ".csv" => ExtractSpreadsheet(fullPath),
                ".pdf" => ExtractPdf(fullPath),
                ".docx" or ".doc" or ".rtf" or ".txt" => ExtractWord(fullPath),
                _ => null,
            };
            return Normalize(text);
        }

        static string ExtractSpreadsheet(string fullPath)
        {
            var sb = new StringBuilder();
            using var workbook = new DevExpress.Spreadsheet.Workbook();
            workbook.LoadDocument(fullPath);
            foreach (DevExpress.Spreadsheet.Worksheet sheet in workbook.Worksheets)
            {
                DevExpress.Spreadsheet.CellRange used = sheet.GetUsedRange();
                if (used == null) continue;
                sb.Append(sheet.Name).Append('\n');
                for (int r = used.TopRowIndex; r <= used.BottomRowIndex && sb.Length < MaxCharacters; r++)
                {
                    bool any = false;
                    for (int c = used.LeftColumnIndex; c <= used.RightColumnIndex; c++)
                    {
                        DevExpress.Spreadsheet.Cell cell = sheet.Cells[r, c];
                        if (cell.Value.IsEmpty) continue;
                        string value = cell.DisplayText;
                        if (string.IsNullOrWhiteSpace(value)) continue;
                        sb.Append(value).Append(' ');
                        any = true;
                    }
                    if (any) sb.Append('\n');
                }
                // Teks di dalam shape / text box (instruksi kerja sering ditulis di shape)
                foreach (DevExpress.Spreadsheet.Shape shape in sheet.Shapes)
                {
                    try
                    {
                        if (shape.ShapeText != null && shape.ShapeText.HasText)
                            sb.Append(shape.ShapeText.Characters().Text).Append('\n');
                    }
                    catch (Exception) { /* shape tanpa teks */ }
                }
            }
            return sb.ToString();
        }

        static string ExtractPdf(string fullPath)
        {
            using var processor = new DevExpress.Pdf.PdfDocumentProcessor();
            processor.LoadDocument(fullPath);
            return processor.Text;
        }

        static string ExtractWord(string fullPath)
        {
            using var server = new DevExpress.XtraRichEdit.RichEditDocumentServer();
            server.LoadDocument(fullPath);
            return server.Text;
        }

        static readonly Regex Spaces = new Regex(@"[ \t ]+", RegexOptions.Compiled);
        static readonly Regex Lines = new Regex(@"\s*\n\s*", RegexOptions.Compiled);

        static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("\r", "\n");
            text = Spaces.Replace(text, " ");
            text = Lines.Replace(text, "\n").Trim();
            return text.Length > MaxCharacters ? text.Substring(0, MaxCharacters) : text;
        }

        List<(string, int, string)> LoadPending()
        {
            var list = new List<(string, int, string)>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = new SqlCommand("[dbo].[sp_DocumentContent_Pending]", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@MAX_ROWS", BatchSize);
            using var r = command.ExecuteReader();
            while (r.Read()) list.Add((r.GetString(0), r.GetInt32(1), r.GetString(2)));
            return list;
        }

        void Save(string type, int id, string path, string content, string status, string error, string method, int? ocrPages)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = new SqlCommand("[dbo].[sp_DocumentContent_Save]", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 120 };
            command.Parameters.AddWithValue("@SOURCE_TYPE", type);
            command.Parameters.AddWithValue("@SOURCE_ID", id);
            command.Parameters.AddWithValue("@FILE_PATH", path);
            command.Parameters.Add(new SqlParameter("@CONTENT", SqlDbType.NVarChar, -1) { Value = (object)content ?? DBNull.Value });
            command.Parameters.AddWithValue("@INDEX_STATUS", status);
            command.Parameters.AddWithValue("@ERROR_MSG", (object)error ?? DBNull.Value);
            command.Parameters.AddWithValue("@EXTRACT_METHOD", (object)method ?? DBNull.Value);
            command.Parameters.AddWithValue("@OCR_PAGES", (object)ocrPages ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
    }
}
