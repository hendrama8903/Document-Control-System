using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
//Recommit DINA

namespace DMS.Models.DB
{
    [Table("TB_R_DOCUMENT_DISTRIBUTION")]
    public class DocumentDistribution
    {
        [Key]
        public int? DISTRIBUTION_ID { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? DIVISION_ID { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }

        public string? DATE { get; set; }
        public string? STATUS { get; set; }
        public string? STATUS_DISPLAY { get; set; }

        // 1 kalau department ini sudah klik Accepted di UserDashboard untuk
        // dokumen ini (TB_R_PUBLISH_HISTORY) - dipakai untuk bedakan "Waiting
        // Acceptance" vs "Accepted" di popup Distribution (request Hendra 2026-08-13).
        public int? ACCEPTED_FLAG { get; set; }
    }
}
