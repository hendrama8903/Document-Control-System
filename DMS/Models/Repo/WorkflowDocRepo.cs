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
                returnId,
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) )
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_DocInsertHeader] @DOCUMENT_LEVEL, @CREATOR_LEVEL, @LOGIN_USER, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT, @DOCUMENT_ID = @DOCUMENT_ID";
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
                returnMsg,
                new SqlParameter ( "@IS_MANDATORY", data.IS_MANDATORY == true ),
                new SqlParameter ( "@LABEL", CheckNullValue(data.LABEL) )
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Workflow_DocInsertDetail] @WORKFLOW_DOC_ID, @WORKFLOW_SEQ, @POSITION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT, @IS_MANDATORY = @IS_MANDATORY, @LABEL = @LABEL";
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

        /// <summary>
        /// Pratinjau approval chain untuk (level, kategori, pembuat) - memakai fungsi
        /// yang sama dengan sp_WorkflowDoc_Create, jadi hasilnya sama dengan chain
        /// yang nanti dibuat.
        /// </summary>
        public List<WorkflowPreviewStep> Preview(int? documentLevel, int? documentId, string documentCreator, DBContext db)
        {
            var steps = new List<WorkflowPreviewStep>();
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "[dbo].[sp_WorkflowDoc_Preview]";
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@DOCUMENT_LEVEL", CheckNullValue(documentLevel)));
                    command.Parameters.Add(new SqlParameter("@DOCUMENT_ID", CheckNullValue(documentId)));
                    command.Parameters.Add(new SqlParameter("@DOCUMENT_CREATOR", CheckNullValue(documentCreator)));

                    using (var r = command.ExecuteReader())
                    {
                        string S(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : Convert.ToString(r[c]);
                        int? I(string c) => r.IsDBNull(r.GetOrdinal(c)) ? null : Convert.ToInt32(r[c]);
                        bool B(string c) => !r.IsDBNull(r.GetOrdinal(c)) && Convert.ToBoolean(r[c]);
                        while (r.Read())
                        {
                            steps.Add(new WorkflowPreviewStep
                            {
                                STATUS = S("STATUS"),
                                CREATOR_POSITION = S("CREATOR_POSITION"),
                                IS_CATEGORY_WORKFLOW = B("IS_CATEGORY_WORKFLOW"),
                                WORKFLOW_SEQ = I("WORKFLOW_SEQ"),
                                ACTUAL_SEQ = I("ACTUAL_SEQ"),
                                POSITION_ID = I("POSITION_ID"),
                                POSITION_NAME = S("POSITION_NAME"),
                                LABEL = S("LABEL"),
                                IS_MANDATORY = B("IS_MANDATORY"),
                                APPROVER = S("APPROVER"),
                                APPROVER_NAME = S("APPROVER_NAME"),
                            });
                        }
                    }
                }
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
            return steps;
        }

        /// <summary>Jabatan yang bisa muncul di approval chain sebuah kategori (untuk cek kotak POS di template).</summary>
        public WorkflowCategoryPositions CategoryPositions(int? documentLevel, int? documentId, DBContext db)
        {
            var result = new WorkflowCategoryPositions();
            if (documentLevel == null) return result;

            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "[dbo].[sp_WorkflowDoc_CategoryPositions]";
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    command.Parameters.Add(new SqlParameter("@DOCUMENT_LEVEL", CheckNullValue(documentLevel)));
                    command.Parameters.Add(new SqlParameter("@DOCUMENT_ID", CheckNullValue(documentId)));

                    using (var r = command.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            int id = Convert.ToInt32(r["POSITION_ID"]);
                            result.Positions[id] = r["POSITION_NAME"] == DBNull.Value ? "Position " + id : Convert.ToString(r["POSITION_NAME"]);
                            if (r["MAX_STEPS"] != DBNull.Value) result.MaxSteps = Convert.ToInt32(r["MAX_STEPS"]);
                        }
                    }
                }
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
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
