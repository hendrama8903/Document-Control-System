using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class UserDashboardRepo : BaseRepo
    {
        private UserDashboardRepo() { }

        #region Singleton
        private static UserDashboardRepo instance = null;
        public static UserDashboardRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new UserDashboardRepo();
                }
                return instance;
            }
        }
        #endregion


        public IList<UserDashboardDocument> Search(DocumentControlMaintenance data, string loginUser, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ("@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID)),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@YEAR", CheckNullValue(data.DOCUMENT_YEAR) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_UserDashboard_Search] @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DOCUMENT_NAME, @DIVISION, " +
                "@DEPARTMENT_ID, @DEPARTMENT_CODE, @YEAR, @LOGIN_USER, @PageNumber, @PageSize";
            IList<UserDashboardDocument> Result = db.UserDashboardSearch.FromSqlRaw<UserDashboardDocument>(query, param.ToArray()).ToList();


            return Result;
        }

        public UserDashboardRepo GetByKey(DepartmentMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) )
            };

            string query = "EXEC [dbo].[sp_DepartmentMaster_GetByKey] @DEPARTMENT_ID";
            UserDashboardRepo Result = null;// db.DepartmentMaster.FromSqlRaw<UserDashboardRepo>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DocumentControlMaintenance GenerateDocumentNo(DBContext db)
        {

            string query = "EXEC [dbo].[sp_UserDashboard_GenerateDocumentNo]";
            //String Result = db.UserDashboard.FromSqlRaw<UserDashboard>(query).ToString();
            DocumentControlMaintenance Result = db.UserDashboard.FromSqlRaw<DocumentControlMaintenance>(query).AsEnumerable().FirstOrDefault(); ;

            return Result;
        }

        public DocumentMaintenance GetDataByDocumentNo(DocumentMaintenance data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@YEAR", CheckNullValue(data.DOCUMENT_YEAR) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(1) ),
                new SqlParameter ( "@PageSize", CheckNullValue(100) )
            };

            string query = "EXEC [dbo].[sp_DocumentMaintenance_Search] @DOCUMENT_CODE, @DEPARTMENT_CODE, @YEAR, @PageNumber, @PageSize";
            //String Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query).ToString();
            DocumentMaintenance Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query, param.ToArray()).AsEnumerable().FirstOrDefault(); ;

            return Result;
        }

        public DocumentControlMaintenance GetByKey(DocumentControlMaintenance data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@YEAR", CheckNullValue(data.DOCUMENT_YEAR) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(1) ),
                new SqlParameter ( "@PageSize", CheckNullValue(100) )
            };

            string query = "EXEC [dbo].[sp_UserDashboard_Search] @DOCUMENT_TRANSACTION_ID, @DIVISION, @DEPARTMENT_CODE, @YEAR, @PageNumber, @PageSize";
            //String Result = db.UserDashboard.FromSqlRaw<UserDashboard>(query).ToString();
            DocumentControlMaintenance Result = db.UserDashboard.FromSqlRaw<DocumentControlMaintenance>(query, param.ToArray()).AsEnumerable().FirstOrDefault(); ;

            return Result;
        }

        public DocumentMaster GetLevelByDocumentCode(DBContext db, String documentCode)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(documentCode) )
            };

            string query = "EXEC [dbo].[sp_UserDashboard_GetLevelByDocumentCode]";
            //String Result = db.UserDashboard.FromSqlRaw<UserDashboard>(query).ToString();
            DocumentMaster Result = db.DocumentMaster.FromSqlRaw<DocumentMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(DocumentControlMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");
            /*
             @DOCUMENT_CODE varchar(5),
		    @DOCUMENT_NAME varchar(255),
		    @DOCUMENT_TYPE varchar(50),
		    @PROCESS_CODE varchar(50),
		    @COMPANY_CODE varchar(50),
		    @SECTION_CODE varchar(50),
		    @ITEM_CHANGED varchar(255),
		    @REASON varchar(255),
		    @EXTERNAL_FLAG varchar(1),
		    @REFERENCE_NO varchar(255),
		    @SOURCE varchar(255),
		    @DOCUMENT_DATE datetime,
		    @FILE_PATH varchar(255),
		    @STATUS varchar(1),
		    @REVISION int,
		    @APPROVAL_ID int,
		    @DELETE_FLAG int,
		    @CREATED_BY varchar(50),
		    @CHANGED_BY varchar(50)
             */
            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ("@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID)),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                new SqlParameter ( "@CHANGED_BY", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_UserDashboard_Request] @DOCUMENT_TRANSACTION_ID,@CREATED_BY,@CHANGED_BY, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult Update(DocumentControlMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CTRL_ID", CheckNullValue(data.DOCUMENT_CTRL_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@LOCATION_DOC", CheckNullValue(data.LOCATION) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                new SqlParameter ( "@CHANGED_BY", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_UserDashboard_Update] @DOCUMENT_CTRL_ID,@DOCUMENT_CODE,@LOCATION_DOC,@CREATED_BY,@CHANGED_BY, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(DocumentControlMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CTRL_ID", CheckNullValue(data.DOCUMENT_CTRL_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_UserDashboard_Delete] @DOCUMENT_CTRL_ID, @DOCUMENT_CODE, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult ApproveReject(DocumentControlMaintenance data, string loginUser, DBContext db)
        {
            /*
             @DOCUMENT_ID int,
		@DOCUMENT_CODE varchar(5),
		@IS_APPROVED varchar(1),
		@REMARKS varchar(200),
		@CREATED_BY varchar(50),
		@CHANGED_BY varchar(50),
             */
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CTRL_ID", CheckNullValue(data.DOCUMENT_CTRL_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@IS_APPROVED", CheckNullValue(data.IS_APPROVED) ),
                new SqlParameter ( "@REMARKS", CheckNullValue(data.REMARKS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_UserDashboard_ApproveReject] @DOCUMENT_CTRL_ID, @DOCUMENT_CODE, @IS_APPROVED, @REMARKS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Assigns (or clears, when folderId is null) one document's PERSONAL folder
        // for loginUser - request Hendra 2026-08-29. Independent of Document
        // Control's global TB_R_DOCUMENT.FOLDER_ID - see
        // TB_R_DOCUMENT_FOLDER_PERSONAL / sp_UserDashboard_AssignFolder.
        public DBResult AssignFolder(int documentTransactionId, int? folderId, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", documentTransactionId ),
                new SqlParameter ( "@USERNAME", loginUser ),
                new SqlParameter ( "@FOLDER_ID", CheckNullValue(folderId) ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_UserDashboard_AssignFolder] @DOCUMENT_TRANSACTION_ID, @USERNAME, @FOLDER_ID, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
