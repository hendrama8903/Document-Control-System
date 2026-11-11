using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DMS.Models.DB
{
    [Table("TB_M_USER")]
    public partial class User : BaseModel
    {
        [Key]
        public string? USERNAME { get; set; }
        public string? REG_NO { get; set; }
        // Never sent to the browser (2026-11-11, Kadai No. 54): the stored value is reversible.
        // Still bound from the login and user forms (JsonIgnore only affects JSON).
        [JsonIgnore]
        public string? PASSWORD { get; set; }
        [JsonIgnore]
        public string? OLD_PASSWORD { get; set; }
        [JsonIgnore]
        public string? CONFIRM_PASSWORD { get; set; }

        /// <summary>The user has a local password (the forms ask for the old one when changing it).</summary>
        [NotMapped]
        public bool HAS_LOCAL_PASSWORD => !string.IsNullOrEmpty(PASSWORD);
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
        public string? SIGNATURE_PATH { get; set; }
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
