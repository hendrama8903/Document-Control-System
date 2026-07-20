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
    public class LoginRepo : BaseRepo
    {

        private LoginRepo() { }

        #region Singleton
        private static LoginRepo instance = null;
        public static LoginRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LoginRepo();
                }
                return instance;
            }
        }
        #endregion

        public User GetUserByUsername(User data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) )
            };

            string query = "EXEC [dbo].[sp_Auth_Login] @USERNAME";
            User Result = db.User.FromSqlRaw<User>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public List<Menu> GetMenuByRole(User data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) )
            };

            string query = "EXEC [dbo].[sp_Auth_Menu] @ROLE_ID";
            List<Menu> Result = db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).ToList();

            return Result;
        }

        public List<Function> GetFunctionByRole(User data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) )
            };

            string query = "EXEC [dbo].[sp_Auth_Function] @ROLE_ID";
            List<Function> Result = db.Function.FromSqlRaw<Function>(query, param.ToArray()).ToList();

            return Result;
        }
    }
}
