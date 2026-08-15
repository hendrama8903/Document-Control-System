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


        // Was previously wired to sp_DocumentControlDashboard_Search but never actually
        // called from the controller (dead code). Repurposed to call the same
        // sp_P4DMaintenance_Search the controller's Search action already uses (proven
        // correct, extended with category/lifecycle-status/owner/acknowledge-ratio
        // columns), returning the richer DocumentControlDashboardDocument type instead
        // of the shared DocumentControlMaintenance.
        //
        // OPERATION_TYPE forced to 1 (real P4D registration) regardless of what the
        // caller passes - request user 2026-08-11. This is the master document
        // register: one row per registered/approved document. OPERATION_TYPE=2 rows
        // are per-user "Request Document" bookmarks from UserDashboard (just enable
        // that user's own Acknowledge button) - not separate documents, so letting
        // them through here made the same DOCUMENT_CODE appear once per user who
        // happened to request it, looking like duplicate rows to QMS.
        public IList<DocumentControlDashboardDocument> Search(DocumentControlMaintenance data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_CTRL_ID", CheckNullValue(data.DOCUMENT_CTRL_ID) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DEPARTMENT_CODE", CheckNullValue(data.DEPARTMENT_CODE) ),
                new SqlParameter ( "@YEAR", CheckNullValue(data.DOCUMENT_YEAR) ),
                new SqlParameter ( "@OPERATION_TYPE", 1 ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@USERNAME", CheckNullValue((string)null) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) ),
                new SqlParameter ( "@DOCUMENT_STATUS", CheckNullValue((string)null) )
            };

            string query = "EXEC [dbo].[sp_P4DMaintenance_Search] @DOCUMENT_CTRL_ID, @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DOCUMENT_NAME, @DIVISION, @DEPARTMENT_ID, " +
                "@DEPARTMENT_CODE, @YEAR, @OPERATION_TYPE, @STATUS, @USERNAME, @PageNumber, @PageSize, @DOCUMENT_STATUS";
            IList<DocumentControlDashboardDocument> Result = db.DocumentControlDashboardSearch.FromSqlRaw<DocumentControlDashboardDocument>(query, param.ToArray()).ToList();


            return Result;
        }
    }
}
