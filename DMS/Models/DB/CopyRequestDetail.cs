using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_R_COPY_REQUEST_D")]
    public class CopyRequestDetail : BaseModel
    {
        [Key]
        public int? REQUEST_DETAIL_ID { get; set; }
        public int? REQUEST_ID { get; set; }
        public int? LINE_NO { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? REVISION_NO { get; set; }
        public string? COPY_TYPE { get; set; }
        public string? COPY_TYPE_DISPLAY { get; set; }
        public int? COPY_QTY { get; set; }
        public string? REASON { get; set; }
        public string? COUNTERMEASURE { get; set; }
        public string? REMARKS { get; set; }
        // Token cetak sekali-pakai PrintTrack - lihat sp_CopyRequest_PrintLog_Insert.
        public string? PRINT_STATUS { get; set; }
        public string? PRINTED_BY { get; set; }
        public DateTime? PRINTED_DT { get; set; }
    }

    // Proyeksi khusus sp_CopyRequest_GetDetailByKey - SENGAJA dipisah dari
    // CopyRequestDetail (bukan ditambahi HEADER_STATUS/FILE_PATH di situ),
    // karena CopyRequestDetail dipakai bersama oleh sp_CopyRequest_SearchDetail
    // (dipakai grid web app) yang TIDAK mengembalikan 2 kolom itu - FromSqlRaw
    // EF Core mewajibkan semua kolom yang di-mapping ada di hasil query, jadi
    // kalau digabung satu class, SearchDetail langsung error "required column
    // not present" walau dipanggil dari SP yang beda. Pola sama seperti
    // UserDashboardDocument (lihat DBContext.cs) - SENGAJA TIDAK dikasih
    // [Table(...)], supaya EF tidak menganggap class ini berbagi tabel
    // dengan CopyRequestDetail (yang punya [Table] asli) tanpa relasi.
    public class CopyRequestDetailForPrint
    {
        public int? REQUEST_DETAIL_ID { get; set; }
        public int? REQUEST_ID { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? REVISION_NO { get; set; }
        public string? COPY_TYPE { get; set; }
        public int? COPY_QTY { get; set; }
        public string? PRINT_STATUS { get; set; }
        public string? PRINTED_BY { get; set; }
        public DateTime? PRINTED_DT { get; set; }
        public string? HEADER_STATUS { get; set; }
        public string? FILE_PATH { get; set; }
    }

    // Proyeksi sp_CopyRequest_PrintQueue - antrian cetak PrintTrack (desktop),
    // di-scope ke requester yang login. Keyless & tanpa [Table] sama seperti
    // CopyRequestDetailForPrint (request Hendra 2026-08-15).
    public class CopyRequestPrintQueueItem
    {
        public int? REQUEST_DETAIL_ID { get; set; }
        public int? REQUEST_ID { get; set; }
        public string? REQUEST_NO { get; set; }
        public int? LINE_NO { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? REVISION_NO { get; set; }
        public string? COPY_TYPE { get; set; }
        public string? COPY_TYPE_DISPLAY { get; set; }
        public int? COPY_QTY { get; set; }
        public string? APPROVED_BY { get; set; }
        public DateTime? APPROVED_DT { get; set; }
    }

    // Proyeksi sp_CopyRequest_PrintLogSearch - panel monitoring "Print" di
    // web CopyRequest/Index. DETAIL_PRINT_STATUS ('0'/'1') dan
    // LOG_PRINT_STATUS ('Success'/'Failed'/'Cancelled') SENGAJA dialiaskan
    // beda nama di SP - dua domain nilai yang berbeda, jangan disatukan
    // (request Hendra 2026-08-15).
    public class CopyRequestPrintLogItem
    {
        public int? REQUEST_DETAIL_ID { get; set; }
        public int? LINE_NO { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public int? COPY_QTY { get; set; }
        public string? DETAIL_PRINT_STATUS { get; set; }
        public int? PRINT_LOG_ID { get; set; }
        public string? COMPUTER_NAME { get; set; }
        public string? PRINTER_NAME { get; set; }
        public int? PAGE_COUNT { get; set; }
        public int? COPY_COUNT { get; set; }
        public int? TOTAL_SHEETS { get; set; }
        public string? LOG_PRINT_STATUS { get; set; }
        public string? ERROR_DETAIL { get; set; }
        public string? PRINTED_BY { get; set; }
        public DateTime? PRINTED_DT { get; set; }
    }
}
