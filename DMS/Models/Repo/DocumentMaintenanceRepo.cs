using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
//Recommit DINA 2

namespace DMS.Models.Repo
{  
    public class DocumentMaintenanceRepo : BaseRepo
    {
        private DocumentMaintenanceRepo() { }

        #region Singleton
        private static DocumentMaintenanceRepo instance = null;
        public static DocumentMaintenanceRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentMaintenanceRepo();
                }
                return instance;
            }
        }
        #endregion


        public IList<DocumentMaintenance> Search(DocumentMaintenance data, string loginUser, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION)),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID)),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE).ToString() ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@YEAR", CheckNullValue(data.DOCUMENT_YEAR) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@NOT_EXIST_FLAG", CheckNullValue(data.NOT_EXIST_FLAG) ),
                new SqlParameter ( "@REVISION_ALLOWAL_FLAG", CheckNullValue(data.REVISION_ALLOWAL_FLAG) ),
                new SqlParameter ( "@EXCLUDE_MIGRATION_FLAG", CheckNullValue(data.EXCLUDE_MIGRATION_FLAG) ),
                new SqlParameter ( "@USERNAME", CheckNullValue(loginUser) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentMaintenance_Search] @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DIVISION, @DEPARTMENT_ID, @DEPARTMENT_CODE, @DOCUMENT_TRANSACTION_NAME, @YEAR, @STATUS, " +
                "@NOT_EXIST_FLAG, @REVISION_ALLOWAL_FLAG, @EXCLUDE_MIGRATION_FLAG, @USERNAME, @PageNumber, @PageSize";
            IList<DocumentMaintenance> Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query, param.ToArray()).ToList();
            return Result;
        }

        public IList<DocumentMaintenance> GetDueForReview(int daysAhead, int reminderThrottleDays, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DAYS_AHEAD", daysAhead ),
                new SqlParameter ( "@REMINDER_THROTTLE_DAYS", reminderThrottleDays )
            };

            string query = "EXEC [dbo].[sp_DocumentMaintenance_GetDueForReview] @DAYS_AHEAD, @REMINDER_THROTTLE_DAYS";
            IList<DocumentMaintenance> Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query, param.ToArray()).ToList();
            return Result;
        }

        public DBResult ImportLegacyInsert(DocumentMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) ),
                new SqlParameter ( "@LEVEL_CODE", CheckNullValue(data.LEVEL_CODE) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@CLASSIFIED", CheckNullValue(data.CLASSIFIED) ),
                new SqlParameter ( "@REVISION", CheckNullValue(data.REVISION) ),
                new SqlParameter ( "@DOCUMENT_DATE", CheckNullValue(data.DOCUMENT_DATE) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@DOCUMENT_CREATOR", CheckNullValue(data.DOCUMENT_CREATOR) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_ImportLegacyInsert] @DOCUMENT_CODE, @DOCUMENT_TRANSACTION_NAME, @DOCUMENT_ID, @LEVEL_CODE, " +
                "@DIVISION, @DEPARTMENT, @CLASSIFIED, @REVISION, @DOCUMENT_DATE, @FILE_PATH, @DOCUMENT_CREATOR, @REASON, @CREATED_BY, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult ImportLegacyHistoryInsert(DocumentMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) ),
                new SqlParameter ( "@LEVEL_CODE", CheckNullValue(data.LEVEL_CODE) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@CLASSIFIED", CheckNullValue(data.CLASSIFIED) ),
                new SqlParameter ( "@REVISION", CheckNullValue(data.REVISION) ),
                new SqlParameter ( "@DOCUMENT_DATE", CheckNullValue(data.DOCUMENT_DATE) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@DOCUMENT_CREATOR", CheckNullValue(data.DOCUMENT_CREATOR) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_ImportLegacyHistoryInsert] @DOCUMENT_CODE, @DOCUMENT_TRANSACTION_NAME, @DOCUMENT_ID, @LEVEL_CODE, " +
                "@DIVISION, @DEPARTMENT, @CLASSIFIED, @REVISION, @DOCUMENT_DATE, @FILE_PATH, @DOCUMENT_CREATOR, @REASON, @CREATED_BY, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public void MarkReviewReminded(int documentTransactionId, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", documentTransactionId )
            };

            string query = "EXEC [dbo].[sp_DocumentMaintenance_MarkReviewReminded] @DOCUMENT_TRANSACTION_ID";
            db.Database.ExecuteSqlRaw(query, param.ToArray());
        }

        public IList<DocumentHistory> SearchHistory(DocumentHistory data, string loginUser, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION)),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID)),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE).ToString() ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentHistory_Search] @DOCUMENT_TRANSACTION_ID, @DOCUMENT_TRANSACTION_NAME, @DOCUMENT_CODE, @DIVISION, @DEPARTMENT_ID, @DEPARTMENT_CODE, " +
                "@PageNumber, @PageSize";
            IList<DocumentHistory> Result = db.DocumentHistory.FromSqlRaw<DocumentHistory>(query, param.ToArray()).ToList();


            return Result;
        }

        public IList<DocumentMaintenance> SearchHistoryToMaintenance(DocumentMaintenance data, string loginUser, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION)),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID)),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE).ToString() ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentHistory_Search] @DOCUMENT_TRANSACTION_ID, @DOCUMENT_TRANSACTION_NAME, @DOCUMENT_CODE, @DIVISION, @DEPARTMENT_ID, @DEPARTMENT_CODE, " +
                "@PageNumber, @PageSize";
            IList<DocumentMaintenance> Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query, param.ToArray()).ToList();


            return Result;
        }

        public IList<DocumentReport> SearchReport(DocumentReport data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE)),
                new SqlParameter ( "@YEAR", CheckNullValue(data.DOCUMENT_YEAR)),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentMaintenance_SearchReport] @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DOCUMENT_TRANSACTION_NAME, @DIVISION, @DEPARTMENT_ID, @DEPARTMENT_CODE, " +
                "@YEAR, @PageNumber, @PageSize";
            IList<DocumentReport> Result = db.DocumentReport.FromSqlRaw<DocumentReport>(query, param.ToArray()).ToList();


            return Result;
        }

        //Document Masterlist (ISO document register) - satu baris per dokumen aktif/current
        //dari TB_R_DOCUMENT (tabel ini cuma pernah punya 1 baris per DOCUMENT_CODE, lihat
        //sp_DocumentMaintenance_SupersedeRevision). Tanpa parameter - semua filter di grid.
        public IList<DocumentMasterlist> GetMasterlist(DBContext db)
        {
            string query = "EXEC [dbo].[sp_DocumentMaintenance_Masterlist]";
            IList<DocumentMasterlist> Result = db.DocumentMasterlist.FromSqlRaw<DocumentMasterlist>(query).ToList();

            return Result;
        }

        public DocumentMaintenanceRepo GetByKey(DepartmentMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) )
            };

            string query = "EXEC [dbo].[sp_DepartmentMaster_GetByKey] @DEPARTMENT_ID";
            DocumentMaintenanceRepo Result = null;// db.DepartmentMaster.FromSqlRaw<DocumentMaintenanceRepo>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DocumentMaintenance GenerateDocumentNo(DBContext db)
        {

            string query = "EXEC [dbo].[sp_DocumentMaintenance_GenerateDocumentNo]";
            //String Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query).ToString();
            DocumentMaintenance Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query).AsEnumerable().FirstOrDefault(); ;

            return Result;
        }

        public string GetManualNoPrefix(int? levelCode, string division, int? departmentId, string sectionCode,
            int? documentId, string processCode, string companyCode, DateTime? documentDate, DBContext db)
        {
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) connection.Open();

            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "[dbo].[sp_DocumentMaintenance_GetManualNoPrefix]";
                    command.CommandType = System.Data.CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@LEVEL_CODE", CheckNullValue(levelCode)));
                    command.Parameters.Add(new SqlParameter("@DIVISION", CheckNullValue(division)));
                    command.Parameters.Add(new SqlParameter("@DEPARTMENT_ID", CheckNullValue(departmentId)));
                    command.Parameters.Add(new SqlParameter("@SECTION_CODE", CheckNullValue(sectionCode)));
                    command.Parameters.Add(new SqlParameter("@DOCUMENT_ID", CheckNullValue(documentId)));
                    command.Parameters.Add(new SqlParameter("@PROCESS_CODE", CheckNullValue(processCode)));
                    command.Parameters.Add(new SqlParameter("@COMPANY_CODE", CheckNullValue(companyCode)));
                    command.Parameters.Add(new SqlParameter("@DOCUMENT_DATE", CheckNullValue(documentDate)));

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read() && !reader.IsDBNull(reader.GetOrdinal("PREFIX")))
                        {
                            return reader.GetString(reader.GetOrdinal("PREFIX"));
                        }
                    }
                }

                return null;
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }

        public DocumentMaster GetLevelByDocumentCode(DBContext db, String documentCode)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(documentCode) )
            };

            string query = "EXEC [dbo].[sp_DocumentMaintenance_GetLevelByDocumentCode]";
            //String Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query).ToString();
            DocumentMaster Result = db.DocumentMaster.FromSqlRaw<DocumentMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(DocumentMaintenance data, string loginUser, DBContext db)
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
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@DOCUMENT_TYPE", CheckNullValue(data.DOCUMENT_TYPE) ),
                new SqlParameter ( "@PROCESS_CODE", CheckNullValue(data.PROCESS_CODE) ),
                new SqlParameter ( "@COMPANY_CODE", CheckNullValue(data.COMPANY_CODE) ),
                //new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@ITEM_CHANGED", CheckNullValue(data.ITEM_CHANGED) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@EXTERNAL_FLAG", CheckNullValue(data.EXTERNAL_FLAG) ),
                new SqlParameter ( "@REFERENCE_NO", CheckNullValue(data.REFERENCE_NO) ),
                new SqlParameter ( "@SOURCE", CheckNullValue(data.SOURCE) ),
                new SqlParameter ( "@DOCUMENT_DATE", CheckNullValue(data.DOCUMENT_DATE) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@REVISION", CheckNullValue(data.REVISION) ),
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(data.APPROVAL_ID) ),
                new SqlParameter ( "@DELETE_FLAG", CheckNullValue(data.DELETE_FLAG) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@CLASSIFIED", CheckNullValue(data.CLASSIFIED) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@IMPACT_OTHER_FLAG", CheckNullValue(data.IMPACT_OTHER_FLAG) ),
                new SqlParameter ( "@LEVEL_CODE", CheckNullValue(data.LEVEL_CODE) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                new SqlParameter ( "@CHANGED_BY", loginUser ),
                new SqlParameter ( "@MENU_ID", "M00006-01" ),
                new SqlParameter ( "@DOCUMENT_CREATOR", CheckNullValue(data.DOCUMENT_CREATOR) ),
                new SqlParameter ( "@MANUAL_SEQ_NO", CheckNullValue(data.MANUAL_SEQ_NO) ),
                new SqlParameter ( "@RELATED_DIVISIONS", CheckNullValue(data.RELATED_DIVISIONS) ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_Insert] @DOCUMENT_TRANSACTION_ID, @DOCUMENT_ID,@DOCUMENT_CODE,@DOCUMENT_TRANSACTION_NAME,@DOCUMENT_TYPE," +
                "@PROCESS_CODE,@COMPANY_CODE,@SECTION_CODE,@ITEM_CHANGED,@REASON,@EXTERNAL_FLAG,@REFERENCE_NO,@SOURCE,@DOCUMENT_DATE,@FILE_PATH,@STATUS,@REVISION," +
                "@APPROVAL_ID,@DELETE_FLAG,@DIVISION,@CLASSIFIED,@DEPARTMENT_ID, @IMPACT_OTHER_FLAG, @LEVEL_CODE, @CREATED_BY,@CHANGED_BY, @MENU_ID, @DOCUMENT_CREATOR, @MANUAL_SEQ_NO, @RELATED_DIVISIONS, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult Update(DocumentMaintenance data, string loginUser, string remark, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_NAME", CheckNullValue(data.DOCUMENT_TRANSACTION_NAME) ),
                new SqlParameter ( "@DOCUMENT_TYPE", CheckNullValue(data.DOCUMENT_TYPE) ),
                new SqlParameter ( "@PROCESS_CODE", CheckNullValue(data.PROCESS_CODE) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@COMPANY_CODE", CheckNullValue(data.COMPANY_CODE) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@ITEM_CHANGED", CheckNullValue(data.ITEM_CHANGED) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@EXTERNAL_FLAG", CheckNullValue(data.EXTERNAL_FLAG) ),
                new SqlParameter ( "@REFERENCE_NO", CheckNullValue(data.REFERENCE_NO) ),
                new SqlParameter ( "@SOURCE", CheckNullValue(data.SOURCE) ),
                new SqlParameter ( "@DOCUMENT_DATE", CheckNullValue(data.DOCUMENT_DATE) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@REVISION", CheckNullValue(data.REVISION) ),
                new SqlParameter ( "@IMPACT_OTHER_FLAG", CheckNullValue(data.IMPACT_OTHER_FLAG) ),
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(data.APPROVAL_ID) ),
                new SqlParameter ( "@DELETE_FLAG", CheckNullValue(data.DELETE_FLAG) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                new SqlParameter ( "@CHANGED_BY", loginUser ),
                new SqlParameter ( "@REMARK", CheckNullValue(remark) ),
                new SqlParameter ( "@MENU_ID", "M00006-01" ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_Update] @DOCUMENT_TRANSACTION_ID,@DOCUMENT_CODE,@DOCUMENT_CODE,@DOCUMENT_TRANSACTION_NAME," +
                "@DOCUMENT_TYPE,@PROCESS_CODE,@COMPANY_CODE,@DEPARTMENT_CODE,@SECTION_CODE,@ITEM_CHANGED,@REASON,@EXTERNAL_FLAG,@REFERENCE_NO," +
                "@SOURCE,@DOCUMENT_DATE,@FILE_PATH,@STATUS,@REVISION,@IMPACT_OTHER_FLAG,@APPROVAL_ID,@DELETE_FLAG,@CREATED_BY,@CHANGED_BY, @REMARK, @MENU_ID, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(DocumentMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_Delete] @DOCUMENT_TRANSACTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<DocumentMaintenance> SearchDepartmentByDivision(DocumentMaintenance data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) )
            };

            string query = "EXEC [dbo].[sp_MDepartment_Search] @DIVISION";
            IList<DocumentMaintenance> Result = db.DocumentMaintenance.FromSqlRaw<DocumentMaintenance>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult UpdateStatus(DocumentMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_UpdateStatus] @DOCUMENT_TRANSACTION_ID, @STATUS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Related Division "Mengetahui" (SPR/SIPOCOR Level 2, request Hendra
        // 2026-08-20) - terpisah dari ApprovalRepo.Approve/Reject karena
        // sekarang tidak lagi bagian dari TB_R_APPROVAL_D (lihat
        // sp_WorkflowDoc_AppendRelatedDivision di _dropped/ utk alasannya).
        public IList<DocumentRelatedDivision> GetRelatedDivisionAckStatus(int documentTransactionId, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(documentTransactionId) )
            };

            string query = "EXEC [dbo].[sp_DocumentMaintenance_GetRelatedDivisionAckStatus] @DOCUMENT_TRANSACTION_ID";
            IList<DocumentRelatedDivision> result = db.DocumentRelatedDivision.FromSqlRaw<DocumentRelatedDivision>(query, param.ToArray()).ToList();

            return result;
        }

        public DBResult AcknowledgeRelatedDivision(int documentTransactionId, string divisionCode, string loginUser, DBContext db, out bool promotedToApproved)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter promoted = CreateSqlParameterOutputInt("@PROMOTED_TO_APPROVED");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(documentTransactionId) ),
                new SqlParameter ( "@DIVISION_CODE", CheckNullValue(divisionCode) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                promoted,
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_AcknowledgeRelatedDivision] @DOCUMENT_TRANSACTION_ID, @DIVISION_CODE, @LOGIN_USER, @PROMOTED_TO_APPROVED OUTPUT, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            promotedToApproved = promoted.Value != DBNull.Value && Convert.ToInt32(promoted.Value) == 1;

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Cuma dipakai buat menimbang STATUS "1" (Approved) vs "6" (Waiting
        // Acknowledgment) begitu approval asli selesai - lihat
        // DocumentMaintenanceController.ApproveRejectAsync.
        public int CountPendingRelatedDivisionAck(int documentTransactionId, DBContext db)
        {
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                if (cmd.Connection.State != System.Data.ConnectionState.Open) cmd.Connection.Open();
                // ApproveRejectAsync membungkus semua ini di db.Database.BeginTransaction() -
                // command ADO mentah WAJIB diikutkan ke transaksi EF yang sedang berjalan,
                // kalau tidak SQL Server menolak dengan "command has a transaction... pending
                // local transaction" (request Hendra 2026-08-20, ditemukan saat testing).
                if (db.Database.CurrentTransaction != null)
                {
                    cmd.Transaction = db.Database.CurrentTransaction.GetDbTransaction();
                }
                // Cuma peran RELATED yang wajib Acknowledge (request Hendra
                // 2026-08-20) - MAIN_PIC/NOTE_RELATED tidak difilter di sini akan
                // bikin dokumen tidak pernah naik ke Approved.
                cmd.CommandText = "SELECT COUNT(*) FROM [dbo].[TB_R_DOCUMENT_RELATED_DIVISION] WHERE DOCUMENT_TRANSACTION_ID = @id AND ACKNOWLEDGED_FLAG = 0 AND DIVISION_ROLE = 'RELATED'";
                var idParam = cmd.CreateParameter();
                idParam.ParameterName = "@id";
                idParam.Value = documentTransactionId;
                cmd.Parameters.Add(idParam);
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        //Obsolete-control (Jul 2026): dipanggil saat revisi baru suatu dokumen selesai
        //disetujui (langkah approval terakhir). Mengarsipkan revisi SEBELUMNYA yang masih
        //berstatus Approved ke TB_R_DOCUMENT_HISTORY dengan status OBSOLETE, lalu
        //menghapusnya dari TB_R_DOCUMENT. Kalau tidak ada revisi sebelumnya (dokumen baru),
        //SP tidak melakukan apa-apa - aman dipanggil selalu di titik approval terakhir.
        public DBResult SupersedePreviousRevision(DocumentMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaintenance_SupersedeRevision] @DOCUMENT_TRANSACTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
