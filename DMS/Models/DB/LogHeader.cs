using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.DB
{
    [Table("TB_R_LOG_H")]
    public class LogHeader : BaseModel
    {
        [Key]
        public long? PROCESS_ID { get; set; }
        public string? MODULE { get; set; }
        public string? FUNCTION { get; set; }
        public DateTime? START_DT { get; set; }
        public DateTime? END_DT { get; set; }
        public string? PROCESS_STATUS { get; set; }
    }
}
