using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DMS.Services
{
    /// <summary>
    /// Register of Download PDF in Document Control (request Hendra 2026-11-11, see
    /// SQL/DocumentExportPdf_Migration.sql). Every export is recorded; a CONTROLLED copy gets
    /// the next copy number of its document revision from sp_DocumentExport_Insert.
    /// </summary>
    public static class DocumentExportService
    {
        public const string FunctionId = "DOCUMENTCONTROLDASHBOARD-EXPORTPDF";

        public const string Master = "MASTER";
        public const string Controlled = "CONTROLLED";
        public const string Uncontrolled = "UNCONTROLLED";

        public static bool IsPurpose(string purpose) =>
            purpose == Master || purpose == Controlled || purpose == Uncontrolled;

        /// <summary>Records the export and returns the copy number (null unless CONTROLLED).</summary>
        public static int? Record(DBContext db, int documentTransactionId, string documentCode, int? revision,
            string purpose, string recipient, string notes, string username)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "[dbo].[sp_DocumentExport_Insert]";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@DOCUMENT_TRANSACTION_ID", documentTransactionId));
            command.Parameters.Add(new SqlParameter("@DOCUMENT_CODE", (object)documentCode ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@REVISION", (object)revision ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@PURPOSE", purpose));
            command.Parameters.Add(new SqlParameter("@RECIPIENT", (object)recipient ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@NOTES", (object)notes ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@USERNAME", username));

            using var reader = command.ExecuteReader();
            if (!reader.Read()) return null;
            return reader["COPY_NO"] is int copyNo ? copyNo : null;
        }

        public class CopyRegisterItem
        {
            public int EXPORT_ID { get; set; }
            public int DOCUMENT_TRANSACTION_ID { get; set; }
            public string DOCUMENT_CODE { get; set; }
            public int? REVISION { get; set; }
            public int? COPY_NO { get; set; }
            public string RECIPIENT { get; set; }
            public string NOTES { get; set; }
            public string CREATED_BY { get; set; }
            public DateTime CREATED_DT { get; set; }
            public string WITHDRAWN_BY { get; set; }
            public DateTime? WITHDRAWN_DT { get; set; }
            public string WITHDRAW_NOTE { get; set; }
            public bool IS_OBSOLETE { get; set; }
        }

        /// <summary>Every controlled copy of every revision of the document (sp_DocumentExport_CopyRegister).</summary>
        public static List<CopyRegisterItem> CopyRegister(DBContext db, int documentTransactionId)
        {
            var items = new List<CopyRegisterItem>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "[dbo].[sp_DocumentExport_CopyRegister]";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@DOCUMENT_TRANSACTION_ID", documentTransactionId));

            using var r = command.ExecuteReader();
            while (r.Read())
            {
                items.Add(new CopyRegisterItem
                {
                    EXPORT_ID = (int)r["EXPORT_ID"],
                    DOCUMENT_TRANSACTION_ID = (int)r["DOCUMENT_TRANSACTION_ID"],
                    DOCUMENT_CODE = r["DOCUMENT_CODE"] as string,
                    REVISION = r["REVISION"] as int?,
                    COPY_NO = r["COPY_NO"] as int?,
                    RECIPIENT = r["RECIPIENT"] as string,
                    NOTES = r["NOTES"] as string,
                    CREATED_BY = r["CREATED_BY"] as string,
                    CREATED_DT = (DateTime)r["CREATED_DT"],
                    WITHDRAWN_BY = r["WITHDRAWN_BY"] as string,
                    WITHDRAWN_DT = r["WITHDRAWN_DT"] as DateTime?,
                    WITHDRAW_NOTE = r["WITHDRAW_NOTE"] as string,
                    IS_OBSOLETE = (bool)r["IS_OBSOLETE"],
                });
            }
            return items;
        }

        /// <summary>Marks a controlled copy as withdrawn; throws when it is missing or already withdrawn.</summary>
        public static void Withdraw(DBContext db, int exportId, string note, string username)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "[dbo].[sp_DocumentExport_Withdraw]";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@EXPORT_ID", exportId));
            command.Parameters.Add(new SqlParameter("@NOTE", (object)note ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@USERNAME", username));
            command.ExecuteNonQuery();
        }
    }
}
