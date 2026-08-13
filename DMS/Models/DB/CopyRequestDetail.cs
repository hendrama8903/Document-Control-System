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
}
