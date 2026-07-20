using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_SECTION")]
    public class SectionMaster : BaseModel
    {
        [Key]
        public int? SECTION_ID { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? SECTION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DIVISION { get; set; }
        public int? DELETE_FLAG { get; set; }
        public DateTime? VALID_FROM { get; set; }
        public DateTime? VALID_TO { get; set; }
        [NotMapped]
        public string? IS_VALID_ONLY { get; set; }
    }

    public class Select2SectionCode
    {
        public string? SECTION_CODE { get; set; }
    }

    public class Select2SectionName
    {
        public string? SECTION_NAME { get; set; }
    }

    public class Select2SectionCodeAndName
    {
        public string? SECTION_CODE { get; set; }
        public string? SECTION_NAME { get; set; }
    }
}
