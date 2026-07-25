using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class MSequenceRepo : BaseRepo
    {
        private MSequenceRepo() { }

        #region Singleton
        private static MSequenceRepo instance = null;
        public static MSequenceRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MSequenceRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<MSequence> Search(MSequence data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SEQ_CODE", CheckNullValue(data.SEQ_CODE) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_MSequence_Search] @SEQ_CODE, @PageNumber, @PageSize";
            IList<MSequence> Result = db.MSequence.FromSqlRaw<MSequence>(query, param.ToArray()).ToList();

            return Result;
        }

        public MSequence GetByKey(MSequence data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@SEQ_TYPE", CheckNullValue(data.SEQ_TYPE) ),
                new SqlParameter ( "@SEQ_CODE", CheckNullValue(data.SEQ_CODE) )
            };

            string query = "EXEC [dbo].[sp_MSequence_GetByKey] @SEQ_TYPE, @SEQ_CODE";
            MSequence Result = db.MSequence.FromSqlRaw<MSequence>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Update(MSequence data, string oldSeqType, string oldSeqCode, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@OLD_SEQ_TYPE", CheckNullValue(oldSeqType) ),
                new SqlParameter ( "@OLD_SEQ_CODE", CheckNullValue(oldSeqCode) ),
                new SqlParameter ( "@SEQ_TYPE", CheckNullValue(data.SEQ_TYPE) ),
                new SqlParameter ( "@SEQ_CODE", CheckNullValue(data.SEQ_CODE) ),
                new SqlParameter ( "@SEQ_NO", CheckNullValue(data.SEQ_NO) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_MSequence_Update] @OLD_SEQ_TYPE, @OLD_SEQ_CODE, @SEQ_TYPE, @SEQ_CODE, @SEQ_NO, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Force-reset SEQ_NO, bypassing the "can't go lower" guard in sp_MSequence_Update.
        // Admin-only escape hatch - lowering the number risks a future duplicate document code.
        public DBResult Reset(MSequence data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@SEQ_TYPE", CheckNullValue(data.SEQ_TYPE) ),
                new SqlParameter ( "@SEQ_CODE", CheckNullValue(data.SEQ_CODE) ),
                new SqlParameter ( "@SEQ_NO", CheckNullValue(data.SEQ_NO) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_MSequence_Reset] @SEQ_TYPE, @SEQ_CODE, @SEQ_NO, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
