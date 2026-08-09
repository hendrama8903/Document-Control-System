using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DocumentSubmissionRepo : BaseRepo
    {
        private DocumentSubmissionRepo() { }

        #region Singleton
        private static DocumentSubmissionRepo instance = null;
        public static DocumentSubmissionRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentSubmissionRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<DocumentSubmission> Search(DocumentSubmission data, string division, int? departmentId, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SUBMISSION_NO", CheckNullValue(data.SUBMISSION_NO) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(division) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(departmentId) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentSubmission_Search] @SUBMISSION_NO, @STATUS, @DIVISION, @DEPARTMENT_ID, @PageNumber, @PageSize";
            IList<DocumentSubmission> Result = db.DocumentSubmission.FromSqlRaw<DocumentSubmission>(query, param.ToArray()).ToList();

            return Result;
        }

        public DocumentSubmission GetByKey(DocumentSubmission data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SUBMISSION_ID", CheckNullValue(data.SUBMISSION_ID) )
            };

            string query = "EXEC [dbo].[sp_DocumentSubmission_GetByKey] @SUBMISSION_ID";
            DocumentSubmission Result = db.DocumentSubmission.FromSqlRaw<DocumentSubmission>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(DocumentSubmission data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@SUBMISSION_DATE", CheckNullValue(data.SUBMISSION_DATE) ),
                new SqlParameter ( "@DOC_CATEGORY", CheckNullValue(data.DOC_CATEGORY) ),
                new SqlParameter ( "@REMARK", CheckNullValue(data.REMARK) ),
                new SqlParameter ( "@DOCUMENT_CREATOR", CheckNullValue(data.DOCUMENT_CREATOR) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_Insert] @DIVISION, @DEPARTMENT_ID, @SECTION_CODE, @SUBMISSION_DATE, @DOC_CATEGORY, " +
                "@REMARK, @DOCUMENT_CREATOR, @LOGIN_USER, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult Update(DocumentSubmission data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SUBMISSION_ID", CheckNullValue(data.SUBMISSION_ID) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@SUBMISSION_DATE", CheckNullValue(data.SUBMISSION_DATE) ),
                new SqlParameter ( "@DOC_CATEGORY", CheckNullValue(data.DOC_CATEGORY) ),
                new SqlParameter ( "@REMARK", CheckNullValue(data.REMARK) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_Update] @SUBMISSION_ID, @SECTION_CODE, @SUBMISSION_DATE, @DOC_CATEGORY, @REMARK, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(DocumentSubmission data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SUBMISSION_ID", CheckNullValue(data.SUBMISSION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_Delete] @SUBMISSION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UpdateStatus(DocumentSubmission data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SUBMISSION_ID", CheckNullValue(data.SUBMISSION_ID) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_UpdateStatus] @SUBMISSION_ID, @STATUS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Submit(int submissionId, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SUBMISSION_ID", submissionId ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_Submit] @SUBMISSION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<DocumentSubmissionDetail> SearchDetail(DocumentSubmissionDetail data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SUBMISSION_ID", CheckNullValue(data.SUBMISSION_ID) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentSubmission_SearchDetail] @SUBMISSION_ID, @PageNumber, @PageSize";
            IList<DocumentSubmissionDetail> Result = db.DocumentSubmissionDetail.FromSqlRaw<DocumentSubmissionDetail>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult InsertDetail(DocumentSubmissionDetail data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SUBMISSION_ID", CheckNullValue(data.SUBMISSION_ID) ),
                new SqlParameter ( "@DOCUMENT_NO", CheckNullValue(data.DOCUMENT_NO) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@CLASSIFICATION", CheckNullValue(data.CLASSIFICATION) ),
                new SqlParameter ( "@SUBMISSION_TYPE", CheckNullValue(data.SUBMISSION_TYPE) ),
                new SqlParameter ( "@REVISION_NO", CheckNullValue(data.REVISION_NO) ),
                new SqlParameter ( "@ITEM_CHANGED", CheckNullValue(data.ITEM_CHANGED) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@EFFECTIVE_DATE", CheckNullValue(data.EFFECTIVE_DATE) ),
                new SqlParameter ( "@COPY_TYPE", CheckNullValue(data.COPY_TYPE) ),
                new SqlParameter ( "@COPY_QTY", CheckNullValue(data.COPY_QTY) ),
                new SqlParameter ( "@DISTRIBUTION_DEPT_IDS", CheckNullValue(data.DISTRIBUTION_DEPT_IDS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_InsertDetail] @SUBMISSION_ID, @DOCUMENT_NO, @DOCUMENT_NAME, @CLASSIFICATION, @SUBMISSION_TYPE, @REVISION_NO, " +
                "@ITEM_CHANGED, @REASON, @EFFECTIVE_DATE, @COPY_TYPE, @COPY_QTY, @DISTRIBUTION_DEPT_IDS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UpdateDetail(DocumentSubmissionDetail data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SUBMISSION_DETAIL_ID", CheckNullValue(data.SUBMISSION_DETAIL_ID) ),
                new SqlParameter ( "@DOCUMENT_NO", CheckNullValue(data.DOCUMENT_NO) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@CLASSIFICATION", CheckNullValue(data.CLASSIFICATION) ),
                new SqlParameter ( "@SUBMISSION_TYPE", CheckNullValue(data.SUBMISSION_TYPE) ),
                new SqlParameter ( "@REVISION_NO", CheckNullValue(data.REVISION_NO) ),
                new SqlParameter ( "@ITEM_CHANGED", CheckNullValue(data.ITEM_CHANGED) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@EFFECTIVE_DATE", CheckNullValue(data.EFFECTIVE_DATE) ),
                new SqlParameter ( "@COPY_TYPE", CheckNullValue(data.COPY_TYPE) ),
                new SqlParameter ( "@COPY_QTY", CheckNullValue(data.COPY_QTY) ),
                new SqlParameter ( "@DISTRIBUTION_DEPT_IDS", CheckNullValue(data.DISTRIBUTION_DEPT_IDS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_UpdateDetail] @SUBMISSION_DETAIL_ID, @DOCUMENT_NO, @DOCUMENT_NAME, @CLASSIFICATION, @SUBMISSION_TYPE, @REVISION_NO, " +
                "@ITEM_CHANGED, @REASON, @EFFECTIVE_DATE, @COPY_TYPE, @COPY_QTY, @DISTRIBUTION_DEPT_IDS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult DeleteDetail(DocumentSubmissionDetail data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SUBMISSION_DETAIL_ID", CheckNullValue(data.SUBMISSION_DETAIL_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentSubmission_DeleteDetail] @SUBMISSION_DETAIL_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
