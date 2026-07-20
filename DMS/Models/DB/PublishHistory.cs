using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_PUBLISH_HISTORY")]
    public partial class PublishHistory : BaseModel
    {
        [Key]
        public int? PUBLISH_HISTORY_ID { get; set; }
        public int? DOCUMENT_CTRL_ID { get; set; }
        public int? DEPARTMENT_ID { get; set; }
    }
}
