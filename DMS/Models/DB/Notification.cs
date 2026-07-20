using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_NOTIFICATION")]
    public class Notification
    {
        [Key]
        public int? NOTIFICATION_ID { get; set; }
        public string? NOTIFICATION_TITLE { get; set; }
        public string? NOTIFICATION_TEXT { get; set; }
        public string? NOTIFICATION_URL { get; set; }
        public string? USERNAME { get; set; }
        public string? STATUS { get; set; }
        public DateTime? NOTIFICATION_DATE { get; set; }
    }
}