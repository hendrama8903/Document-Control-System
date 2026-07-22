using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class ExternalDocumentRepo : BaseRepo
    {
        private ExternalDocumentRepo() { }

        #region Singleton
        private static ExternalDocumentRepo instance = null;
        public static ExternalDocumentRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ExternalDocumentRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<ExternalDocument> Search(ExternalDocument data, string division, int? departmentId, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@EXTERNAL_TYPE", CheckNullValue(data.EXTERNAL_TYPE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(division) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(departmentId) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_ExternalDocument_Search] @DOCUMENT_NAME, @EXTERNAL_TYPE, @STATUS, @DIVISION, @DEPARTMENT_ID, @PageNumber, @PageSize";
            IList<ExternalDocument> Result = db.ExternalDocument.FromSqlRaw<ExternalDocument>(query, param.ToArray()).ToList();

            return Result;
        }

        public ExternalDocument GetByKey(ExternalDocument data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@EXTERNAL_DOCUMENT_ID", CheckNullValue(data.EXTERNAL_DOCUMENT_ID) )
            };

            string query = "EXEC [dbo].[sp_ExternalDocument_GetByKey] @EXTERNAL_DOCUMENT_ID";
            ExternalDocument Result = db.ExternalDocument.FromSqlRaw<ExternalDocument>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(ExternalDocument data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@REVISION_EDITION", CheckNullValue(data.REVISION_EDITION) ),
                new SqlParameter ( "@RECEIVED_DATE", CheckNullValue(data.RECEIVED_DATE) ),
                new SqlParameter ( "@EXTERNAL_TYPE", CheckNullValue(data.EXTERNAL_TYPE) ),
                new SqlParameter ( "@STORAGE_LOCATION", CheckNullValue(data.STORAGE_LOCATION) ),
                new SqlParameter ( "@STORAGE_PIC", CheckNullValue(data.STORAGE_PIC) ),
                new SqlParameter ( "@REVIEW_FREQUENCY", CheckNullValue(data.REVIEW_FREQUENCY) ),
                new SqlParameter ( "@REVIEW_SOURCE", CheckNullValue(data.REVIEW_SOURCE) ),
                new SqlParameter ( "@LAST_CHECK_DATE", CheckNullValue(data.LAST_CHECK_DATE) ),
                new SqlParameter ( "@REVIEW_CYCLE_MONTHS", CheckNullValue(data.REVIEW_CYCLE_MONTHS) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@INTERNAL_IMPACT", CheckNullValue(data.INTERNAL_IMPACT) ),
                new SqlParameter ( "@IMPACTED_DOCUMENT_TRANSACTION_ID", CheckNullValue(data.IMPACTED_DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_ExternalDocument_Insert] @DOCUMENT_NAME, @REVISION_EDITION, @RECEIVED_DATE, @EXTERNAL_TYPE, " +
                "@STORAGE_LOCATION, @STORAGE_PIC, @REVIEW_FREQUENCY, @REVIEW_SOURCE, @LAST_CHECK_DATE, @REVIEW_CYCLE_MONTHS, " +
                "@STATUS, @INTERNAL_IMPACT, @IMPACTED_DOCUMENT_TRANSACTION_ID, @DIVISION, @DEPARTMENT_ID, @FILE_PATH, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(ExternalDocument data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@EXTERNAL_DOCUMENT_ID", CheckNullValue(data.EXTERNAL_DOCUMENT_ID) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@REVISION_EDITION", CheckNullValue(data.REVISION_EDITION) ),
                new SqlParameter ( "@RECEIVED_DATE", CheckNullValue(data.RECEIVED_DATE) ),
                new SqlParameter ( "@EXTERNAL_TYPE", CheckNullValue(data.EXTERNAL_TYPE) ),
                new SqlParameter ( "@STORAGE_LOCATION", CheckNullValue(data.STORAGE_LOCATION) ),
                new SqlParameter ( "@STORAGE_PIC", CheckNullValue(data.STORAGE_PIC) ),
                new SqlParameter ( "@REVIEW_FREQUENCY", CheckNullValue(data.REVIEW_FREQUENCY) ),
                new SqlParameter ( "@REVIEW_SOURCE", CheckNullValue(data.REVIEW_SOURCE) ),
                new SqlParameter ( "@LAST_CHECK_DATE", CheckNullValue(data.LAST_CHECK_DATE) ),
                new SqlParameter ( "@REVIEW_CYCLE_MONTHS", CheckNullValue(data.REVIEW_CYCLE_MONTHS) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@INTERNAL_IMPACT", CheckNullValue(data.INTERNAL_IMPACT) ),
                new SqlParameter ( "@IMPACTED_DOCUMENT_TRANSACTION_ID", CheckNullValue(data.IMPACTED_DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_ExternalDocument_Update] @EXTERNAL_DOCUMENT_ID, @DOCUMENT_NAME, @REVISION_EDITION, @RECEIVED_DATE, @EXTERNAL_TYPE, " +
                "@STORAGE_LOCATION, @STORAGE_PIC, @REVIEW_FREQUENCY, @REVIEW_SOURCE, @LAST_CHECK_DATE, @REVIEW_CYCLE_MONTHS, " +
                "@STATUS, @INTERNAL_IMPACT, @IMPACTED_DOCUMENT_TRANSACTION_ID, @DIVISION, @DEPARTMENT_ID, @FILE_PATH, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(ExternalDocument data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@EXTERNAL_DOCUMENT_ID", CheckNullValue(data.EXTERNAL_DOCUMENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_ExternalDocument_Delete] @EXTERNAL_DOCUMENT_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult RemoveAttachment(ExternalDocument data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@EXTERNAL_DOCUMENT_ID", CheckNullValue(data.EXTERNAL_DOCUMENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_ExternalDocument_RemoveAttachment] @EXTERNAL_DOCUMENT_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<ExternalDocument> GetDueForReview(int daysAhead, int reminderThrottleDays, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DAYS_AHEAD", daysAhead ),
                new SqlParameter ( "@REMINDER_THROTTLE_DAYS", reminderThrottleDays )
            };

            string query = "EXEC [dbo].[sp_ExternalDocument_GetDueForReview] @DAYS_AHEAD, @REMINDER_THROTTLE_DAYS";
            IList<ExternalDocument> Result = db.ExternalDocument.FromSqlRaw<ExternalDocument>(query, param.ToArray()).ToList();

            return Result;
        }

        public void MarkReviewReminded(int externalDocumentId, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@EXTERNAL_DOCUMENT_ID", externalDocumentId )
            };

            string query = "EXEC [dbo].[sp_ExternalDocument_MarkReviewReminded] @EXTERNAL_DOCUMENT_ID";
            db.Database.ExecuteSqlRaw(query, param.ToArray());
        }
    }
}
