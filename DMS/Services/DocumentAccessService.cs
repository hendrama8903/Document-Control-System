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
