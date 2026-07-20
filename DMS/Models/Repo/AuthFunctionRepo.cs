using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.Repo
{
    public class AuthFunctionRepo : BaseRepo
    {
        private AuthFunctionRepo() { }

        #region Singleton
        private static AuthFunctionRepo instance = null;
        public static AuthFunctionRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AuthFunctionRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<AuthFunction> Search(AuthFunction data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) )
            };

            string query = "EXEC [dbo].[sp_AuthFunction_Search] @ROLE_ID";
            IList<AuthFunction> Result = db.AuthFunction.FromSqlRaw<AuthFunction>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<AuthFunction> GetAll(DBContext db)
        {
            string query = "EXEC [dbo].[sp_AuthFunction_GetAll]";
            IList<AuthFunction> Result = db.AuthFunction.FromSqlRaw<AuthFunction>(query).ToList();

            return Result;
        }

        public DBResult Insert(AuthFunction data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_AuthFunction_Insert] @ROLE_ID, @FUNCTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult DeleteByRole(AuthFunction data, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_AuthFunction_DeleteByRole] @ROLE_ID, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}