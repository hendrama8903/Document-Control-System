using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_APPROVAL_H")]
    public partial class ApprovalHeader : BaseModel
    {
        [Key]
        public int? APPROVAL_ID { get; set; }
        public DateTime? APPROVAL_DATE { get; set; }
        public string? APPROVAL_STATUS { get; set; }
        public string? APPROVAL_STATUS_DESC { get; set; }
        public int? CURRENT_SEQ { get; set; }
        public string? CREATOR { get; set; }
        public string? CREATOR_NAME { get; set; }
        public string? MENU_ID { get; set; }
        public int? TRANSACTION_ID { get; set; }
    }
}
