using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MDC.Models.Repo
{
    public class ApprovalRepo : BaseRepo
    {
        private ApprovalRepo() { }

        #region Singleton
        private static ApprovalRepo instance = null;
        public static ApprovalRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ApprovalRepo();
                }
                return instance;
            }
        }
        #endregion

        public ApprovalHeader GetApprovalHeader(int approvalId, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(approvalId) )
            };

            string query = "EXEC [dbo].[sp_Approval_GetHeader] @APPROVAL_ID";
            ApprovalHeader Result = db.ApprovalHeader.FromSqlRaw<ApprovalHeader>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public IList<ApprovalDetail> GetApprovalDetail(int approvalId, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(approvalId) )
            };

            string query = "EXEC [dbo].[sp_Approval_GetDetail] @APPROVAL_ID";
            IList<ApprovalDetail> Result = db.ApprovalDetail.FromSqlRaw<ApprovalDetail>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<ApprovalDetail> GetApprovalHistoryDetail(ApprovalDetail data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(data.APPROVAL_ID) ),
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) ),
                new SqlParameter ( "@TRANSACTION_ID", CheckNullValue(data.TRANSACTION_ID) )
            };

            string query = "EXEC [dbo].[sp_Approval_GetHistoryDetail] @APPROVAL_ID, @MENU_ID, @TRANSACTION_ID";
            IList<ApprovalDetail> Result = db.ApprovalDetail.FromSqlRaw<ApprovalDetail>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<ApprovalDetail> GetPendingByApprover(string approver, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@APPROVER", CheckNullValue(approver) )
            };

            string query = "EXEC [dbo].[sp_Approval_GetPendingByApprover] @APPROVER";
            IList<ApprovalDetail> Result = db.ApprovalDetail.FromSqlRaw<ApprovalDetail>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult ReassignApprover(int approvalId, int workflowSeq, string oldApprover, string newApprover, string reason, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(approvalId) ),
                new SqlParameter ( "@WORKFLOW_SEQ", CheckNullValue(workflowSeq) ),
                new SqlParameter ( "@OLD_APPROVER", CheckNullValue(oldApprover) ),
                new SqlParameter ( "@NEW_APPROVER", CheckNullValue(newApprover) ),
                new SqlParameter ( "@REASON", CheckNullValue(reason) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Approval_ReassignApprover] @APPROVAL_ID, @WORKFLOW_SEQ, @OLD_APPROVER, @NEW_APPROVER, @REASON, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Approve(int approvalId, int workflowSeq, string approver, string remark, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(approvalId) ),
                new SqlParameter ( "@WORKFLOW_SEQ", CheckNullValue(workflowSeq) ),
                new SqlParameter ( "@APPROVER", CheckNullValue(approver) ),
                new SqlParameter ( "@REMARK", CheckNullValue(remark) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Approval_Approve] @APPROVAL_ID, @WORKFLOW_SEQ, @APPROVER, @REMARK, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Reject(int approvalId, int workflowSeq, string approver, string remark, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(approvalId) ),
                new SqlParameter ( "@WORKFLOW_SEQ", CheckNullValue(workflowSeq) ),
                new SqlParameter ( "@APPROVER", CheckNullValue(approver) ),
                new SqlParameter ( "@REMARK", CheckNullValue(remark) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Approval_Reject] @APPROVAL_ID, @WORKFLOW_SEQ, @APPROVER, @REMARK, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(ApprovalHeader data, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@APPROVAL_ID", CheckNullValue(data.APPROVAL_ID) ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Approval_Delete] @APPROVAL_ID, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

    }
}
