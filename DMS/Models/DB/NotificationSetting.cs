using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Maps 1:1 to TB_M_NOTIFICATION_SETTING (sp_NotificationSetting_Search /
    // sp_NotificationSetting_ToggleEmail). Keyless/unmapped in DBContext - see
    // OnModelCreating - only used to shape FromSqlRaw results.
    public class NotificationSetting : BaseModel
    {
        public string? NOTIFICATION_TYPE { get; set; }
        public string? NOTIFICATION_LABEL { get; set; }
        public string? MODULE_NAME { get; set; }
        public bool? SEND_EMAIL { get; set; }
        public string? CHANGED_BY { get; set; }
        public DateTime? CHANGED_DT { get; set; }
    }
}
