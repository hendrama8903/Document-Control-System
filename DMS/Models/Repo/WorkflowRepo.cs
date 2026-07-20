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
    public class WorkflowRepo : BaseRepo
    {
        private WorkflowRepo() { }

        #region Singleton
        private static WorkflowRepo instance = null;
        public static WorkflowRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new WorkflowRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<Workflow> Search(Workflow data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@WORKFLOW_NAME", CheckNullValue(data.WORKFLOW_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Workflow_Search] @WORKFLOW_NAME, @PageNumber, @PageSize";
            IList<Workflow> Result = db.Workflow.FromSqlRaw<Workflow>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<Workflow> GetByCode(Workflow data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@WORKFLOW_CODE", CheckNullValue(data.WORKFLOW_CODE) )
            };

            string query = "EXEC [dbo].[sp_Workflow_GetByCode] @WORKFLOW_CODE";
            IList<Workflow> Result = db.Workflow.FromSqlRaw<Workflow>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<Workflow> GetByName(Workflow data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@WORKFLOW_NAME", CheckNullValue(data.WORKFLOW_NAME) )
            };

            string query = "EXEC [dbo].[sp_Workflow_GetByName] @WORKFLOW_NAME";
            IList<Workflow> Result = db.Workflow.FromSqlRaw<Workflow>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult InsertHeader(Workflow data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@WORKFLOW_CODE", CheckNullValue(data.WORKFLOW_CODE) ),
                new SqlParameter ( "@WORKFLOW_NAME", CheckNullValue(data.WORKFLOW_NAME) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_InsertHeader] @WORKFLOW_CODE, @WORKFLOW_NAME, @LOGIN_USER, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult InsertDetail(Workflow data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@WORKFLOW_ID", CheckNullValue(data.WORKFLOW_ID) ),
                new SqlParameter ( "@WORKFLOW_SEQ", CheckNullValue(data.WORKFLOW_SEQ) ),
                new SqlParameter ( "@APPROVER", CheckNullValue(data.APPROVER) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_InsertDetail] @WORKFLOW_ID, @WORKFLOW_SEQ, @APPROVER, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(Workflow data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@WORKFLOW_ID", CheckNullValue(data.WORKFLOW_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_Delete] @WORKFLOW_ID,@LOGIN_USER, @RETURN_MSG OUTPUT";
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
