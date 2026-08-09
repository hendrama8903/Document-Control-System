using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class SectionMasterRepo : BaseRepo
    {
        private SectionMasterRepo() { }

        #region Singleton
        private static SectionMasterRepo instance = null;
        public static SectionMasterRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SectionMasterRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<SectionMaster> Search(SectionMaster data, DBContext db, int? PageNumber, int? PageSize, bool showAll = false)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@SECTION_NAME", CheckNullValue(data.SECTION_NAME) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@IS_VALID_ONLY", CheckNullValue(data.IS_VALID_ONLY) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) ),
                new SqlParameter ( "@SHOW_DELETED", "0" ),
                new SqlParameter ( "@SHOW_ALL", showAll ? "1" : "0" )
            };

            string query = "EXEC [dbo].[sp_SectionMaster_Search] @SECTION_CODE, @SECTION_NAME, @DEPARTMENT_CODE, @DEPARTMENT_ID, @IS_VALID_ONLY, @PageNumber, @PageSize, @SHOW_DELETED, @SHOW_ALL";
            IList<SectionMaster> Result = db.SectionMaster.FromSqlRaw<SectionMaster>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<Select2SectionCode> ListSectionCode(Select2SectionCode data, string DepartmentCode, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(DepartmentCode) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_SectionMaster_ListSectionCode] @DEPARTMENT_CODE, @SECTION_CODE, @PageNumber, @PageSize";
            IList<Select2SectionCode> Result = db.Select2SectionCode.FromSqlRaw<Select2SectionCode>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<Select2SectionName> ListSectionName(Select2SectionName data, string DepartmentCode, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(DepartmentCode) ),
                new SqlParameter ( "@SECTION_NAME", CheckNullValue(data.SECTION_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_SectionMaster_ListSectionName] @DEPARTMENT_CODE, @SECTION_NAME, @PageNumber, @PageSize";
            IList<Select2SectionName> Result = db.Select2SectionName.FromSqlRaw<Select2SectionName>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<Select2SectionCodeAndName> ListSectionCodeAndName(Select2SectionCodeAndName data, int? departmentId, string DepartmentCode, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(departmentId) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(DepartmentCode) ),
                new SqlParameter ( "@SECTION_NAME", CheckNullValue(data.SECTION_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_SectionMaster_ListSectionCodeAndName] @DEPARTMENT_ID, @DEPARTMENT_CODE, @SECTION_NAME, @PageNumber, @PageSize";
            IList<Select2SectionCodeAndName> Result = db.Select2SectionCodeAndName.FromSqlRaw<Select2SectionCodeAndName>(query, param.ToArray()).ToList();

            return Result;
        }

        public SectionMaster GetByKey(SectionMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SECTION_ID", CheckNullValue(data.SECTION_ID) )
            };

            string query = "EXEC [dbo].[sp_SectionMaster_GetByKey] @SECTION_ID";
            SectionMaster Result = db.SectionMaster.FromSqlRaw<SectionMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(SectionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@SECTION_NAME", CheckNullValue(data.SECTION_NAME) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@VALID_FROM", CheckNullValue(data.VALID_FROM) ),
                new SqlParameter ( "@VALID_TO", CheckNullValue(data.VALID_TO) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_SectionMaster_Insert] @SECTION_CODE, @SECTION_NAME,@DEPARTMENT_CODE," +
                "@VALID_FROM, @VALID_TO, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(SectionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SECTION_ID", CheckNullValue(data.SECTION_ID) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@SECTION_NAME", CheckNullValue(data.SECTION_NAME) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@VALID_FROM", CheckNullValue(data.VALID_FROM) ),
                new SqlParameter ( "@VALID_TO", CheckNullValue(data.VALID_TO) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_SectionMaster_Update] @SECTION_ID, @SECTION_CODE, @SECTION_NAME, " +
                "@DEPARTMENT_CODE, @VALID_FROM, @VALID_TO, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(SectionMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SECTION_ID", CheckNullValue(data.SECTION_ID) ),
                new SqlParameter ( "@DELETE_FLAG", CheckNullValue(data.DELETE_FLAG) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_SectionMaster_Delete] @SECTION_ID,@DELETE_FLAG, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
