using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DocumentFolderPersonalRepo : BaseRepo
    {
        private DocumentFolderPersonalRepo() { }

        #region Singleton
        private static DocumentFolderPersonalRepo instance = null;
        public static DocumentFolderPersonalRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentFolderPersonalRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<DocumentFolderPersonal> GetTree(string username, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", username )
            };

            string query = "EXEC [dbo].[sp_DocumentFolderPersonal_Tree] @USERNAME";
            IList<DocumentFolderPersonal> Result = db.DocumentFolderPersonal.FromSqlRaw<DocumentFolderPersonal>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult Insert(DocumentFolderPersonal data, string username, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FOLDER_NAME", CheckNullValue(data.FOLDER_NAME) ),
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                new SqlParameter ( "@USERNAME", username ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentFolderPersonal_Insert] @FOLDER_NAME, @PARENT_ID, @USERNAME, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult Update(DocumentFolderPersonal data, string username, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FOLDER_ID", CheckNullValue(data.FOLDER_ID) ),
                new SqlParameter ( "@FOLDER_NAME", CheckNullValue(data.FOLDER_NAME) ),
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                new SqlParameter ( "@USERNAME", username ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentFolderPersonal_Update] @FOLDER_ID, @FOLDER_NAME, @PARENT_ID, @USERNAME, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(DocumentFolderPersonal data, string username, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FOLDER_ID", CheckNullValue(data.FOLDER_ID) ),
                new SqlParameter ( "@USERNAME", username ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentFolderPersonal_Delete] @FOLDER_ID, @USERNAME, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
