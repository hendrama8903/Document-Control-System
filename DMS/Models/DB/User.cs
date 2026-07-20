using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_USER")]
    public partial class User : BaseModel
    {
        [Key]
        public string? USERNAME { get; set; }
        public string? REG_NO { get; set; }
        public string? PASSWORD { get; set; }
        public string? OLD_PASSWORD { get; set; }
        public string? CONFIRM_PASSWORD { get; set; }
        public string? FULL_NAME { get; set; }
        public string? EMAIL { get; set; }
        public string? PHONE { get; set; }
        public string? ROLE_ID { get; set; }
        public string? ROLE_NAME { get; set; }
        public int? POSITION_ID { get; set; }
        public string? POSITION_NAME { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public int? SECTION_ID { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? SECTION_NAME { get; set; }
        public string? DOCUMENT_CONTROL_ACCESS { get; set; }
        public string? FILE_PATH { get; set; }
        public string? AD_USER { get; set; }
    }

    public partial class UserPosition
    {
        [Key]
        public int? USER_POS_ID { get; set; }
        public string? USERNAME { get; set; }
        public int? POSITION_ID { get; set; }
        public string? POSITION_NAME { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public int? SECTION_ID { get; set; }
        public string? SECTION_NAME { get; set; }
        public string? DOCUMENT_CONTROL_ACCESS { get; set; }
    }
}
