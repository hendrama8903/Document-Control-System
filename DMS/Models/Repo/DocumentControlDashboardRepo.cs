using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DocumentControlDashboardRepo : BaseRepo
    {
        private DocumentControlDashboardRepo() { }

        #region Singleton
        private static DocumentControlDashboardRepo instance = null;
        public static DocumentControlDashboardRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentControlDashboardRepo();
                }
                return instance;
            }
        }
        #endregion


        // Rewrite (2026-08-28, request Hendra): used to reuse sp_P4DMaintenance_Search,
        // which drives off TB_R_CTRL_DOCUMENT (the P4D distribution-registration table) -
        // so a document only showed up on this "master document register" AFTER someone
        // manually registered it into P4D Maintenance. Legacy Document Import never
        // creates a TB_R_CTRL_DOCUMENT row, so imported master documents (already
        // effective in real life) were invisible here entirely.
        //
        // Now calls the dedicated sp_DocumentControlDashboard_Search, which drives off
        // TB_R_DOCUMENT directly (TB_R_CTRL_DOCUMENT is an optional left join for the
        // P4D-specific columns only) - see that SP's header comment. A document appears
        // here as soon as it's Approved/Published (@DOCUMENT_STATUS defaults to '1,5'
        // server-side), regardless of P4D registration.
        public IList<DocumentControlDashboardDocument> Search(DocumentControlMaintenance data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@YEAR", CheckNullValue(data.DOCUMENT_YEAR) ),
                new SqlParameter ( "@DOCUMENT_STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentControlDashboard_Search] @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DOCUMENT_NAME, @DIVISION, @DEPARTMENT_ID, " +
                "@YEAR, @DOCUMENT_STATUS, @PageNumber, @PageSize";
            IList<DocumentControlDashboardDocument> Result = db.DocumentControlDashboardSearch.FromSqlRaw<DocumentControlDashboardDocument>(query, param.ToArray()).ToList();


            return Result;
        }

        // Assigns (or clears, when folderId is null) one document's folder - called
        // once per document from the controller's MoveDocumentsToFolder loop, same
        // "single-row SP, loop in C#" pattern as PositionMasterController.DeleteMultiple.
        public DBResult AssignFolder(int documentTransactionId, int? folderId, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", documentTransactionId ),
                new SqlParameter ( "@FOLDER_ID", CheckNullValue(folderId) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentControlDashboard_AssignFolder] @DOCUMENT_TRANSACTION_ID, @FOLDER_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
