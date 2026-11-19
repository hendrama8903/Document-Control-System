using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DMS.Services
{
    /// <summary>
    /// Binders (bantex) in Document Control (request Hendra 2026-11-15, see
    /// SQL/DocumentBinder_Migration.sql). A Document Control folder can be a master binder
    /// (holds documents) or a controlled copy binder kept by a department (holds the numbered
    /// copies of the Copy Register filed in it).
    /// </summary>
    public static class DocumentBinderService
    {
        public const string Master = "MASTER";
        public const string Copy = "COPY";

        public class BinderItem
        {
            public int? EXPORT_ID { get; set; }
            public int DOCUMENT_TRANSACTION_ID { get; set; }
            public string DOCUMENT_CODE { get; set; }
            public string DOCUMENT_NAME { get; set; }
            public int? REVISION { get; set; }
            public int? CURRENT_REVISION { get; set; }
            public int? COPY_NO { get; set; }
            public string RECIPIENT { get; set; }
            public DateTime? ISSUED_DT { get; set; }
            public bool IS_OBSOLETE { get; set; }
        }

        public class ActionItems
        {
            public int REGISTRATION_WAITING { get; set; }
            public int DISTRIBUTION_NOT_SENT { get; set; }
            public int PRINT_REQUEST_WAITING { get; set; }
        }

        public class DepartmentOption
        {
            public int DEPARTMENT_ID { get; set; }
            public string DEPARTMENT_CODE { get; set; }
            public string DEPARTMENT_NAME { get; set; }
            public string DIVISION { get; set; }
        }

        static SqlCommand Procedure(SqlConnection connection, string name)
        {
            SqlCommand command = connection.CreateCommand();
            command.CommandText = name;
            command.CommandType = CommandType.StoredProcedure;
            return command;
        }

        static object Value(object value) => value ?? DBNull.Value;

        /// <summary>Saves the binder details of a folder; the procedure throws a readable message when refused.</summary>
        public static void Save(DBContext db, int folderId, string folderType, string folderCode, int? departmentId,
            string location, string remarks, string username)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = Procedure(connection, "[dbo].[sp_DocumentFolder_SaveBinder]");
            command.Parameters.Add(new SqlParameter("@FOLDER_ID", folderId));
            command.Parameters.Add(new SqlParameter("@FOLDER_TYPE", Value(folderType)));
            command.Parameters.Add(new SqlParameter("@FOLDER_CODE", Value(folderCode)));
            command.Parameters.Add(new SqlParameter("@DEPARTMENT_ID", Value(departmentId)));
            command.Parameters.Add(new SqlParameter("@LOCATION", SqlDbType.NVarChar, 200) { Value = Value(location) });
            command.Parameters.Add(new SqlParameter("@REMARKS", SqlDbType.NVarChar, 500) { Value = Value(remarks) });
            command.Parameters.Add(new SqlParameter("@USERNAME", username));
            command.ExecuteNonQuery();
        }

        /// <summary>Copies in circulation of a controlled copy binder, or the documents of a master binder / folder.</summary>
        public static List<BinderItem> Contents(DBContext db, int folderId)
        {
            var items = new List<BinderItem>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = Procedure(connection, "[dbo].[sp_DocumentBinder_Contents]");
            command.Parameters.Add(new SqlParameter("@FOLDER_ID", folderId));

            using SqlDataReader r = command.ExecuteReader();
            while (r.Read())
            {
                items.Add(new BinderItem
                {
                    EXPORT_ID = r["EXPORT_ID"] as int?,
                    DOCUMENT_TRANSACTION_ID = (int)r["DOCUMENT_TRANSACTION_ID"],
                    DOCUMENT_CODE = r["DOCUMENT_CODE"] as string,
                    DOCUMENT_NAME = r["DOCUMENT_NAME"] as string,
                    REVISION = r["REVISION"] as int?,
                    CURRENT_REVISION = r["CURRENT_REVISION"] as int?,
                    COPY_NO = r["COPY_NO"] as int?,
                    RECIPIENT = r["RECIPIENT"] as string,
                    ISSUED_DT = r["ISSUED_DT"] as DateTime?,
                    IS_OBSOLETE = (bool)r["IS_OBSOLETE"],
                });
            }
            return items;
        }

        /// <summary>Files a controlled copy in a binder, or takes it out when folderId is null.</summary>
        public static void AssignCopy(DBContext db, int exportId, int? folderId)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = Procedure(connection, "[dbo].[sp_DocumentExport_AssignBinder]");
            command.Parameters.Add(new SqlParameter("@EXPORT_ID", exportId));
            command.Parameters.Add(new SqlParameter("@FOLDER_ID", Value(folderId)));
            command.ExecuteNonQuery();
        }

        /// <summary>"CODE - Name" of the department holding a controlled copy binder; null when the
        /// folder is not such a binder. The default recipient of a copy filed in it.</summary>
        public static string HolderLabel(DBContext db, int folderId)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT D.DEPARTMENT_CODE + ' - ' + D.DEPARTMENT_NAME
                                    FROM [dbo].[TB_M_DOCUMENT_FOLDER] F
                                    JOIN [dbo].[TB_M_DEPARTMENT] D ON D.DEPARTMENT_ID = F.DEPARTMENT_ID
                                    WHERE F.FOLDER_ID = @FOLDER_ID AND F.FOLDER_TYPE = 'COPY' AND ISNULL(F.DELETE_FLAG, 0) = 0";
            command.Parameters.Add(new SqlParameter("@FOLDER_ID", folderId));
            return command.ExecuteScalar() as string;
        }

        /// <summary>"Needs Action" counts that come from Document Registration and Print Request.</summary>
        public static ActionItems GetActionItems(DBContext db, string mgmtSystem = null)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = Procedure(connection, "[dbo].[sp_DocumentControlDashboard_ActionItems]");
            // Management System filter of the dashboard (2026-11-19)
            command.Parameters.Add(new SqlParameter("@MGMT_SYSTEM", string.IsNullOrWhiteSpace(mgmtSystem) ? DBNull.Value : mgmtSystem.Trim()));
            using SqlDataReader r = command.ExecuteReader();
            if (!r.Read()) return new ActionItems();
            return new ActionItems
            {
                REGISTRATION_WAITING = (int)r["REGISTRATION_WAITING"],
                DISTRIBUTION_NOT_SENT = (int)r["DISTRIBUTION_NOT_SENT"],
                PRINT_REQUEST_WAITING = (int)r["PRINT_REQUEST_WAITING"],
            };
        }

        /// <summary>Active departments, for the holder of a binder.</summary>
        public static List<DepartmentOption> Departments(DBContext db)
        {
            var items = new List<DepartmentOption>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = @"SELECT DEPARTMENT_ID, DEPARTMENT_CODE, DEPARTMENT_NAME, DIVISION
                                    FROM [dbo].[TB_M_DEPARTMENT]
                                    WHERE ISNULL(DELETE_FLAG, 0) = 0
                                    ORDER BY DIVISION, DEPARTMENT_CODE";
            using SqlDataReader r = command.ExecuteReader();
            while (r.Read())
            {
                items.Add(new DepartmentOption
                {
                    DEPARTMENT_ID = (int)r["DEPARTMENT_ID"],
                    DEPARTMENT_CODE = r["DEPARTMENT_CODE"] as string,
                    DEPARTMENT_NAME = r["DEPARTMENT_NAME"] as string,
                    DIVISION = r["DIVISION"] as string,
                });
            }
            return items;
        }
    }
}
