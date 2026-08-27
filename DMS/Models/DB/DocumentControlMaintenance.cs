using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
//Recommit DINA

namespace DMS.Models.DB
{
    [Table("TB_R_CTRL_DOCUMENT")]
    public class DocumentControlMaintenance : BaseModel
    {
        [Key]
        public int? DOCUMENT_CTRL_ID { get; set; }
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_NAME { get; set; }
        public string? DOCUMENT_TYPE { get; set; }
        public string? PROCESS_CODE { get; set; }
        public string? COMPANY_CODE { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? ITEM_CHANGED { get; set; }
        public string? REASON { get; set; }
        public string? EXTERNAL_FLAG { get; set; }
        public string? REFERENCE_NO { get; set; }
        public string? SOURCE { get; set; }
        public DateTime? DOCUMENT_DATE { get; set; }
        public string? FILE_PATH { get; set; }
        public string? STATUS { get; set; }
        public string? STATUS_VAL { get; set; }
        public int? REVISION { get; set; }
        public int? APPROVAL_ID { get; set; }
        public int? DELETE_FLAG { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DISTRIBUTION { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public int? CLASSIFIED { get; set; }
        public string? CLASSIFIED_VAL { get; set; }
        public string? DOCUMENT_YEAR { get; set; }
        public DateTime? NEXT_REVIEW_DATE { get; set; }
        public string? LOCATION { get; set; }
        public string? IS_APPROVED { get; set; }
        public string? REMARKS { get; set; }
        public string? ACTION { get; set; }
        public int? OPERATION_TYPE { get; set; }
        public string? OPERATION_TYPE_VAL { get; set; }
        public int? PUBLISH_FLAG { get; set; }

        // ACK_DONE dari ACK_TOTAL department tujuan distribusi sudah klik Accepted
        // di UserDashboard (TB_R_PUBLISH_HISTORY) - dipakai untuk badge "Waiting
        // Acceptance"/"Accepted" di grid P4D (request Hendra 2026-08-13).
        public int? ACK_TOTAL { get; set; }
        public int? ACK_DONE { get; set; }
        public int? DIVISION_COUNT { get; set; }
        public int? PENDING_DISTRIBUTION_COUNT { get; set; }

        // Dihitung di C# (bukan dari sp_P4DMaintenance_Search) supaya SP yang
        // dipakai bersama P4DMaintenanceController tidak perlu diubah - lihat
        // DocumentSubmissionController.SearchP4DPendingDocuments. Merujuk ke
        // kode DOC_SUBMISSION_CATEGORY (Pedoman/SOP-EIS-IK-OPL-ACUAN/Prosedur/
        // Checksheet-Form/Lain Lain) berdasarkan TB_M_DOCUMENT.LEVEL dokumen.
        [NotMapped]
        public string? DOC_CATEGORY_CODE { get; set; }
    }
}
