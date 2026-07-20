using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.DB
{
    [Table("TB_R_LOG_D")]
    public class LogDetail : BaseModel
    {
        [Key]
        public long? PROCESS_ID { get; set; }
        [Key]
        public int? SEQ_NO { get; set; }
        public string? MESSAGE_TYPE { get; set; }
        public string? MESSAGE_CONTENT { get; set; }
        public string? LOCATION { get; set; }
    }
}