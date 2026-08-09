using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class NotificationRepo : BaseRepo
    {
        private NotificationRepo() { }

        #region Singleton
        private static NotificationRepo instance = null;
        public static NotificationRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new NotificationRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<Notification> Search(Notification data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
            };

            string query = "EXEC [dbo].[sp_Notification_Search] @USERNAME";
            IList<Notification> Result = db.Notification.FromSqlRaw<Notification>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult Insert(Notification data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@NOTIFICATION_TEXT", CheckNullValue(data.NOTIFICATION_TEXT) ),
                new SqlParameter ( "@NOTIFICATION_TITLE", CheckNullValue(data.NOTIFICATION_TITLE) ),
                new SqlParameter ( "@NOTIFICATION_URL", CheckNullValue(data.NOTIFICATION_URL) ),
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Notification_Insert] @NOTIFICATION_TEXT, @NOTIFICATION_TITLE, @NOTIFICATION_URL, @USERNAME, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(Notification data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@NOTIFICATION_ID", CheckNullValue(data.NOTIFICATION_ID) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Notification_UpdateStatus] @NOTIFICATION_ID, @STATUS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult MarkAllRead(string username, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(username) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Notification_MarkAllRead] @USERNAME, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }
    }
}
