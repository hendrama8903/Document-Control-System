using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class P4DMaintenanceRepo : BaseRepo
    {
        private P4DMaintenanceRepo() { }

        #region Singleton
        private static P4DMaintenanceRepo instance = null;
        public static P4DMaintenanceRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new P4DMaintenanceRepo();
                }
                return instance;
            }
        }
        #endregion


        public IList<DocumentControlMaintenance> Search(DocumentControlMaintenance data, string loginUser, DBContext db, int? PageNumber, int? PageSize, string documentStatus = null)
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
                new SqlParameter ( "@OPERATION_TYPE", CheckNullValue(data.OPERATION_TYPE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@USERNAME", CheckNullValue(loginUser) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) ),
                new SqlParameter ( "@DOCUMENT_STATUS", CheckNullValue(documentStatus) )
            };

            string query = "EXEC [dbo].[sp_P4DMaintenance_Search] @DOCUMENT_CTRL_ID, @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DOCUMENT_NAME, @DIVISION, @DEPARTMENT_ID, " +
                "@DEPARTMENT_CODE, @YEAR, @OPERATION_TYPE, @STATUS, @USERNAME, @PageNumber, @PageSize, @DOCUMENT_STATUS";
            IList<DocumentControlMaintenance> Result = db.P4DMaintenance.FromSqlRaw<DocumentControlMaintenance>(query, param.ToArray()).ToList();


            return Result;
        }

        public P4DMaintenanceRepo GetByKey(DepartmentMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) )
            };

            string query = "EXEC [dbo].[sp_DepartmentMaster_GetByKey] @DEPARTMENT_ID";
            P4DMaintenanceRepo Result = null;// db.DepartmentMaster.FromSqlRaw<P4DMaintenanceRepo>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DocumentControlMaintenance GenerateDocumentNo(DBContext db)
        {

            string query = "EXEC [dbo].[sp_P4DMaintenance_GenerateDocumentNo]";
            //String Result = db.P4DMaintenance.FromSqlRaw<P4DMaintenance>(query).ToString();
            DocumentControlMaintenance Result = db.P4DMaintenance.FromSqlRaw<DocumentControlMaintenance>(query).AsEnumerable().FirstOrDefault(); ;

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

        public DocumentMaster GetLevelByDocumentCode(DBContext db, String documentCode)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(documentCode) )
            };

            string query = "EXEC [dbo].[sp_P4DMaintenance_GetLevelByDocumentCode]";
            //String Result = db.P4DMaintenance.FromSqlRaw<P4DMaintenance>(query).ToString();
            DocumentMaster Result = db.DocumentMaster.FromSqlRaw<DocumentMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(DocumentControlMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                new SqlParameter ( "@CHANGED_BY", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_Insert] @DOCUMENT_TRANSACTION_ID,@CREATED_BY,@CHANGED_BY, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
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

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_Update] @DOCUMENT_CTRL_ID,@DOCUMENT_CODE,@LOCATION_DOC,@CREATED_BY,@CHANGED_BY, @RETURN_MSG OUTPUT";
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

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_Delete] @DOCUMENT_CTRL_ID, @DOCUMENT_CODE, @LOGIN_USER, @RETURN_MSG OUTPUT";
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

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_ApproveReject] @DOCUMENT_CTRL_ID, @DOCUMENT_CODE, @IS_APPROVED, @REMARKS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UnReceive(DocumentControlMaintenance data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CTRL_ID", CheckNullValue(data.DOCUMENT_CTRL_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_UnReceive] @DOCUMENT_CTRL_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<DocumentDistribution> SearchDocumentDistribution(DocumentDistribution data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DISTRIBUTION_ID", CheckNullValue(data.DISTRIBUTION_ID) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_P4DMaintenance_SearchDistribution] @DISTRIBUTION_ID, @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @STATUS, @PageNumber, @PageSize";
            IList<DocumentDistribution> Result = db.DocumentDistribution.FromSqlRaw<DocumentDistribution>(query, param.ToArray()).ToList();


            return Result;
        }

        public DBResult InsertDistribution(DocumentDistribution data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DATE", CheckNullValue(data.DATE) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                new SqlParameter ( "@CHANGED_BY", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_InsertDistribution] @DOCUMENT_TRANSACTION_ID,@DEPARTMENT_ID,@DATE,@CREATED_BY,@CHANGED_BY, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UpdateDistribution(DocumentDistribution data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DISTRIBUTION_ID", CheckNullValue(data.DISTRIBUTION_ID) ),
                new SqlParameter ("@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID)),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DIVISION_ID", CheckNullValue(data.DIVISION_ID) ),
                new SqlParameter ( "@DATE", CheckNullValue(data.DATE) ),
                new SqlParameter ( "@CREATED_BY", loginUser ),
                new SqlParameter ( "@CHANGED_BY", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_UpdateDistribution] @DISTRIBUTION_ID, @DOCUMENT_TRANSACTION_ID,@DEPARTMENT_ID,@DIVISION_ID,@DATE,@CREATED_BY,@CHANGED_BY, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UpdateDistributionPublish(PublishHistory data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CTRL_ID", CheckNullValue(data.DOCUMENT_CTRL_ID) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_UpdateDistributionPublish] @DOCUMENT_CTRL_ID, @DEPARTMENT_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult DeleteDistribution(DocumentDistribution data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DISTRIBUTION_ID", CheckNullValue(data.DISTRIBUTION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_DeleteDistribution] @DISTRIBUTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DocumentDistribution GetByKeyDistribution(DocumentDistribution data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DISTRIBUTION_ID", CheckNullValue(data.DISTRIBUTION_ID) ),
                new SqlParameter ("@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID)),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(1) ),
                new SqlParameter ( "@PageSize", CheckNullValue(1) )
            };

            string query = "EXEC [dbo].[sp_P4DMaintenance_SearchDistribution] @DISTRIBUTION_ID, @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @PageNumber, @PageSize";
            //String Result = db.P4DMaintenance.FromSqlRaw<P4DMaintenance>(query).ToString();
            DocumentDistribution Result = db.DocumentDistribution.FromSqlRaw<DocumentDistribution>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult SendDistribution(DocumentDistribution data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DISTRIBUTION_ID", CheckNullValue(data.DISTRIBUTION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_SendDistribution] @DISTRIBUTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult SendDistributionAll(DocumentDistribution data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ("@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID)),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_P4DMaintenance_SendDistributionAll] @DOCUMENT_TRANSACTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
