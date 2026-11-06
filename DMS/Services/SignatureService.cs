using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DMS.Services
{
    /// <summary>
    /// Tanda tangan elektronik ala Folderit (request Hendra 2026-11-06): setiap kali
    /// menandatangani, user menggambar (DRAW) atau mengetik (TYPE) tanda tangannya.
    /// Yang disimpan di TB_R_SIGNATURE adalah goresan / teks + font - BUKAN file gambar -
    /// dan terikat ke satu dokumen + langkah; gambarnya baru dibentuk saat lembar
    /// pengesahan dirender. Lihat SQL/ElectronicSignature_Migration.sql.
    /// </summary>
    public static class SignatureService
    {
        public const string KindCreator = "CREATOR";
        public const string KindApproval = "APPROVAL";
        public const string KindAcknowledge = "ACK";

        public const string MethodDraw = "DRAW";
        public const string MethodType = "TYPE";

        /// <summary>Font tulisan tangan bawaan Windows (tidak butuh Office di server).</summary>
        public static readonly string[] TypeFonts = { "Segoe Script", "Segoe Print", "Ink Free", "Gabriola" };

        const int MaxDrawJsonLength = 200_000;
        const int MaxStrokes = 300;
        const int MaxPoints = 8000;

        /// <summary>Tanda tangan dari form (dialog tanda tangan di browser).</summary>
        public class Input
        {
            public string Method { get; set; }
            public string Data { get; set; }
            public string Font { get; set; }
        }

        public class Record
        {
            public int SignatureId { get; set; }
            public string Signer { get; set; }
            public string SignerName { get; set; }
            public string Method { get; set; }
            public string Data { get; set; }
            public string Font { get; set; }
            public DateTime SignedDt { get; set; }
        }

        class DrawData
        {
            public float w { get; set; }
            public float h { get; set; }
            public List<List<float[]>> strokes { get; set; }
        }

        /// <summary>null = valid; selain itu pesan kesalahan untuk user.</summary>
        public static string Validate(Input input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Method) || string.IsNullOrWhiteSpace(input.Data))
                return "Please sign the document first (draw or type your signature).";

            if (input.Method == MethodType)
            {
                string text = input.Data.Trim();
                if (text.Length < 2 || text.Length > 60) return "Typed signature must be 2-60 characters.";
                if (!TypeFonts.Contains(input.Font)) return "Unknown signature font.";
                return null;
            }

            if (input.Method == MethodDraw)
            {
                if (input.Data.Length > MaxDrawJsonLength) return "Signature drawing is too large.";
                DrawData draw = ParseDraw(input.Data);
                if (draw == null) return "Signature drawing could not be read. Please draw it again.";
                int points = draw.strokes.Sum(s => s.Count);
                if (draw.strokes.Count > MaxStrokes || points > MaxPoints) return "Signature drawing is too large.";
                if (points < 10) return "Signature is too short. Please draw your signature.";
                return null;
            }

            return "Unknown signature method.";
        }

        static DrawData ParseDraw(string json)
        {
            try
            {
                DrawData draw = JsonSerializer.Deserialize<DrawData>(json);
                if (draw?.strokes == null || draw.w <= 0 || draw.h <= 0) return null;
                if (draw.strokes.Any(s => s == null || s.Any(p => p == null || p.Length < 2))) return null;
                return draw;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static DbCommand Command(DBContext db, string procedure, out bool wasClosed)
        {
            var connection = db.Database.GetDbConnection();
            wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = procedure;
            command.CommandType = CommandType.StoredProcedure;
            var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            if (transaction != null) command.Transaction = transaction;
            return command;
        }

        /// <summary>Simpan tanda tangan (dipanggil di dalam transaksi Save/Approve/Mengetahui).</summary>
        public static (bool ok, string message) Save(DBContext db, string kind, int documentTransactionId, string refKey,
            string signer, Input input, string clientIp, string userAgent, string documentFileHash)
        {
            using DbCommand command = Command(db, "[dbo].[sp_Signature_Save]", out bool wasClosed);
            try
            {
                command.Parameters.Add(new SqlParameter("@KIND", kind));
                command.Parameters.Add(new SqlParameter("@DOCUMENT_TRANSACTION_ID", documentTransactionId));
                command.Parameters.Add(new SqlParameter("@REF_KEY", refKey ?? ""));
                command.Parameters.Add(new SqlParameter("@SIGNER", signer));
                command.Parameters.Add(new SqlParameter("@SIGN_METHOD", input.Method));
                command.Parameters.Add(new SqlParameter("@SIGN_DATA", input.Method == MethodType ? input.Data.Trim() : input.Data));
                command.Parameters.Add(new SqlParameter("@SIGN_FONT", (object)(input.Method == MethodType ? input.Font : null) ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CLIENT_IP", (object)clientIp ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@USER_AGENT", (object)userAgent ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@DOCUMENT_FILE_HASH", (object)documentFileHash ?? DBNull.Value));
                var message = new SqlParameter("@RETURN_MSG", SqlDbType.VarChar, -1) { Direction = ParameterDirection.Output };
                var ret = new SqlParameter("@RETURN_VAL", SqlDbType.Int) { Direction = ParameterDirection.ReturnValue };
                command.Parameters.Add(message);
                command.Parameters.Add(ret);
                command.ExecuteNonQuery();
                return (Convert.ToInt32(ret.Value) == 1, Convert.ToString(message.Value));
            }
            finally
            {
                if (wasClosed) command.Connection.Close();
            }
        }

        public static Record Load(DBContext db, string kind, int documentTransactionId, string refKey)
        {
            using DbCommand command = Command(db, "[dbo].[sp_Signature_Get]", out bool wasClosed);
            try
            {
                command.Parameters.Add(new SqlParameter("@KIND", kind));
                command.Parameters.Add(new SqlParameter("@DOCUMENT_TRANSACTION_ID", documentTransactionId));
                command.Parameters.Add(new SqlParameter("@REF_KEY", refKey ?? ""));
                using var r = command.ExecuteReader();
                if (!r.Read()) return null;
                return new Record
                {
                    SignatureId = Convert.ToInt32(r["SIGNATURE_ID"]),
                    Signer = Convert.ToString(r["SIGNER"]),
                    SignerName = r["SIGNER_NAME"] == DBNull.Value ? null : Convert.ToString(r["SIGNER_NAME"]),
                    Method = Convert.ToString(r["SIGN_METHOD"]),
                    Data = Convert.ToString(r["SIGN_DATA"]),
                    Font = r["SIGN_FONT"] == DBNull.Value ? null : Convert.ToString(r["SIGN_FONT"]),
                    SignedDt = Convert.ToDateTime(r["SIGNED_DT"]),
                };
            }
            finally
            {
                if (wasClosed) command.Connection.Close();
            }
        }

        /// <summary>SHA-256 file dokumen yang ditandatangani (jejak audit), null kalau file tidak ada.</summary>
        public static string FileHash(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) return null;
            using var stream = File.OpenRead(fullPath);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(stream));
        }

        // ------------------------------------------------------------------
        // Render ke PNG (sementara, hanya untuk ditempel ke lembar pengesahan)
        // ------------------------------------------------------------------

        const int RenderWidth = 900, RenderHeight = 300;   // rasio 3:1, sama dengan kanvas di browser
        static readonly Color InkColor = Color.FromArgb(20, 40, 120);

        /// <summary>
        /// PNG tanda tangan di folder temp aplikasi (bukan wwwroot - tidak bisa diunduh),
        /// di-cache per isi tanda tangan. Mengembalikan path lengkap.
        /// </summary>
        public static string RenderPng(string method, string data, string font)
        {
            string folder = Path.Combine(Path.GetTempPath(), "DMS_Sign");
            Directory.CreateDirectory(folder);
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(method + "|" + font + "|" + data)))[..20];
            string path = Path.Combine(folder, key + ".png");
            if (File.Exists(path)) return path;

            using (var bmp = new Bitmap(RenderWidth, RenderHeight))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                if (method == MethodType) DrawTyped(g, data, font);
                else DrawStrokes(g, ParseDraw(data));

                string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                bmp.Save(tmp, ImageFormat.Png);
                try { File.Move(tmp, path, true); } catch (IOException) { if (File.Exists(tmp)) File.Delete(tmp); }
            }
            return path;
        }

        static void DrawStrokes(Graphics g, DrawData draw)
        {
            if (draw == null) return;
            var all = draw.strokes.SelectMany(s => s).ToList();
            if (all.Count == 0) return;

            // Potong ke area goresan (bukan seluruh kanvas) lalu skala ke gambar dengan
            // margin, supaya tanda tangan kecil tetap mengisi kotak.
            float minX = all.Min(p => p[0]), maxX = all.Max(p => p[0]);
            float minY = all.Min(p => p[1]), maxY = all.Max(p => p[1]);
            float bw = Math.Max(maxX - minX, 1), bh = Math.Max(maxY - minY, 1);
            const float margin = 18;
            float scale = Math.Min((RenderWidth - 2 * margin) / bw, (RenderHeight - 2 * margin) / bh);
            float offX = (RenderWidth - bw * scale) / 2 - minX * scale;
            float offY = (RenderHeight - bh * scale) / 2 - minY * scale;
            float penWidth = Math.Clamp(3.2f * scale * draw.w / 600f, 2.5f, 7f);

            using var pen = new Pen(InkColor, penWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            using var brush = new SolidBrush(InkColor);
            foreach (var stroke in draw.strokes)
            {
                var pts = stroke.Select(p => new PointF(p[0] * scale + offX, p[1] * scale + offY)).ToArray();
                if (pts.Length == 1) g.FillEllipse(brush, pts[0].X - penWidth / 2, pts[0].Y - penWidth / 2, penWidth, penWidth);
                else if (pts.Length == 2) g.DrawLine(pen, pts[0], pts[1]);
                else g.DrawCurve(pen, pts, 0.5f);
            }
        }

        static void DrawTyped(Graphics g, string text, string font)
        {
            string family = TypeFonts.Contains(font) && IsInstalled(font) ? font : TypeFonts.FirstOrDefault(IsInstalled) ?? "Segoe Script";
            float size = 120;
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            using var brush = new SolidBrush(InkColor);
            // Kecilkan font sampai teksnya muat di gambar
            while (size > 20)
            {
                using var f = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
                SizeF measured = g.MeasureString(text, f);
                if (measured.Width <= RenderWidth - 40 && measured.Height <= RenderHeight - 20) break;
                size -= 6;
            }
            using var finalFont = new Font(family, size, FontStyle.Regular, GraphicsUnit.Pixel);
            g.DrawString(text, finalFont, brush, new RectangleF(0, 0, RenderWidth, RenderHeight), format);
        }

        static readonly HashSet<string> installedFonts =
            new HashSet<string>(new InstalledFontCollection().Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

        static bool IsInstalled(string font) => font != null && installedFonts.Contains(font);
    }
}
