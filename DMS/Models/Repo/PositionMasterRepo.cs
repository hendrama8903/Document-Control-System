using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class PositionMasterRepo : BaseRepo
    {
        private PositionMasterRepo() { }

        #region Singleton
        private static PositionMasterRepo instance = null;
        public static PositionMasterRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PositionMasterRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<PositionMaster> Search(PositionMaster data, DBContext db, int? PageNumber, int? PageSize, bool showAll = false)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@POSITION_NAME", CheckNullValue(data.POSITION_NAME) ),
                new SqlParameter ( "@POSITION_LEVEL", CheckNullValue(data.POSITION_LEVEL) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) ),
                new SqlParameter ( "@SHOW_DELETED", "0" ),
                new SqlParameter ( "@SHOW_ALL", showAll ? "1" : "0" )
            };

            string query = "EXEC [dbo].[sp_PositionMaster_Search] @POSITION_NAME, @POSITION_LEVEL, @PageNumber, @PageSize, @SHOW_DELETED, @SHOW_ALL";
            IList<PositionMaster> Result = db.PositionMaster.FromSqlRaw<PositionMaster>(query, param.ToArray()).ToList();

            return Result;
        }

        public PositionMaster GetByKey(PositionMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@POSITION_ID", CheckNullValue(data.POSITION_ID) )
            };

            string query = "EXEC [dbo].[sp_PositionMaster_GetByKey] @POSITION_ID";
            PositionMaster Result = db.PositionMaster.FromSqlRaw<PositionMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(PositionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@POSITION_NAME", CheckNullValue(data.POSITION_NAME) ),
                new SqlParameter ( "@POSITION_LEVEL", CheckNullValue(data.POSITION_LEVEL) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_PositionMaster_Insert] @POSITION_NAME, @POSITION_LEVEL, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(PositionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@POSITION_ID", CheckNullValue(data.POSITION_ID) ),
                new SqlParameter ( "@POSITION_NAME", CheckNullValue(data.POSITION_NAME) ),
                new SqlParameter ( "@POSITION_LEVEL", CheckNullValue(data.POSITION_LEVEL) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_PositionMaster_Update] @POSITION_ID, @POSITION_NAME, @POSITION_LEVEL, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(PositionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@POSITION_ID", CheckNullValue(data.POSITION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_PositionMaster_Delete] @POSITION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
