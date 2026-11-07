using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace DMS.Services
{
    /// <summary>
    /// OCR / full-text tahap 2 (request Hendra 2026-11-06), memakai indeks teks
    /// TB_R_DOCUMENT_CONTENT dan ekstraktor DocumentContentIndexer:
    ///  1. Analisis dampak revisi - dokumen lain yang menyebut nomor dokumen ini.
    ///  2. Cek file upload - file identik dengan revisi sebelumnya, memuat nomor
    ///     dokumen lain (salah file), atau tidak memuat judul dokumen.
    /// </summary>
    public static class DocumentInsightService
    {
        public class Reference
        {
            public string SourceType { get; set; }
            public int SourceId { get; set; }
            public string DocumentCode { get; set; }
            public string Title { get; set; }
            public string Revision { get; set; }
            public string StatusName { get; set; }
            public string DepartmentName { get; set; }
            public string FilePath { get; set; }
            public bool ByOcr { get; set; }
            public bool InTitle { get; set; }
            public string Snippet { get; set; }
        }

        public class FileCheck
        {
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Notes { get; } = new List<string>();
            public int TextLength { get; set; }
        }

        public static List<Reference> GetReferences(DBContext db, string documentCode)
        {
            var list = new List<Reference>();
            if (string.IsNullOrWhiteSpace(documentCode)) return list;

            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = new SqlCommand("[dbo].[sp_DocumentReferences]", connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 60 };
            command.Parameters.AddWithValue("@DOCUMENT_CODE", documentCode.Trim());
            using var r = command.ExecuteReader();
            string S(string c) => r[c] == DBNull.Value ? null : Convert.ToString(r[c]);
            while (r.Read())
            {
                string method = S("EXTRACT_METHOD");
                list.Add(new Reference
                {
                    SourceType = S("SOURCE_TYPE"),
                    SourceId = Convert.ToInt32(r["SOURCE_ID"]),
                    DocumentCode = S("DOCUMENT_CODE"),
                    Title = S("TITLE"),
                    Revision = S("REVISION"),
                    StatusName = S("STATUS_NAME"),
                    DepartmentName = S("DEPARTMENT_NAME"),
                    FilePath = S("FILE_PATH"),
                    ByOcr = method != null && method.Contains("OCR") && method != "TEXT/NO-OCR",
                    InTitle = Convert.ToBoolean(r["IN_TITLE"]),
                    Snippet = S("SNIPPET") == null ? null : Regex.Replace(S("SNIPPET"), @"\s+", " ").Trim(),
                });
            }
            return list;
        }

        /// <summary>
        /// Periksa file yang akan di-upload. Peringatan tidak memblokir penyimpanan -
        /// hanya membantu user menyadari kemungkinan salah file.
        /// </summary>
        public static FileCheck CheckUpload(DBContext db, string webRootPath, IFormFile file, string revisedDocumentCode, string title)
        {
            var result = new FileCheck();
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string tmp = Path.Combine(Path.GetTempPath(), "dms_check_" + Guid.NewGuid().ToString("N") + extension);
            try
            {
                using (var stream = new FileStream(tmp, FileMode.CreateNew)) file.CopyTo(stream);

                string uploadedHash;
                using (var stream = File.OpenRead(tmp)) using (var sha = SHA256.Create())
                    uploadedHash = Convert.ToHexString(sha.ComputeHash(stream));

                // OCR tidak dipakai di sini (user menunggu); file dokumen internal berupa Excel
                string text = DocumentContentIndexer.Extract(tmp, () => null).Text ?? "";
                result.TextLength = text.Length;

                (string previousPath, string previousRevision, List<string> codes) = LoadCheckData(db, revisedDocumentCode);
                string code = revisedDocumentCode?.Trim();
                bool isRevision = !string.IsNullOrEmpty(code);

                // 1. File identik dengan revisi sebelumnya
                if (isRevision && previousPath != null)
                {
                    string previousFull = UploadPath.Resolve(webRootPath, previousPath);
                    if (previousFull != null && File.Exists(previousFull))
                    {
                        using var stream = File.OpenRead(previousFull);
                        using var sha = SHA256.Create();
                        if (Convert.ToHexString(sha.ComputeHash(stream)) == uploadedHash)
                            result.Warnings.Add($"This file is identical to the current revision (Rev. {previousRevision}) of {code}. Did you upload the updated file?");
                    }
                }

                if (text.Length == 0)
                {
                    result.Notes.Add("No text could be read from this file, so its content could not be checked.");
                    return result;
                }

                // 2. Nomor dokumen lain di dalam file
                var otherCodes = codes
                    .Where(c => !string.Equals(c, code, StringComparison.OrdinalIgnoreCase))
                    .Where(c => text.Contains(c, StringComparison.OrdinalIgnoreCase))
                    .Take(5).ToList();
                bool mentionsOwnCode = isRevision && text.Contains(code, StringComparison.OrdinalIgnoreCase);
                if (otherCodes.Count > 0)
                {
                    string list = string.Join(", ", otherCodes);
                    if (isRevision && !mentionsOwnCode)
                        result.Warnings.Add($"This file mentions document number {list} but not {code}. Make sure you selected the right file.");
                    else if (!isRevision)
                        result.Notes.Add($"This file mentions existing document number {list}. If it was copied from that document, update its number and content before saving.");
                    else
                        result.Notes.Add($"This file also refers to {list}.");
                }

                // 3. Judul dokumen
                string normalizedTitle = Regex.Replace(title ?? "", @"\s+", " ").Trim();
                if (normalizedTitle.Length >= 8)
                {
                    string normalizedText = Regex.Replace(text, @"\s+", " ");
                    if (!normalizedText.Contains(normalizedTitle, StringComparison.OrdinalIgnoreCase))
                        result.Notes.Add($"The document title \"{normalizedTitle}\" was not found in the file. Check that the title in the form matches the file.");
                }
                return result;
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch (IOException) { }
            }
        }

        static (string path, string revision, List<string> codes) LoadCheckData(DBContext db, string revisedDocumentCode)
        {
            string path = null, revision = null;
            var codes = new List<string>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = new SqlCommand("[dbo].[sp_DocumentFileCheck_Data]", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@REVISED_DOCUMENT_CODE", (object)revisedDocumentCode ?? DBNull.Value);
            using var r = command.ExecuteReader();
            if (r.Read())
            {
                path = Convert.ToString(r["FILE_PATH"]);
                revision = r["REVISION"] == DBNull.Value ? "?" : Convert.ToString(r["REVISION"]);
            }
            if (r.NextResult())
                while (r.Read()) codes.Add(Convert.ToString(r["DOCUMENT_CODE"]));
            return (path, revision, codes);
        }
    }
}
