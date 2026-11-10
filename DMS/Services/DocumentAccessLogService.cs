using DMS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DMS.Services
{
    /// <summary>
    /// Audit trail akses dokumen (request Hendra 2026-11-06): siapa membuka (VIEW),
    /// mengunduh (DOWNLOAD), atau mencetak (PRINT) dokumen apa, kapan, dari halaman
    /// mana. Lihat SQL/DocumentAccessLog_Migration.sql.
    ///
    /// Ditulis lewat koneksi SQL tersendiri (bukan transaksi EF yang sedang berjalan)
    /// supaya log tetap tercatat walau aksi pemanggilnya di-rollback, dan semua error
    /// ditelan - pencatatan log tidak boleh menggagalkan user membuka dokumen.
    /// </summary>
    public static class DocumentAccessLogService
    {
        public const string View = "VIEW";
        public const string Download = "DOWNLOAD";
        public const string Print = "PRINT";

        public static void Log(DBContext db, HttpContext http, string action,
            int? documentTransactionId = null, string filePath = null, int? externalDocumentId = null, int? copies = null)
        {
            try
            {
                // Username dari session; claim Name untuk request ber-token JWT.
                string username = http?.Session?.GetString("USERNAME") ?? http?.User?.Identity?.Name;
                if (string.IsNullOrEmpty(username)) return;
                if (documentTransactionId == null && externalDocumentId == null && string.IsNullOrWhiteSpace(filePath)) return;

                string sourcePage = null;
                if (Uri.TryCreate(http.Request.Headers["Referer"].ToString(), UriKind.Absolute, out Uri referer))
                    sourcePage = referer.AbsolutePath;

                using var connection = new SqlConnection(db.Database.GetConnectionString());
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "[dbo].[sp_DocumentAccessLog_Insert]";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@USERNAME", username));
                command.Parameters.Add(new SqlParameter("@ACTION", action));
                command.Parameters.Add(new SqlParameter("@DOCUMENT_TRANSACTION_ID", (object)documentTransactionId ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@EXTERNAL_DOCUMENT_ID", (object)externalDocumentId ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@FILE_PATH", (object)filePath ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@COPIES", (object)copies ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@SOURCE_PAGE", (object)sourcePage ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@CLIENT_IP", (object)http.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@USER_AGENT", (object)http.Request.Headers["User-Agent"].ToString() ?? DBNull.Value));
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Document access log failed: " + ex.Message);
            }
        }
    }
}
