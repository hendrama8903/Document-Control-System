using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class NotificationSettingRepo : BaseRepo
    {
        private NotificationSettingRepo() { }

        #region Singleton
        private static NotificationSettingRepo instance = null;
        public static NotificationSettingRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new NotificationSettingRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<NotificationSetting> Search(DBContext db)
        {
            string query = "EXEC [dbo].[sp_NotificationSetting_Search]";
            return db.NotificationSetting.FromSqlRaw<NotificationSetting>(query).ToList();
        }

        public DBResult ToggleEmail(string notificationType, bool sendEmail, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@NOTIFICATION_TYPE", CheckNullValue(notificationType) ),
                new SqlParameter ( "@SEND_EMAIL", sendEmail ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_NotificationSetting_ToggleEmail] @NOTIFICATION_TYPE, @SEND_EMAIL, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Dipakai di titik-titik pengiriman email (SendApprovalEmailAsync, dst) sebelum
        // enqueue Hangfire job - kalau baris settingnya belum pernah dibuat sama sekali
        // (mis. jenis notifikasi baru yang belum di-seed), default-nya tetap kirim (true),
        // supaya perilaku existing tidak berubah tanpa sengaja.
        public bool IsEmailEnabled(string notificationType, DBContext db)
        {
            string query = "EXEC [dbo].[sp_NotificationSetting_Search]";
            NotificationSetting setting = db.NotificationSetting
                .FromSqlRaw<NotificationSetting>(query)
                .AsEnumerable()
                .FirstOrDefault(x => x.NOTIFICATION_TYPE == notificationType);

            return setting?.SEND_EMAIL ?? true;
        }
    }
}
