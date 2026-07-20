using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_DOCUMENT_LOG")]
    public partial class DocumentLog : BaseModel
    {
        [Key]
        public int? DOCUMENT_LOG_ID { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? LOG_TYPE { get; set; }
        public string? LOG_TYPE_VAL { get; set; }
    }
}
