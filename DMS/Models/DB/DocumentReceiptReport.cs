using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_PUBLISH_HISTORY")]
    public class DocumentReceiptReport
    {
        [Key]
        public int? PUBLISH_HISTORY_ID { get; set; }
        public int? DOCUMENT_CTRL_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? REVISION { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public DateTime? RECEIVED_DATE { get; set; }
        public string? RECEIVED_BY { get; set; }
        public string? RECEIVED_BY_NAME { get; set; }
    }
}
