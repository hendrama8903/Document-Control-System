using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_POSITION")]
    public class PositionMaster
    {
        [Key]
        public int? POSITION_ID { get; set; }
        public string? POSITION_NAME { get; set; }
        public int? POSITION_LEVEL { get; set; }
        public int? DELETE_FLAG { get; set; }
        public string? CREATED_BY { get; set; }
        public DateTime? CREATED_DT { get; set; }
        public string? CHANGED_BY { get; set; }
        public DateTime? CHANGED_DT { get; set; }
    }
}
