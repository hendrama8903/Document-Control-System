using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DocumentLogRepo : BaseRepo
    {
        private DocumentLogRepo() { }

        #region Singleton
        private static DocumentLogRepo instance = null;
        public static DocumentLogRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentLogRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<DocumentLog> Search(DocumentLog data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@LOG_TYPE", CheckNullValue(data.LOG_TYPE) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentLog_Search] @DOCUMENT_TRANSACTION_ID, @LOG_TYPE, @PageNumber, @PageSize";
            IList<DocumentLog> Result = db.DocumentLog.FromSqlRaw<DocumentLog>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult Insert(DocumentLog data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@LOG_TYPE", CheckNullValue(data.LOG_TYPE) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentLog_Insert] @DOCUMENT_TRANSACTION_ID, @LOG_TYPE, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}