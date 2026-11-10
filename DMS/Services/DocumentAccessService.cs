using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using DMS.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DMS.Services
{
    /// <summary>
    /// Who may open an uploaded file (request Hendra 2026-11-10). Used by every endpoint that
    /// serves a document file and by the /Upload middleware, so the rule matches the document
    /// search. The rule itself lives in SQL: dbo.fn_Document_CanView / sp_DocumentFile_CanView
    /// (SQL/DocumentAccess_Migration.sql).
    /// </summary>
    public static class DocumentAccessService
    {
        public enum Access { Allowed, NotLoggedIn, Denied }

        public const string DeniedMessage = "You do not have access to this document.";

        // TEMP_<file name>.pdf, plus the stamped copies TEMP_<file name>_wm.pdf / _print_wm.pdf
        static readonly Regex TempPdf = new Regex(@"^TEMP_(?<name>.+?)(_print_wm|_wm)?\.pdf$", RegexOptions.IgnoreCase);

        /// <summary>Access to a file by its path relative to wwwroot, e.g. /Upload/ATTACHMENT/DOCUMENT_TRANSACTION/x.xlsx.</summary>
        public static Access CheckFile(HttpContext http, DBContext db, string relativePath)
        {
            string username = http.Session.GetString("USERNAME");
            if (string.IsNullOrEmpty(username)) return Access.NotLoggedIn;

            string path = "/" + (relativePath ?? "").Replace('\\', '/').TrimStart('/');
            string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // /document/... (templates, print forms): any logged-in user
            if (parts.Length > 0 && parts[0].Equals("document", StringComparison.OrdinalIgnoreCase)) return Access.Allowed;

            // /Upload/ATTACHMENT/<FOLDER>/<file>
            if (parts.Length < 4 || !parts[0].Equals("Upload", StringComparison.OrdinalIgnoreCase)
                || !parts[1].Equals("ATTACHMENT", StringComparison.OrdinalIgnoreCase))
                return IsDocumentControl(db, username) ? Access.Allowed : Access.Denied;

            string folder = parts[2].ToUpperInvariant();
            string fileName = parts[parts.Length - 1];

            switch (folder)
            {
                case "DOCUMENT_TEMP":
                    Match m = TempPdf.Match(fileName);
                    return m.Success && CanViewFile(db, username, null, m.Groups["name"].Value) ? Access.Allowed : Access.Denied;

                case "USER":
                    // Signature images: owner, or a role that may edit users. Other files
                    // (profile photos) are visible to every logged-in user.
                    if (fileName.StartsWith("Signature-", StringComparison.OrdinalIgnoreCase))
                        return IsOwnSignature(db, username, path) || HasFunction(http, "USER-EDIT") ? Access.Allowed : Access.Denied;
                    return Access.Allowed;

                case "DOCUMENT_MASTER":
                    return Access.Allowed;

                default:   // DOCUMENT_TRANSACTION, EXTERNAL_DOCUMENT, anything else
                    return CanViewFile(db, username, path, null) ? Access.Allowed : Access.Denied;
            }
        }

        /// <summary>
        /// Static access to /Upload: the view rule of <see cref="CheckFile"/>, plus the source rule
        /// for document files. The source file (usually Excel) carries no stamps and can be edited,
        /// so only the creator and Document Control get it directly; everyone else gets the
        /// stamped PDF through ViewAttachment / DownloadAttachment (ISO 9001 7.5.3, 2026-11-10).
        /// </summary>
        public static Access CheckStaticFile(HttpContext http, DBContext db, string relativePath)
        {
            Access access = CheckFile(http, db, relativePath);
            if (access != Access.Allowed) return access;

            string path = "/" + (relativePath ?? "").Replace('\\', '/').TrimStart('/');
            if (!path.StartsWith("/Upload/ATTACHMENT/DOCUMENT_TRANSACTION/", StringComparison.OrdinalIgnoreCase))
                return Access.Allowed;

            string username = http.Session.GetString("USERNAME");
            return CanDownloadSource(db, username, FindDocumentByFile(db, path)) ? Access.Allowed : Access.Denied;
        }

        /// <summary>The document (current or history revision) a file path belongs to, or null.</summary>
        public static int? FindDocumentByFile(DBContext db, string filePath) =>
            Scalar(db, @"SELECT TOP 1 ID FROM (
                    SELECT DOCUMENT_TRANSACTION_ID AS ID, 0 AS SRC FROM dbo.TB_R_DOCUMENT WHERE FILE_PATH = @P
                    UNION ALL
                    SELECT DOCUMENT_TRANSACTION_ID, 1 FROM dbo.TB_R_DOCUMENT_HISTORY WHERE FILE_PATH = @P
                ) X ORDER BY SRC", ("@P", filePath)) is int id ? id : null;

        /// <summary>
        /// May the user take the unstamped source file: Document Control / admin, or the creator
        /// of the document. A file that belongs to no document: Document Control only.
        /// </summary>
        public static bool CanDownloadSource(DBContext db, string username, int? documentTransactionId)
        {
            if (string.IsNullOrEmpty(username)) return false;
            if (IsDocumentControl(db, username)) return true;
            if (documentTransactionId == null) return false;
            return Scalar(db, @"SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @T AND (DOCUMENT_CREATOR = @U OR CREATED_BY = @U)
                    UNION ALL
                    SELECT 1 FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DOCUMENT_TRANSACTION_ID = @T AND (DOCUMENT_CREATOR = @U OR CREATED_BY = @U)
                ) THEN 1 ELSE 0 END AS BIT)", ("@U", username), ("@T", documentTransactionId)) is bool b && b;
        }

        /// <summary>
        /// QMS has received the registration of the document: the registration row in Document
        /// Registration is Accepted, Sent or Partially Sent (2/5/6). Self-request rows
        /// (OPERATION_TYPE 2) do not count.
        /// </summary>
        public static bool IsReceivedByQms(DBContext db, int? documentTransactionId) =>
            documentTransactionId != null
            && Scalar(db, @"SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.TB_R_CTRL_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @T
                    AND ISNULL(OPERATION_TYPE, 1) = 1 AND ISNULL(DELETE_FLAG, 0) = 0 AND [STATUS] IN ('2', '5', '6')
                ) THEN 1 ELSE 0 END AS BIT)", ("@T", documentTransactionId)) is bool b && b;

        /// <summary>The document has been sent to one of the user's departments (dbo.fn_User_RecipientDepartments).</summary>
        public static bool IsDistributedToUser(DBContext db, string username, int? documentTransactionId) =>
            !string.IsNullOrEmpty(username) && documentTransactionId != null
            && Scalar(db, @"SELECT CAST(CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.fn_User_RecipientDepartments(@U, @T)
                ) THEN 1 ELSE 0 END AS BIT)", ("@U", username), ("@T", documentTransactionId)) is bool b && b;

        public static bool HasFunction(HttpContext http, string functionId) =>
            (http.Session.GetString("functionList") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(functionId);

        static bool CanViewFile(DBContext db, string username, string filePath, string baseName)
        {
            using DbCommand command = Command(db, "[dbo].[sp_DocumentFile_CanView]", CommandType.StoredProcedure, out bool wasClosed);
            try
            {
                command.Parameters.Add(new SqlParameter("@USERNAME", username));
                command.Parameters.Add(new SqlParameter("@FILE_PATH", (object)filePath ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@BASE_NAME", (object)baseName ?? DBNull.Value));
                using var r = command.ExecuteReader();
                return r.Read() && Convert.ToBoolean(r["CAN_VIEW"]);
            }
            finally
            {
                if (wasClosed) command.Connection.Close();
            }
        }

        static bool IsDocumentControl(DBContext db, string username) =>
            Scalar(db, "SELECT dbo.fn_User_IsDocumentControl(@U)", ("@U", username)) is bool b && b;

        static bool IsOwnSignature(DBContext db, string username, string path) =>
            Scalar(db, "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.TB_M_USER WHERE USERNAME = @U AND SIGNATURE_PATH = @P) THEN 1 ELSE 0 END AS BIT)",
                ("@U", username), ("@P", path)) is bool b && b;

        static object Scalar(DBContext db, string sql, params (string name, object value)[] parameters)
        {
            using DbCommand command = Command(db, sql, CommandType.Text, out bool wasClosed);
            try
            {
                foreach (var (name, value) in parameters) command.Parameters.Add(new SqlParameter(name, value ?? DBNull.Value));
                object result = command.ExecuteScalar();
                return result == DBNull.Value ? null : result;
            }
            finally
            {
                if (wasClosed) command.Connection.Close();
            }
        }

        static DbCommand Command(DBContext db, string text, CommandType type, out bool wasClosed)
        {
            var connection = db.Database.GetDbConnection();
            wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = text;
            command.CommandType = type;
            var transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            if (transaction != null) command.Transaction = transaction;
            return command;
        }
    }
}
