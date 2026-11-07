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
    }
}
