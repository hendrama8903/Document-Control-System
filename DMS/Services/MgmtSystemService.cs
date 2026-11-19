using DMS.Models;

namespace DMS.Services
{
    /// <summary>
    /// Management System (2026-11-19): QMS, K3L, ... (TB_M_MGMT_SYSTEM, SQL/ManagementSystem_Migration.sql).
    /// A document belongs to one system. NUMBER_CODE goes into Level 1-2 numbers in place of the
    /// year / process code; an archived system takes no new documents; when a system lists its
    /// CREATORs only they may create its documents (sp_DocumentMaintenance_Insert checks it too).
    /// </summary>
    public static class MgmtSystemService
    {
        public class Option
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string NumberCode { get; set; }
        }

        /// <summary>Active systems the user may create documents in, QMS first.</summary>
        public static List<Option> CreatableSystems(DBContext db, string username) =>
            OrgChangeService.Query(db, @"
                SELECT S.MGMT_SYSTEM_CODE, S.MGMT_SYSTEM_NAME, S.NUMBER_CODE
                FROM dbo.TB_M_MGMT_SYSTEM S
                WHERE S.IS_ACTIVE = 1
                  AND (NOT EXISTS (SELECT 1 FROM dbo.TB_M_MGMT_SYSTEM_USER U WHERE U.MGMT_SYSTEM_CODE = S.MGMT_SYSTEM_CODE AND U.USER_ROLE = 'CREATOR')
                       OR EXISTS (SELECT 1 FROM dbo.TB_M_MGMT_SYSTEM_USER U WHERE U.MGMT_SYSTEM_CODE = S.MGMT_SYSTEM_CODE AND U.USER_ROLE = 'CREATOR' AND U.USERNAME = @U))
                ORDER BY S.SORT_NO, S.MGMT_SYSTEM_CODE", ("@U", username))
            .Select(r => new Option
            {
                Code = r["MGMT_SYSTEM_CODE"] as string,
                Name = r["MGMT_SYSTEM_NAME"] as string,
                NumberCode = r["NUMBER_CODE"] as string
            }).ToList();

        /// <summary>Every system (active or archived), for filters and the master page.</summary>
        public static List<Option> AllSystems(DBContext db) =>
            OrgChangeService.Query(db, "SELECT MGMT_SYSTEM_CODE, MGMT_SYSTEM_NAME, NUMBER_CODE FROM dbo.TB_M_MGMT_SYSTEM ORDER BY SORT_NO, MGMT_SYSTEM_CODE")
            .Select(r => new Option { Code = r["MGMT_SYSTEM_CODE"] as string, Name = r["MGMT_SYSTEM_NAME"] as string, NumberCode = r["NUMBER_CODE"] as string })
            .ToList();

        /// <summary>Document Control of this system (fn_User_IsDocumentControlFor, IMS tahap 2).</summary>
        public static bool IsDocumentControlFor(DBContext db, string username, string system) =>
            !string.IsNullOrEmpty(username) && OrgChangeService.Scalar(db, "SELECT dbo.fn_User_IsDocumentControlFor(@U, @S)", ("@U", username), ("@S", system)) is bool b && b;

        /// <summary>The system of a document (its current revision), with its name.</summary>
        public static Option OfDocument(DBContext db, string documentCode)
        {
            var r = OrgChangeService.Query(db, @"
                SELECT TOP 1 S.MGMT_SYSTEM_CODE, S.MGMT_SYSTEM_NAME, S.NUMBER_CODE
                FROM dbo.TB_R_DOCUMENT D JOIN dbo.TB_M_MGMT_SYSTEM S ON S.MGMT_SYSTEM_CODE = ISNULL(D.MGMT_SYSTEM, 'QMS')
                WHERE D.DOCUMENT_CODE = @C AND ISNULL(D.DELETE_FLAG, 0) = 0 ORDER BY D.REVISION DESC", ("@C", documentCode)).FirstOrDefault();
            return r == null ? null : new Option { Code = r["MGMT_SYSTEM_CODE"] as string, Name = r["MGMT_SYSTEM_NAME"] as string, NumberCode = r["NUMBER_CODE"] as string };
        }
    }
}
