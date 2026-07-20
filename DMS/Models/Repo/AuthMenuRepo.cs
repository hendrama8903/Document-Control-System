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
    public class AuthMenuRepo : BaseRepo
    {
        private AuthMenuRepo() { }

        #region Singleton
        private static AuthMenuRepo instance = null;
        public static AuthMenuRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new AuthMenuRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<AuthMenu> Search(AuthMenu data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) )
            };

            string query = "EXEC [dbo].[sp_AuthMenu_Search] @ROLE_ID";
            IList<AuthMenu> Result = db.AuthMenu.FromSqlRaw<AuthMenu>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<AuthMenu> GetAll(DBContext db)
        {
            string query = "EXEC [dbo].[sp_AuthMenu_GetAll]";
            IList<AuthMenu> Result = db.AuthMenu.FromSqlRaw<AuthMenu>(query).ToList();

            return Result;
        }

        public DBResult Insert(AuthMenu data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_AuthMenu_Insert] @ROLE_ID, @MENU_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult DeleteByRole(AuthMenu data, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_AuthMenu_DeleteByRole] @ROLE_ID, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}