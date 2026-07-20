using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_DEPARTMENT")]
    public class DepartmentMaster : BaseModel
    {
        [Key]
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? DIVISION { get; set; }
        public string? DEPARTMENT_CODE_NAME { get; set; }
        [NotMapped]

        public string? IS_VALID_ONLY { get; set; }
        [NotMapped]
        public bool? STATUS_EXIST { get; set; }
        public string? DOCUMENT_CONTROL_ACCESS { get; set; }


    }
}
