using DMS.Common.Controllers;
using DMS.Models;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DMS.Controllers
{
    /// <summary>
    /// Target of the QR code printed in a signature box (signing method QR, request Hendra
    /// 2026-11-09). Login is required; after login the user returns to this page.
    /// </summary>
    public class VerifyController : BaseController
    {
        private readonly DBContext db;
        private readonly IWebHostEnvironment environment;

        public VerifyController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            this.environment = environment;
        }

        public class SignatureInfo
        {
            public string Kind { get; set; }
            public string SignerName { get; set; }
            public string SignerPosition { get; set; }
            public DateTime SignedDt { get; set; }
            public string RecordedHash { get; set; }
            public string DocumentCode { get; set; }
            public string DocumentTitle { get; set; }
            public string Revision { get; set; }
            public string DepartmentName { get; set; }
            public string FilePath { get; set; }
            public bool IsCurrentRevision { get; set; }
            public string FileCheck { get; set; }   // MATCH | CHANGED | UNKNOWN
        }

        public IActionResult Signature(string id)
        {
            if (GetLoginUsername() == null)
                return Redirect("/Auth/Login?param=" + Uri.EscapeDataString("/Verify/Signature/" + (id ?? "")));

            ViewData["Title"] = "Signature Verification";
            SignatureInfo info = System.Text.RegularExpressions.Regex.IsMatch(id ?? "", "^[0-9a-fA-F]{32}$") ? Load(id) : null;
            if (info != null)
            {
                string full = UploadPath.Resolve(environment.WebRootPath, info.FilePath);
                string currentHash = SignatureService.FileHash(full);
                info.FileCheck = info.RecordedHash == null || currentHash == null ? "UNKNOWN"
                    : string.Equals(info.RecordedHash, currentHash, StringComparison.OrdinalIgnoreCase) ? "MATCH" : "CHANGED";
            }
            return View(info);
        }

        // The signed revision is either still live in TB_R_DOCUMENT, or was moved to
        // TB_R_DOCUMENT_HISTORY when a newer revision was approved.
        SignatureInfo Load(string token)
        {
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TOP 1 S.KIND, S.SIGNER_NAME, S.SIGNER_POSITION, S.SIGNED_DT, S.DOCUMENT_FILE_HASH,
       COALESCE(D.DOCUMENT_CODE, H.DOCUMENT_CODE) AS DOCUMENT_CODE,
       COALESCE(D.DOCUMENT_TRANSACTION_NAME, H.DOCUMENT_TRANSACTION_NAME) AS DOCUMENT_TITLE,
       COALESCE(D.REVISION, H.REVISION) AS REVISION,
       COALESCE(D.DEPARTMENT_NAME, H.DEPARTMENT_NAME) AS DEPARTMENT_NAME,
       COALESCE(D.FILE_PATH, H.FILE_PATH) AS FILE_PATH,
       CASE WHEN D.DOCUMENT_TRANSACTION_ID IS NOT NULL THEN 1 ELSE 0 END AS IS_CURRENT
FROM dbo.TB_R_SIGNATURE S
LEFT JOIN dbo.TB_R_DOCUMENT D ON D.DOCUMENT_TRANSACTION_ID = S.DOCUMENT_TRANSACTION_ID
OUTER APPLY (SELECT TOP 1 * FROM dbo.TB_R_DOCUMENT_HISTORY X
             WHERE X.DOCUMENT_TRANSACTION_ID = S.DOCUMENT_TRANSACTION_ID
             ORDER BY X.DOCUMENT_HISTORY_ID DESC) H
WHERE S.SIGN_METHOD = 'QR' AND S.SIGN_DATA = @TOKEN";
                command.Parameters.Add(new SqlParameter("@TOKEN", token));
                using var r = command.ExecuteReader();
                if (!r.Read()) return null;
                string Str(string col) => r[col] == DBNull.Value ? null : Convert.ToString(r[col]);
                return new SignatureInfo
                {
                    Kind = Str("KIND"),
                    SignerName = Str("SIGNER_NAME"),
                    SignerPosition = Str("SIGNER_POSITION"),
                    SignedDt = Convert.ToDateTime(r["SIGNED_DT"]),
                    RecordedHash = Str("DOCUMENT_FILE_HASH"),
                    DocumentCode = Str("DOCUMENT_CODE"),
                    DocumentTitle = Str("DOCUMENT_TITLE"),
                    Revision = Str("REVISION"),
                    DepartmentName = Str("DEPARTMENT_NAME"),
                    FilePath = Str("FILE_PATH"),
                    IsCurrentRevision = Convert.ToInt32(r["IS_CURRENT"]) == 1,
                };
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }
    }
}
