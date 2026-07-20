using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.DB
{
    [Table("TB_M_ROLE")]
    public partial class Role : BaseModel
    {
        [Key]
        public string? ROLE_ID { get; set; }
        public string? ROLE_NAME { get; set; }
        public string? ROLE_DESC { get; set; }
    }
}
