using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_COPY_REQUEST_H")]
    public class CopyRequest : BaseModel
    {
        [Key]
        public int? REQUEST_ID { get; set; }
        public string? REQUEST_NO { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? SECTION_NAME { get; set; }
        public DateTime? REQUEST_DATE { get; set; }
        public string? DOC_CATEGORY { get; set; }
        public string? STATUS { get; set; }
        public string? STATUS_DISPLAY { get; set; }
        public string? REQUESTED_BY { get; set; }
        public string? SUBMITTED_BY { get; set; }
        public DateTime? SUBMITTED_DT { get; set; }
        public string? REMARK { get; set; }
        // Approval satu langkah oleh QMS - lihat sp_CopyRequest_ApproveReject.
        public string? APPROVED_BY { get; set; }
        public DateTime? APPROVED_DT { get; set; }
        public string? APPROVAL_REMARK { get; set; }
        // Requester mengonfirmasi sudah menerima copy fisiknya - lihat sp_CopyRequest_Accept.
        public int? ACCEPTED_FLAG { get; set; }
        public string? ACCEPTED_BY { get; set; }
        public DateTime? ACCEPTED_DT { get; set; }
        public int? DETAIL_COUNT { get; set; }
    }
}
