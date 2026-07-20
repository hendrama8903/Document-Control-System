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
    public class LogMonitoringRepo : BaseRepo
    {
        private LogMonitoringRepo() { }

        #region Singleton
        private static LogMonitoringRepo instance = null;
        public static LogMonitoringRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LogMonitoringRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<LogHeader> Search(LogHeader data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@START_DT", CheckNullValue(data.START_DT) ),
                new SqlParameter ( "@END_DT", CheckNullValue(data.END_DT) ),
                new SqlParameter ( "@PROCESS_ID", CheckNullValue(data.PROCESS_ID) ),
                new SqlParameter ( "@MODULE", CheckNullValue(data.MODULE) ),
                new SqlParameter ( "@FUNCTION", CheckNullValue(data.FUNCTION) ),
                new SqlParameter ( "@CREATED_BY", CheckNullValue(data.CREATED_BY) ),
                new SqlParameter ( "@PROCESS_STATUS", CheckNullValue(data.PROCESS_STATUS) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_LogHeader_Search] @START_DT, @END_DT, @PROCESS_ID, @MODULE, @FUNCTION, @CREATED_BY, @PROCESS_STATUS, " +
                "@PageNumber, @PageSize";
            IList<LogHeader> Result = db.LogHeader.FromSqlRaw<LogHeader>(query, param.ToArray()).ToList();

            return Result;
        }

        public LogHeader GetByKey(LogHeader data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@PROCESS_ID", CheckNullValue(data.PROCESS_ID) )
            };

            string query = "EXEC [dbo].[sp_LogHeader_GetByKey] @PROCESS_ID";
            LogHeader Result = db.LogHeader.FromSqlRaw<LogHeader>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public IList<LogDetail> SearchDetail(LogDetail data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@PROCESS_ID", CheckNullValue(data.PROCESS_ID) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_LogDetail_Search] @PROCESS_ID, " +
                "@PageNumber, @PageSize";
            IList<LogDetail> Result = db.LogDetail.FromSqlRaw<LogDetail>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<LogHeader> GetListModule(LogHeader data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MODULE", CheckNullValue(data.MODULE) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_LogHeader_GetListModule] @MODULE, " +
                "@PageNumber, @PageSize";
            IList<LogHeader> Result = db.LogHeader.FromSqlRaw<LogHeader>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<LogHeader> GetListFunction(LogHeader data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@FUNCTION", CheckNullValue(data.FUNCTION) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_LogHeader_GetListFunction] @FUNCTION, " +
                "@PageNumber, @PageSize";
            IList<LogHeader> Result = db.LogHeader.FromSqlRaw<LogHeader>(query, param.ToArray()).ToList();

            return Result;
        }

        public long StartLog(LogHeader data, string location, string loginUser, DBContext db)
        {
            SqlParameter processID = CreateSqlParameterOutputString("@PROCESS_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                processID,
                new SqlParameter ( "@MODULE", CheckNullValue(data.MODULE) ),
                new SqlParameter ( "@FUNCTION", CheckNullValue(data.FUNCTION) ),
                new SqlParameter ( "@LOCATION", CheckNullValue(location) ),
                new SqlParameter ( "@LOGIN_USER", loginUser )
            };

            string query = "EXEC sp_StartLog @PROCESS_ID OUTPUT, @MODULE, @FUNCTION, @LOCATION, @LOGIN_USER";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            long result = Convert.ToInt64(processID.Value); ;
            return result;
        }

        public string WriteLog(long processid, string processStatus, string messageType, string messageContent, string location, string loginUser, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@PROCESS_ID", CheckNullValue(processid) ),
                new SqlParameter ( "@PROCESS_STATUS", CheckNullValue(processStatus) ),
                new SqlParameter ( "@MESSAGE_TYPE", CheckNullValue(messageType) ),
                new SqlParameter ( "@MESSAGE_CONTENT", CheckNullValue(messageContent) ),
                new SqlParameter ( "@LOCATION", CheckNullValue(location) ),
                new SqlParameter ( "@LOGIN_USER", loginUser )
            };

            string query = "EXEC sp_WriteLog @PROCESS_ID, @PROCESS_STATUS, @MESSAGE_TYPE, @MESSAGE_CONTENT, @LOCATION, @LOGIN_USER";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            return "true";
        }
    }
}
