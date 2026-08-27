using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    // TIDAK extend BaseModel (beda dari kebanyakan model lain di sini) - tabel
    // TB_R_DOCUMENT_RELATED_DIVISION memang tidak punya kolom CHANGED_BY/
    // CHANGED_DT, cuma CREATED_BY/CREATED_DT. FromSqlRaw<T> mewajibkan SEMUA
    // properti (termasuk yang diwariskan) ada di hasil SELECT SP - extend
    // BaseModel di sini bikin EF minta CHANGED_BY yang tidak pernah dipilih
    // (request Hendra 2026-08-20, ditemukan saat testing - pola bug yang sama
    // seperti yang sudah didokumentasikan sebelumnya soal FromSqlRaw).
    [Table("TB_R_DOCUMENT_RELATED_DIVISION")]
    public partial class DocumentRelatedDivision
    {
        [Key]
        public int? RELATED_DIVISION_ID { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DIVISION_CODE { get; set; }
        // MAIN_PIC / RELATED / NOTE_RELATED (request Hendra 2026-08-20) - lihat
        // DocumentMaintenance_RelatedDivision_Role_Migration.sql. Cuma RELATED
        // yang wajib Acknowledge; MAIN_PIC boleh lebih dari satu divisi per
        // dokumen dan dipilih manual (tidak lagi otomatis = TB_R_DOCUMENT.DIVISION).
        public string? DIVISION_ROLE { get; set; }
        public string? CREATED_BY { get; set; }
        public DateTime? CREATED_DT { get; set; }
        public bool? ACKNOWLEDGED_FLAG { get; set; }
        public string? ACKNOWLEDGED_BY { get; set; }
        public DateTime? ACKNOWLEDGED_DT { get; set; }

        // BUKAN [NotMapped] meski bukan kolom asli tabel ini (join dari
        // sp_DocumentMaintenance_GetRelatedDivisionAckStatus) - [NotMapped]
        // justru bikin EF MENGABAIKAN kolom ini dari hasil FromSqlRaw sama
        // sekali (nilainya selalu null), bukan mewajibkannya seperti properti
        // lain - kebalikan dari bug CHANGED_BY di atas, ditemukan di testing
        // yang sama. Pola yang sudah dipakai ApprovalDetail.APPROVER_NAME dkk.
        public string? DIVISION_NAME { get; set; }
        public string? HEAD_USERNAME { get; set; }
        public string? HEAD_FULL_NAME { get; set; }
    }
}
