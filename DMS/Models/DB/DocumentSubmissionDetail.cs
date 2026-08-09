using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_DOC_SUBMISSION_FORM_D")]
    public class DocumentSubmissionDetail : BaseModel
    {
        [Key]
        public int? SUBMISSION_DETAIL_ID { get; set; }
        public int? SUBMISSION_ID { get; set; }
        public int? LINE_NO { get; set; }
        public string? DOCUMENT_NO { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? CLASSIFICATION { get; set; }
        public string? CLASSIFICATION_DISPLAY { get; set; }
        public string? SUBMISSION_TYPE { get; set; }
        public string? SUBMISSION_TYPE_DISPLAY { get; set; }
        public int? REVISION_NO { get; set; }
        public string? ITEM_CHANGED { get; set; }
        public string? REASON { get; set; }
        public DateTime? EFFECTIVE_DATE { get; set; }
        public string? COPY_TYPE { get; set; }
        public string? COPY_TYPE_DISPLAY { get; set; }
        public int? COPY_QTY { get; set; }
        public string? DISTRIBUTION_DEPT_IDS { get; set; }
        public string? DISTRIBUTION_DEPT_NAMES { get; set; }
    }
}
