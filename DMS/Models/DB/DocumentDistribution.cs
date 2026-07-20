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

    }
}
