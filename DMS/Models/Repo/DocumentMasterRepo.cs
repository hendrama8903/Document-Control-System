using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DocumentMasterRepo : BaseRepo
    {
        private DocumentMasterRepo() { }

        #region Singleton
        private static DocumentMasterRepo instance = null;
        public static DocumentMasterRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentMasterRepo();
                }
                return instance;
            }
        }
        #endregion

        // Dibaca manual: DELETE_FLAG bukan properti DocumentMaster, dan menambahkannya
        // mewajibkan semua SP yang di-FromSqlRaw ke entity itu ikut mengembalikannya.
        public List<Dictionary<string, object>> SearchWithStatus(DBContext db)
        {
            var rows = new List<Dictionary<string, object>>();
            var connection = db.Database.GetDbConnection();
            bool wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed) connection.Open();
            try
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "[dbo].[sp_DocumentMaster_SearchWithStatus]";
                    command.CommandType = System.Data.CommandType.StoredProcedure;
                    using (var r = command.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < r.FieldCount; i++)
                            {
                                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                            }
                            rows.Add(row);
                        }
                    }
                }
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
            return rows;
        }

        public IList<DocumentMaster> Search(DocumentMaster data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@LEVEL", CheckNullValue(data.LEVEL) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentMaster_Search] @DOCUMENT_CODE, @DOCUMENT_NAME, @LEVEL, @PageNumber, @PageSize";
            IList<DocumentMaster> Result = db.DocumentMaster.FromSqlRaw<DocumentMaster>(query, param.ToArray()).ToList();

            return Result;
        }

        public DocumentMaster GetByKey(DocumentMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) )
            };

            string query = "EXEC [dbo].[sp_DocumentMaster_GetByKey] @DOCUMENT_ID";
            DocumentMaster Result = db.DocumentMaster.FromSqlRaw<DocumentMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(DocumentMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@LEVEL", CheckNullValue(data.LEVEL) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@REVIEW_CYCLE_MONTHS", CheckNullValue(data.REVIEW_CYCLE_MONTHS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaster_Insert] @DOCUMENT_CODE, @DOCUMENT_NAME, @LEVEL, @FILE_PATH, @REVIEW_CYCLE_MONTHS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(DocumentMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@LEVEL", CheckNullValue(data.LEVEL) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@REVIEW_CYCLE_MONTHS", CheckNullValue(data.REVIEW_CYCLE_MONTHS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaster_Update] @DOCUMENT_ID, @DOCUMENT_CODE, @DOCUMENT_NAME, @LEVEL, @FILE_PATH, @REVIEW_CYCLE_MONTHS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Soft delete (DELETE_FLAG = 2) for a category that was never used; see sp_DocumentMaster_Remove.
        public DBResult Remove(DocumentMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            db.Database.ExecuteSqlRaw("EXEC @RETURN_VAL = [dbo].[sp_DocumentMaster_Remove] @DOCUMENT_ID, @LOGIN_USER, @RETURN_MSG OUTPUT", param.ToArray());
            return new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
        }

        public DBResult RemoveAttachment(DocumentMaster data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_DocumentMaster_RemoveAttachment] @DOCUMENT_ID," +
                "@LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<ExcelTemplateMaster> SearchTemplate(ExcelTemplateMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_ID", CheckNullValue(data.DOCUMENT_ID) )
            };

            string query = "EXEC [dbo].[sp_ExcelTemplate_Search] @DOCUMENT_ID";
            IList<ExcelTemplateMaster> Result = db.ExcelTemplateMaster.FromSqlRaw<ExcelTemplateMaster>(query, param.ToArray()).ToList();

            return Result;
        }

    }
}
