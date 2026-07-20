using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.DB
{
    [Table("TB_M_MENU")]
    public partial class Menu : BaseModel
    {
        [Key] 
        public string? MENU_ID { get; set; }
        public string? PARENT_ID { get; set; }
        public string? PARENT_NAME { get; set; }
        public string? MENU_NAME { get; set; }
        public string? MENU_ICON { get; set; }
        public string? MENU_URL { get; set; }
        public int? MENU_SEQ { get; set; }
    }
}
