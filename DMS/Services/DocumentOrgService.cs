using DMS.Models;
using DMS.Models.DB.Commons;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace DMS.Services
{
    /// <summary>
    /// Which division / department / creator a user may put on a document, and the
    /// departments a PIC handles (request Hendra 2026-11-17, see
    /// SQL/DocumentDepartmentScope_Migration.sql). A user prepares documents for the
    /// department of their own position; a PIC named in User Position > Departments Handled
    /// also for those departments. The department on a document is its owner: its
    /// managers approve it (fn_WorkflowDoc_Steps).
    /// </summary>
    public static class DocumentOrgService
    {
        public class HandledDepartment
        {
            public int DEPARTMENT_ID { get; set; }
            public string DEPARTMENT_CODE { get; set; }
            public string DEPARTMENT_NAME { get; set; }
            public string DIVISION { get; set; }
        }

        public static List<Select2> Divisions(DBContext db, string username, string q)
        {
            return Options(db, "[dbo].[sp_DocumentOrg_Divisions]",
                new SqlParameter("@USERNAME", Value(username)),
                new SqlParameter("@Q", Value(q)));
        }

        public static List<Select2> Departments(DBContext db, string username, string division, string q)
        {
            return Options(db, "[dbo].[sp_DocumentOrg_Departments]",
                new SqlParameter("@USERNAME", Value(username)),
                new SqlParameter("@DIVISION", Value(division)),
                new SqlParameter("@Q", Value(q)),
                new SqlParameter("@DEPARTMENT_ID", DBNull.Value));
        }

        public static List<Select2> Creators(DBContext db, string loginUser, int departmentId, string q)
        {
            return Options(db, "[dbo].[sp_DocumentOrg_Creators]",
                new SqlParameter("@LOGIN_USER", Value(loginUser)),
                new SqlParameter("@DEPARTMENT_ID", departmentId),
                new SqlParameter("@Q", Value(q)));
        }

        /// <summary>True when the user may put this department on a document.</summary>
        public static bool CanUseDepartment(DBContext db, string username, int departmentId)
        {
            return Options(db, "[dbo].[sp_DocumentOrg_Departments]",
                new SqlParameter("@USERNAME", Value(username)),
                new SqlParameter("@DIVISION", DBNull.Value),
                new SqlParameter("@Q", DBNull.Value),
                new SqlParameter("@DEPARTMENT_ID", departmentId)).Count > 0;
        }

        /// <summary>Number of departments the user may choose (more than one = the field is open).</summary>
        public static int DepartmentCount(DBContext db, string username)
        {
            return Departments(db, username, null, null).Count;
        }

        public static List<HandledDepartment> Handled(DBContext db, string username)
        {
            var items = new List<HandledDepartment>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = Procedure(connection, "[dbo].[sp_UserDeptScope_Get]");
            command.Parameters.Add(new SqlParameter("@USERNAME", Value(username)));
            using SqlDataReader r = command.ExecuteReader();
            while (r.Read())
            {
                items.Add(new HandledDepartment
                {
                    DEPARTMENT_ID = (int)r["DEPARTMENT_ID"],
                    DEPARTMENT_CODE = r["DEPARTMENT_CODE"] as string,
                    DEPARTMENT_NAME = r["DEPARTMENT_NAME"] as string,
                    DIVISION = r["DIVISION"] as string
                });
            }
            return items;
        }

        public static void SaveHandled(DBContext db, string username, IEnumerable<string> departmentIds, string loginUser)
        {
            string ids = string.Join(",", (departmentIds ?? Enumerable.Empty<string>())
                .Where(x => int.TryParse(x, out _)));
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = Procedure(connection, "[dbo].[sp_UserDeptScope_Save]");
            command.Parameters.Add(new SqlParameter("@USERNAME", Value(username)));
            command.Parameters.Add(new SqlParameter("@DEPARTMENT_IDS", ids));
            command.Parameters.Add(new SqlParameter("@LOGIN_USER", Value(loginUser)));
            using SqlDataReader r = command.ExecuteReader();
            if (r.Read() && !Convert.ToBoolean(r["status"]))
                throw new InvalidOperationException(r["message"] as string);
        }

        static List<Select2> Options(DBContext db, string procedure, params SqlParameter[] parameters)
        {
            var list = new List<Select2>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using SqlCommand command = Procedure(connection, procedure);
            command.Parameters.AddRange(parameters);
            using SqlDataReader r = command.ExecuteReader();
            while (r.Read())
                list.Add(new Select2 { id = Convert.ToString(r["id"]), text = r["text"] as string });
            return list;
        }

        static SqlCommand Procedure(SqlConnection connection, string name)
        {
            return new SqlCommand(name, connection) { CommandType = CommandType.StoredProcedure };
        }

        static object Value(object value)
        {
            return value is string s ? (string.IsNullOrWhiteSpace(s) ? DBNull.Value : s) : (value ?? DBNull.Value);
        }
    }
}
