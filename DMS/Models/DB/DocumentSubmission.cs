using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_DOC_SUBMISSION_FORM_H")]
    public class DocumentSubmission : BaseModel
    {
        [Key]
        public int? SUBMISSION_ID { get; set; }
        public string? SUBMISSION_NO { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? SECTION_NAME { get; set; }
        public DateTime? SUBMISSION_DATE { get; set; }
        public string? DOC_CATEGORY { get; set; }
        public string? STATUS { get; set; }
        public string? STATUS_DISPLAY { get; set; }
        public int? APPROVAL_ID { get; set; }
        public int? APPROVAL_STATUS { get; set; }
        public int? CURRENT_SEQ { get; set; }
        public string? DOCUMENT_CREATOR { get; set; }
        public string? SUBMITTED_BY { get; set; }
        public DateTime? SUBMITTED_DT { get; set; }
        public string? REMARK { get; set; }
        public int? DETAIL_COUNT { get; set; }
    }
}
