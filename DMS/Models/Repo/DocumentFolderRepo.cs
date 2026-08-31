using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DocumentFolderRepo : BaseRepo
    {
        private DocumentFolderRepo() { }

        #region Singleton
        private static DocumentFolderRepo instance = null;
        public static DocumentFolderRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentFolderRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<DocumentFolder> GetTree(DBContext db)
        {
            string query = "EXEC [dbo].[sp_DocumentFolder_Tree]";
            IList<DocumentFolder> Result = db.DocumentFolder.FromSqlRaw<DocumentFolder>(query).ToList();

            return Result;
        }

        public DBResult Insert(DocumentFolder data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FOLDER_NAME", CheckNullValue(data.FOLDER_NAME) ),
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentFolder_Insert] @FOLDER_NAME, @PARENT_ID, @LOGIN_USER, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult Update(DocumentFolder data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FOLDER_ID", CheckNullValue(data.FOLDER_ID) ),
                new SqlParameter ( "@FOLDER_NAME", CheckNullValue(data.FOLDER_NAME) ),
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentFolder_Update] @FOLDER_ID, @FOLDER_NAME, @PARENT_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(DocumentFolder data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@FOLDER_ID", CheckNullValue(data.FOLDER_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentFolder_Delete] @FOLDER_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
