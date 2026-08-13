using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    // Tidak mewarisi BaseModel dengan sengaja - TB_R_COPY_REQUEST_PRINT_LOG
    // adalah log audit write-once (tidak ada CREATED_BY/CHANGED_BY/DELETE_FLAG
    // di tabelnya, PRINTED_BY/PRINTED_DT sudah cukup).
    [Table("TB_R_COPY_REQUEST_PRINT_LOG")]
    public class CopyRequestPrintLog
    {
        [Key]
        public int? PRINT_LOG_ID { get; set; }
        public int? REQUEST_DETAIL_ID { get; set; }
        public string? COMPUTER_NAME { get; set; }
        public string? PRINTER_NAME { get; set; }
        public int? PAGE_COUNT { get; set; }
        public int? COPY_COUNT { get; set; }
        public int? TOTAL_SHEETS { get; set; }
        public int? PRINT_JOB_ID { get; set; }
        public string? PRINT_STATUS { get; set; }
        public string? ERROR_DETAIL { get; set; }
        public string? PRINTED_BY { get; set; }
        public DateTime? PRINTED_DT { get; set; }
    }
}
