using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DivisionMasterRepo : BaseRepo
    {
        private DivisionMasterRepo() { }

        #region Singleton
        private static DivisionMasterRepo instance = null;
        public static DivisionMasterRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DivisionMasterRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<DivisionMaster> Search(DivisionMaster data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DIVISION_CODE", CheckNullValue(data.DIVISION_CODE) ),
                new SqlParameter ( "@DIVISION_NAME", CheckNullValue(data.DIVISION_NAME) ),
                new SqlParameter ( "@DIVISION_CODE_NAME", CheckNullValue(data.DIVISION_CODE_NAME) ),
                new SqlParameter ("@IS_VALID_ONLY", CheckNullValue(data.IS_VALID_ONLY)),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DivisionMaster_Search] @DIVISION_CODE, @DIVISION_NAME, @DIVISION_CODE_NAME,@IS_VALID_ONLY, @PageNumber, @PageSize";
            IList<DivisionMaster> Result = db.DivisionMaster.FromSqlRaw<DivisionMaster>(query, param.ToArray()).ToList();

            return Result;
        }

        public DivisionMaster GetByKey(DivisionMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DIVISION_ID", CheckNullValue(data.DIVISION_ID) )
            };

            string query = "EXEC [dbo].[sp_DivisionMaster_GetByKey] @DIVISION_ID";
            DivisionMaster Result = db.DivisionMaster.FromSqlRaw<DivisionMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(DivisionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DIVISION_CODE", CheckNullValue(data.DIVISION_CODE) ),
                new SqlParameter ( "@DIVISION_NAME", CheckNullValue(data.DIVISION_NAME) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DivisionMaster_Insert] @DIVISION_CODE, @DIVISION_NAME, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(DivisionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DIVISION_ID", CheckNullValue(data.DIVISION_ID) ),
                new SqlParameter ( "@DIVISION_CODE", CheckNullValue(data.DIVISION_CODE) ),
                new SqlParameter ( "@DIVISION_NAME", CheckNullValue(data.DIVISION_NAME) ),
                //new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                //new SqlParameter ( "@WORKFLOW_ID", CheckNullValue(data.WORKFLOW_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DivisionMaster_Update] @DIVISION_ID, @DIVISION_CODE, @DIVISION_NAME, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        //public DBResult Delete(DivisionMaster data, string loginUser, DBContext db)
        //{
        //    SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
        //    SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

        //    List<SqlParameter> param = new List<SqlParameter>
        //    {
        //        returnVal,
        //        new SqlParameter ( "@DIVISION_ID", CheckNullValue(data.DIVISION_ID) ),
        //        new SqlParameter ( "@LOGIN_USER", loginUser ),
        //        returnMsg
        //    };

        //    string query = "EXEC @RETURN_VAL = [dbo].[sp_DivisionMaster_Delete] @DIVISION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
        //    int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

        //    DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
        //    return result;
        //}
        public DBResult Delete(List<int> divisionIds, string loginUser, DBContext db)
        {
            // Process each division ID in the list
            foreach (var divisionId in divisionIds)
            {
                SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
                SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

                List<SqlParameter> param = new List<SqlParameter>
        {
            returnVal,
            new SqlParameter("@DIVISION_ID", divisionId),
            new SqlParameter("@LOGIN_USER", loginUser),
            returnMsg
        };

                string query = "EXEC @RETURN_VAL = [dbo].[sp_DivisionMaster_Delete] @DIVISION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
                int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());
            }

            // Assuming the result should reflect the overall success or failure of the operation
            return new DBResult(true, "Delete operation completed successfully");
        }


        public IList<MSystem> SearchDivision(MSystem data, string loginUser, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SYSTEM_TYPE", CheckNullValue(data.SYSTEM_TYPE) ),
                new SqlParameter ( "@SYSTEM_CODE", CheckNullValue(data.SYSTEM_CODE) ),
                new SqlParameter ( "@SYSTEM_VALUE", CheckNullValue(data.SYSTEM_VALUE) ),
                new SqlParameter ( "@SYSTEM_CODE_VALUE", CheckNullValue(data.SYSTEM_CODE_VALUE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@USERNAME", CheckNullValue(loginUser) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Division_SearchByUser] @SYSTEM_TYPE, @SYSTEM_CODE, @SYSTEM_VALUE, @SYSTEM_CODE_VALUE, @STATUS, @USERNAME, @PageNumber, @PageSize";
            IList<MSystem> Result = db.MSystem.FromSqlRaw<MSystem>(query, param.ToArray()).ToList();

            return Result;
        }

    }
}
