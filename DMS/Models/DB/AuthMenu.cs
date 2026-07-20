using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.DB
{
    [Table("TB_M_AUTH_MENU")]
    public partial class AuthMenu : BaseModel
    {
        public string? ROLE_ID { get; set; }
        public string? MENU_ID { get; set; }
    }
}
