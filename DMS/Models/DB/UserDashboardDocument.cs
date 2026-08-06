using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Dedicated read model for UserDashboard's grid search (sp_UserDashboard_Search).
    // Kept separate from DocumentControlMaintenance (shared by P4DMaintenanceRepo and
    // DocumentControlDashboardRepo via their own SPs) so extending this one with the
    // document-lifecycle/category/acknowledge-date columns below can't break those
    // other FromSqlRaw<DocumentControlMaintenance> calls, which require every mapped
    // property to have a matching column in their own SP's SELECT list.
    // Deliberately NOT mapped to a table via [Table] - see DBContext.OnModelCreating,
    // where it's configured as keyless and unmapped (ToView(null)) so EF doesn't treat
    // it as sharing TB_R_CTRL_DOCUMENT with DocumentControlMaintenance. It only exists
    // to shape FromSqlRaw results.
    public class UserDashboardDocument : BaseModel
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

        // Document master category (e.g. SOP / IK / PRO / PDM / FR), from TB_M_DOCUMENT
        // via TB_R_DOCUMENT.DOCUMENT_ID.
        public string? CATEGORY_CODE { get; set; }
        public string? CATEGORY_NAME { get; set; }

        // Document lifecycle status (Waiting Approval/Approved/Rejected/Deleted/Obsolete/
        // Published (Effective)) from TB_R_DOCUMENT.STATUS - distinct from the
        // registration/distribution workflow STATUS above.
        public string? DOC_STATUS { get; set; }
        public string? DOC_STATUS_VAL { get; set; }

        // Latest acknowledgement timestamp for this document/department, from
        // TB_R_PUBLISH_HISTORY.
        public DateTime? ACKNOWLEDGE_DT { get; set; }
    }
}
