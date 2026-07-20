using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace DMS.Models.DB
{
    public class DocumentReport
    {
        public string? DOCUMENT_CODE { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DOCUMENT_TRANSACTION_NAME { get; set; }
        public DateTime? DOCUMENT_DATE { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? REVISION { get; set; }
        public int? PUBLISH_FLAG { get; set; }
        public string? DOCUMENT_CREATOR { get; set; }
        public DateTime? DISTRIBUTION_DATE { get; set; }
        public string? DOCUMENT_YEAR { get; set; }
    }
}
