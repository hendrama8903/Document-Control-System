using DMS.Common.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_SEQUENCE")]
    public class MSequence : BaseModel
    {
        public string? SEQ_TYPE { get; set; }
        public string? SEQ_CODE { get; set; }
        public int? SEQ_NO { get; set; }
    }
}
