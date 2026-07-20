using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_APPROVAL_D")]
    public partial class ApprovalDetail : BaseModel
    {
        [Key]
        public int? APPROVAL_DETAIL_ID { get; set; }
        public int? APPROVAL_ID { get; set; }
        public int? WORKFLOW_SEQ { get; set; }
        public string? APPROVER { get; set; }
        public string? APPROVER_NAME { get; set; }
        public string? STATUS { get; set; }
        public string? STATUS_DESC { get; set; }
        public string? REMARK { get; set; }
        public string? LABEL { get; set; }
        public string? FILE_PATH { get; set; }
        public string? MENU_ID { get; set; }
        public int? TRANSACTION_ID { get; set; }
        public DateTime? APPROVAL_DATE { get; set; }
        public int? CURRENT_SEQ { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_TRANSACTION_NAME { get; set; }
    }
}
