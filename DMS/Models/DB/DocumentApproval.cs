using DMS.Common.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
//Recommit DINA

namespace DMS.Models.DB
{
    [Table("TB_R_DOCUMENT_WF")]
    public class DocumentApproval
    {
        [Key]
        public int? SEQUENCE { get; set; }
        public int? DOCUMENT_ID { get; set; }
        public string? DOCUMENT_NO { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? APPROVER_NAME { get; set; }
        public string? STATUS { get; set; }
        public string? ACTION { get; set; }

    }
}
