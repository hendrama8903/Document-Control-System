using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Common.Models
{
    public class BaseModel
    {
        public DateTime? CREATED_DT { get; set; }
        public string? CREATED_BY { get; set; }
        public DateTime? CHANGED_DT { get; set; }
        public string? CHANGED_BY { get; set; }
    }
}
