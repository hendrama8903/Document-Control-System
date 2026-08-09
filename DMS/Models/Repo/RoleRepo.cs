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
    public class RoleRepo : BaseRepo
    {
        private RoleRepo() { }

        #region Singleton
        private static RoleRepo instance = null;
        public static RoleRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new RoleRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<Role> Search(Role data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@ROLE_NAME", CheckNullValue(data.ROLE_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Role_Search] @ROLE_ID, @ROLE_NAME, @PageNumber, @PageSize";
            IList<Role> Result = db.Role.FromSqlRaw<Role>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<RoleListItem> SearchGrid(Role data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@ROLE_NAME", CheckNullValue(data.ROLE_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Role_Search] @ROLE_ID, @ROLE_NAME, @PageNumber, @PageSize";
            IList<RoleListItem> Result = db.RoleListItem.FromSqlRaw<RoleListItem>(query, param.ToArray()).ToList();

            return Result;
        }

        public Role GetByKey(Role data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) )
            };

            string query = "EXEC [dbo].[sp_Role_GetByKey] @ROLE_ID";
            Role Result = db.Role.FromSqlRaw<Role>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(Role data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@ROLE_NAME", CheckNullValue(data.ROLE_NAME) ),
                new SqlParameter ( "@ROLE_DESC", CheckNullValue(data.ROLE_DESC) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Role_Insert] @ROLE_ID, @ROLE_NAME, @ROLE_DESC, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(Role data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@ROLE_NAME", CheckNullValue(data.ROLE_NAME) ),
                new SqlParameter ( "@ROLE_DESC", CheckNullValue(data.ROLE_DESC) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Role_Update] @ROLE_ID, @ROLE_NAME, @ROLE_DESC, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(Role data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Role_Delete] @ROLE_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
