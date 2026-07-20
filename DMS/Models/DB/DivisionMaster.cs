using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_DIVISION")]
    public class DivisionMaster : BaseModel
    {
        [Key]
        public int? DIVISION_ID { get; set; }
        public string? DIVISION_CODE { get; set; }
        public string? DIVISION_NAME { get; set; }
        public string? DIVISION_CODE_NAME { get; set; }
        [NotMapped]
        public string? IS_VALID_ONLY { get; set; }
        [NotMapped]
        public bool? STATUS_EXIST { get; set; }
    }

    public class Select2DivisionUser
    {
        public string? DIVISION_CODE { get; set; }
        public string? DIVISION_NAME { get; set; }
        public string? DIVISION_CODE_NAME { get; set; }
        public string? USERNAME { get; set; }
    }
}
