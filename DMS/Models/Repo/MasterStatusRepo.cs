using DMS.Common.Models;
using DMS.Common.Repo;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class MasterStatusRepo : BaseRepo
    {
        private MasterStatusRepo() { }

        #region Singleton
        private static MasterStatusRepo instance = null;
        public static MasterStatusRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MasterStatusRepo();
                }
                return instance;
            }
        }
        #endregion

        public DBResult Activate(string entity, int id, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter("@ENTITY", entity),
                new SqlParameter("@ID", id),
                new SqlParameter("@LOGIN_USER", loginUser),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_MasterData_Activate] @ENTITY, @ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            db.Database.ExecuteSqlRaw(query, param.ToArray());

            return new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
        }

        // Delete a never-used Division / Department / Section (soft delete, DELETE_FLAG = 2).
        // See SQL/MasterDataRemove_Migration.sql (request Hendra 2026-11-16).
        public DBResult Remove(string entity, int id, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter("@ENTITY", entity),
                new SqlParameter("@ID", id),
                new SqlParameter("@LOGIN_USER", loginUser),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_MasterData_Remove] @ENTITY, @ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            db.Database.ExecuteSqlRaw(query, param.ToArray());

            return new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
        }

        // Where each row is used ("" = never used, may be deleted), keyed by its id.
        // Read with a plain reader: the master entities keep their own columns, so adding a
        // usage column to them would break every other FromSqlRaw on those entities.
        public Dictionary<int, string> Usage(string entity, DBContext db)
        {
            var usage = new Dictionary<int, string>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "[dbo].[sp_MasterData_Usage]";
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@ENTITY", entity));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                usage[(int)reader["ID"]] = reader["USED_BY"] as string ?? "";
            }
            return usage;
        }
    }
}
