using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.DB
{
    [Table("TB_M_SYSTEM")]
    public partial class MSystem : BaseModel
    {
        public string? SYSTEM_TYPE { get; set; }
        public string? SYSTEM_CODE { get; set; }
        public string? SYSTEM_VALUE { get; set; }
        public string? SYSTEM_CODE_VALUE { get; set; }
        public int? STATUS { get; set; }
    }
}
