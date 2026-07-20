using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.Repo
{
    public class WorkflowDocRepo : BaseRepo
    {
        private WorkflowDocRepo() { }

        #region Singleton
        private static WorkflowDocRepo instance = null;
        public static WorkflowDocRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new WorkflowDocRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<WorkflowDocArr> Search(WorkflowDocArr data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@CREATOR_LEVEL", CheckNullValue(data.CREATOR_LEVEL) ),
                new SqlParameter ( "@DOCUMENT_LEVEL", CheckNullValue(data.@DOCUMENT_LEVEL) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Workflow_DocSearch] @CREATOR_LEVEL,@DOCUMENT_LEVEL, @PageNumber, @PageSize";
            IList<WorkflowDocArr> Result = db.WorkflowDocArr.FromSqlRaw<WorkflowDocArr>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<WorkflowDoc> GetByCode(WorkflowDoc data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@WORKFLOW_DOC_ID", CheckNullValue(data.WORKFLOW_DOC_ID) ),
                new SqlParameter ( "@DOCUMENT_LEVEL", CheckNullValue(data.DOCUMENT_LEVEL) )
            };

            string query = "EXEC [dbo].[sp_Workflow_DocGetByCode] @WORKFLOW_DOC_ID, @DOCUMENT_LEVEL";
            IList<WorkflowDoc> Result = db.WorkflowDoc.FromSqlRaw<WorkflowDoc>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<WorkflowDoc> GetByName(WorkflowDoc data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_LEVEL", CheckNullValue(data.DOCUMENT_LEVEL) )
            };

            string query = "EXEC [dbo].[sp_Workflow_DocGetByName] @DOCUMENT_LEVEL";
            IList<WorkflowDoc> Result = db.WorkflowDoc.FromSqlRaw<WorkflowDoc>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult InsertHeader(WorkflowDoc data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId  = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_LEVEL", CheckNullValue(data.DOCUMENT_LEVEL) ),
                new SqlParameter ( "@CREATOR_LEVEL", CheckNullValue(data.CREATOR_LEVEL) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_DocInsertHeader] @DOCUMENT_LEVEL, @CREATOR_LEVEL, @LOGIN_USER, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult InsertDetail(WorkflowDoc data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@WORKFLOW_DOC_ID", CheckNullValue(data.WORKFLOW_DOC_ID) ),
                new SqlParameter ( "@WORKFLOW_SEQ", CheckNullValue(data.WORKFLOW_SEQ) ),
                new SqlParameter ( "@POSITION_ID", CheckNullValue(data.POSITION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_DocInsertDetail] @WORKFLOW_DOC_ID, @WORKFLOW_SEQ, @POSITION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(WorkflowDoc data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@WORKFLOW_DOC_ID", CheckNullValue(data.WORKFLOW_DOC_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_DocDelete] @WORKFLOW_DOC_ID,@LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Create(String workflowName, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@WORKFLOW_NAME", CheckNullValue(workflowName) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_Create] @WORKFLOW_NAME, @LOGIN_USER, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            string affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray()).ToString();

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }
    }
}
