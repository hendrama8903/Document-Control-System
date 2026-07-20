using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
//Recommit DINA
//Recommit DINA 2

namespace DMS.Models.DB
{
    [Table("TB_R_DOCUMENT")]
    public class DocumentMaintenance : BaseModel
    {
        [Key]
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public int? DOCUMENT_ID { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public string? DOCUMENT_TYPE { get; set; }
        public string? PROCESS_CODE { get; set; }
        public string? COMPANY_CODE { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? SECTION_NAME { get; set; }
        public string? ITEM_CHANGED { get; set; }
        public string? REASON { get; set; }
        public string? EXTERNAL_FLAG { get; set; }
        public string? REFERENCE_NO { get; set; }
        public string? SOURCE { get; set; }
        public DateTime? DOCUMENT_DATE { get; set; }
        public string? FILE_PATH { get; set; }
        public string? STATUS { get; set; }
        public string? STATUS_DISPLAY { get; set; }
        public int? REVISION { get; set; }
        public int? APPROVAL_ID { get; set; }
        public int? DELETE_FLAG { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DISTRIBUTION { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public int? CLASSIFIED { get; set; }
        public string? CLASSIFIED_VAL { get; set; }
        public string? DOCUMENT_YEAR { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public string? IS_APPROVED { get; set; }
        public string? IMPACT_OTHER_FLAG { get; set; }
        public int? LEVEL_CODE { get; set; }
        public string? DOCUMENT_TRANSACTION_NAME { get; set; }
        public string? DOCUMENT_CREATOR { get; set; }
        public DateTime? DOCUMENT_REVISION_0_DATE { get; set; }

        [NotMapped]
        public string? NOT_EXIST_FLAG { get; set; }
        [NotMapped]
        public string? REVISION_ALLOWAL_FLAG { get; set; }
    }
}
