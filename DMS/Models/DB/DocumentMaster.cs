using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.DB
{
    [Table("TB_M_DOCUMENT")]
    public class DocumentMaster : BaseModel
    {
        [Key]
        public int? DOCUMENT_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? LEVEL { get; set; }
        public string? LEVEL_VAL { get; set; }
        public string? FILE_PATH { get; set; }
    }
}
