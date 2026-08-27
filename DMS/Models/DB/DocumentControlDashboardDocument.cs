using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Dedicated read model for the Document Control master register (backed by
    // sp_P4DMaintenance_Search). Kept separate from the shared DocumentControlMaintenance
    // entity (used as-is by P4DMaintenanceController's own Search, and by other
    // FromSqlRaw<DocumentControlMaintenance> callers) so extending it with the
    // category/lifecycle-status/owner/acknowledge-ratio columns below can't break those
    // other call sites - same reasoning as UserDashboardDocument.cs. Deliberately NOT
    // mapped to a table via [Table] - see DBContext.OnModelCreating (HasNoKey + ToView(null)).
    public class DocumentControlDashboardDocument : BaseModel
    {
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

        // Document master category (e.g. SOP / IK / PRO / PDM / FR), from TB_M_DOCUMENT.
        public string? CATEGORY_CODE { get; set; }
        public string? CATEGORY_NAME { get; set; }

        // Document lifecycle status (Waiting Approval/Approved/Rejected/Deleted/Obsolete/
        // Published (Effective)) from TB_R_DOCUMENT.STATUS.
        public string? DOC_STATUS { get; set; }
        public string? DOC_STATUS_VAL { get; set; }

        // Document owner display name (TB_M_USER.FULL_NAME, falls back to username).
        public string? OWNER_FULL_NAME { get; set; }

        // Acknowledgement completion ratio for this document transaction: ACK_DONE of
        // ACK_TOTAL registration/distribution entries have a matching TB_R_PUBLISH_HISTORY row.
        public int? ACK_TOTAL { get; set; }
        public int? ACK_DONE { get; set; }
        public int? DIVISION_COUNT { get; set; }
        public int? PENDING_DISTRIBUTION_COUNT { get; set; }
    }
}
