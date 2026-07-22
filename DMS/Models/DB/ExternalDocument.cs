using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_EXTERNAL_DOCUMENT")]
    public class ExternalDocument : BaseModel
    {
        [Key]
        public int? EXTERNAL_DOCUMENT_ID { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public string? REVISION_EDITION { get; set; }
        public DateTime? RECEIVED_DATE { get; set; }
        public string? EXTERNAL_TYPE { get; set; }
        public string? STORAGE_LOCATION { get; set; }
        public string? STORAGE_PIC { get; set; }
        public string? REVIEW_FREQUENCY { get; set; }
        public string? REVIEW_SOURCE { get; set; }
        public DateTime? LAST_CHECK_DATE { get; set; }
        public int? REVIEW_CYCLE_MONTHS { get; set; }
        public DateTime? NEXT_REVIEW_DATE { get; set; }
        public DateTime? LAST_REVIEW_REMINDER_DT { get; set; }
        public int? STATUS { get; set; }
        public string? STATUS_DISPLAY { get; set; }
        public int? INTERNAL_IMPACT { get; set; }
        public string? INTERNAL_IMPACT_DISPLAY { get; set; }
        public int? IMPACTED_DOCUMENT_TRANSACTION_ID { get; set; }
        public string? IMPACTED_DOCUMENT_CODE { get; set; }
        public string? IMPACTED_DOCUMENT_NAME { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? FILE_PATH { get; set; }
    }
}
