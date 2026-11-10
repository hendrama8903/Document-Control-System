using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_COPY_REQUEST_D")]
    public class CopyRequestDetail : BaseModel
    {
        [Key]
        public int? REQUEST_DETAIL_ID { get; set; }
        public int? REQUEST_ID { get; set; }
        public int? LINE_NO { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? REVISION_NO { get; set; }
        public string? COPY_TYPE { get; set; }
        public string? COPY_TYPE_DISPLAY { get; set; }
        public int? COPY_QTY { get; set; }
        public string? REASON { get; set; }
        public string? COUNTERMEASURE { get; set; }
        public string? REMARKS { get; set; }
    }
}
