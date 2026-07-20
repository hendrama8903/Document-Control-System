using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class MSystemRepo : BaseRepo
    {
        private MSystemRepo() { }

        #region Singleton
        private static MSystemRepo instance = null;
        public static MSystemRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MSystemRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<MSystem> GetByType(string systemType, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SYSTEM_TYPE", systemType ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_MSystem_GetByType] @SYSTEM_TYPE, @PageNumber, @PageSize";
            IList<MSystem> Result = db.MSystem.FromSqlRaw<MSystem>(query, param.ToArray()).ToList();

            return Result;
        }

        public MSystem GetByTypeAndCode(string systemType, string systemCode, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SYSTEM_TYPE", systemType ),
                new SqlParameter ( "@SYSTEM_CODE", systemCode )
            };

            string query = "EXEC [dbo].[sp_System_GetByTypeAndCode] @SYSTEM_TYPE, @SYSTEM_CODE";
            MSystem Result = db.MSystem.FromSqlRaw<MSystem>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public IList<MSystem> Search(MSystem data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SYSTEM_TYPE", CheckNullValue(data.SYSTEM_TYPE) ),
                new SqlParameter ( "@SYSTEM_CODE", CheckNullValue(data.SYSTEM_CODE) ),
                new SqlParameter ( "@SYSTEM_VALUE", CheckNullValue(data.SYSTEM_VALUE) ),
                new SqlParameter ( "@SYSTEM_CODE_VALUE", CheckNullValue(data.SYSTEM_CODE_VALUE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_MSystem_Search] @SYSTEM_TYPE, @SYSTEM_CODE, @SYSTEM_VALUE, @SYSTEM_CODE_VALUE, @STATUS, @PageNumber, @PageSize";
            IList<MSystem> Result = db.MSystem.FromSqlRaw<MSystem>(query, param.ToArray()).ToList();

            return Result;
        }

        public MSystem GetByKey(MSystem data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SYSTEM_TYPE", CheckNullValue(data.SYSTEM_TYPE) ),
                new SqlParameter ( "@SYSTEM_CODE", CheckNullValue(data.SYSTEM_CODE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) )
            };

            string query = "EXEC [dbo].[sp_MSystem_GetByKey] @SYSTEM_TYPE, @SYSTEM_CODE, @STATUS";
            MSystem Result = db.MSystem.FromSqlRaw<MSystem>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(MSystem data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SYSTEM_TYPE", CheckNullValue(data.SYSTEM_TYPE) ),
                new SqlParameter ( "@SYSTEM_CODE", CheckNullValue(data.SYSTEM_CODE) ),
                new SqlParameter ( "@SYSTEM_VALUE", CheckNullValue(data.SYSTEM_VALUE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_MSystem_Insert] @SYSTEM_TYPE, @SYSTEM_CODE, @SYSTEM_VALUE, @STATUS," +
                " @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(System.Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(MSystem data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SYSTEM_TYPE", CheckNullValue(data.SYSTEM_TYPE) ),
                new SqlParameter ( "@SYSTEM_CODE", CheckNullValue(data.SYSTEM_CODE) ),
                new SqlParameter ( "@SYSTEM_VALUE", CheckNullValue(data.SYSTEM_VALUE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_MSystem_Update] @SYSTEM_TYPE, @SYSTEM_CODE, @SYSTEM_VALUE, @STATUS," +
                " @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(System.Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(MSystem data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SYSTEM_TYPE", CheckNullValue(data.SYSTEM_TYPE) ),
                new SqlParameter ( "@SYSTEM_CODE", CheckNullValue(data.SYSTEM_CODE) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_MSystem_Delete] @SYSTEM_TYPE, @SYSTEM_CODE, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(System.Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public MSystem GetTop1BySysVal(MSystem data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SYSTEM_TYPE", CheckNullValue(data.SYSTEM_TYPE) ),
                new SqlParameter ( "@SYSTEM_VALUE", CheckNullValue(data.SYSTEM_VALUE) ),
                new SqlParameter ( "@SYSTEM_CODE", CheckNullValue(data.SYSTEM_CODE) ),
            };

            string query = "EXEC [dbo].[sp_MSystem_GetTop1BySysVal] @SYSTEM_TYPE, @SYSTEM_VALUE, @SYSTEM_CODE";
            MSystem Result = db.MSystem.FromSqlRaw<MSystem>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }
    }
}
