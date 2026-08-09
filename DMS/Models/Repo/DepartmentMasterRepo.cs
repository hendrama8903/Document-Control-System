using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DepartmentMasterRepo : BaseRepo
    {
        private DepartmentMasterRepo() { }

        #region Singleton
        private static DepartmentMasterRepo instance = null;
        public static DepartmentMasterRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DepartmentMasterRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<DepartmentMaster> Search(DepartmentMaster data, DBContext db, int? PageNumber, int? PageSize, bool showAll = false)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@DEPARTMENT_NAME", CheckNullValue(data.DEPARTMENT_NAME) ),
                new SqlParameter ( "@DEPARTMENT_CODE_NAME", CheckNullValue(data.DEPARTMENT_CODE_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DOCUMENT_CONTROL_ACCESS", CheckNullValue(data.DOCUMENT_CONTROL_ACCESS) ),
                new SqlParameter ("@IS_VALID_ONLY", CheckNullValue(data.IS_VALID_ONLY)),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) ),
                new SqlParameter ( "@SHOW_DELETED", "0" ),
                new SqlParameter ( "@SHOW_ALL", showAll ? "1" : "0" )
            };

            string query = "EXEC [dbo].[sp_DepartmentMaster_Search] @DEPARTMENT_CODE, @DEPARTMENT_NAME, @DEPARTMENT_CODE_NAME, @DIVISION, @DOCUMENT_CONTROL_ACCESS,@IS_VALID_ONLY, @PageNumber, @PageSize, @SHOW_DELETED, @SHOW_ALL";
            IList<DepartmentMaster> Result = db.DepartmentMaster.FromSqlRaw<DepartmentMaster>(query, param.ToArray()).ToList();

            return Result;
        }


        public DepartmentMaster GetByKey(DepartmentMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DOCUMENT_CONTROL_ACCESS", CheckNullValue(data.DOCUMENT_CONTROL_ACCESS) ),
            };

            string query = "EXEC [dbo].[sp_DepartmentMaster_GetByKey] @DEPARTMENT_ID, @DIVISION, @DOCUMENT_CONTROL_ACCESS";
            DepartmentMaster Result = db.DepartmentMaster.FromSqlRaw<DepartmentMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(DepartmentMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@DEPARTMENT_NAME", CheckNullValue(data.DEPARTMENT_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                //new SqlParameter ( "@WORKFLOW_ID", CheckNullValue(data.WORKFLOW_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DepartmentMaster_Insert] @DEPARTMENT_CODE, @DEPARTMENT_NAME, @DIVISION, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(DepartmentMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@DEPARTMENT_NAME", CheckNullValue(data.DEPARTMENT_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                //new SqlParameter ( "@WORKFLOW_ID", CheckNullValue(data.WORKFLOW_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DepartmentMaster_Update] @DEPARTMENT_ID, @DEPARTMENT_CODE, @DEPARTMENT_NAME, @DIVISION, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(DepartmentMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DepartmentMaster_Delete] @DEPARTMENT_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<Select2DivisionUser> SearchDivision(Select2DivisionUser data, string loginUser, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DIVISION_CODE", CheckNullValue(data.DIVISION_CODE) ),
                new SqlParameter ( "@DIVISION_NAME", CheckNullValue(data.DIVISION_NAME) ),
                new SqlParameter ( "@DIVISION_CODE_NAME", CheckNullValue(data.DIVISION_CODE_NAME) ),
                new SqlParameter ( "@USERNAME", CheckNullValue(loginUser) ),
                new SqlParameter("@PageNumber", PageNumber ?? 1),
                new SqlParameter("@PageSize", PageSize ?? 999999)
                //new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                //new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Division_SearchByUser] @DIVISION_CODE, @DIVISION_NAME, @DIVISION_CODE_NAME, @USERNAME, @PageNumber, @PageSize";
            IList<Select2DivisionUser> Result = db.Select2DivisionUser.FromSqlRaw<Select2DivisionUser>(query, param.ToArray()).ToList();

            return Result;
        }

    }
}
