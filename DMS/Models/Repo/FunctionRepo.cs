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
    public class FunctionRepo : BaseRepo
    {
        private FunctionRepo() { }

        #region Singleton
        private static FunctionRepo instance = null;
        public static FunctionRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new FunctionRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<Function> GetAll(DBContext db)
        {
            string query = "EXEC [dbo].[sp_Function_GetAll]";
            IList<Function> Result = db.Function.FromSqlRaw<Function>(query).ToList();

            return Result;
        }

        public IList<Function> Search(Function data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID) ),
                new SqlParameter ( "@FUNCTION_NAME", CheckNullValue(data.FUNCTION_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Function_Search] @FUNCTION_ID, @FUNCTION_NAME, @PageNumber, @PageSize";
            IList<Function> Result = db.Function.FromSqlRaw<Function>(query, param.ToArray()).ToList();

            return Result;
        }

        public Function GetByKey(Function data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID) )
            };

            string query = "EXEC [dbo].[sp_Function_GetByKey] @FUNCTION_ID";
            Function Result = db.Function.FromSqlRaw<Function>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public IList<Function> GetByMenu(Function data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) )
            };

            string query = "EXEC [dbo].[sp_Function_GetByMenu] @MENU_ID";
            IList<Function> Result = db.Function.FromSqlRaw<Function>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult Insert(Function data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID) ),
                new SqlParameter ( "@FUNCTION_NAME", CheckNullValue(data.FUNCTION_NAME) ),
                new SqlParameter ( "@FUNCTION_DESC", CheckNullValue(data.FUNCTION_DESC) ),
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Function_Insert] @FUNCTION_ID, @FUNCTION_NAME, @FUNCTION_DESC, @MENU_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(Function data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID) ),
                new SqlParameter ( "@FUNCTION_NAME", CheckNullValue(data.FUNCTION_NAME) ),
                new SqlParameter ( "@FUNCTION_DESC", CheckNullValue(data.FUNCTION_DESC) ),
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Function_Update] @FUNCTION_ID, @FUNCTION_NAME, @FUNCTION_DESC, @MENU_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(Function data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FUNCTION_ID", CheckNullValue(data.FUNCTION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Function_Delete] @FUNCTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
