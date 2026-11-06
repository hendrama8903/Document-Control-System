using DMS.Common.Models;

namespace DMS.Models.DB
{
    public partial class WorkflowDoc : BaseModel
    {
        public int? WORKFLOW_DOC_ID { get; set; }
        public int? DOCUMENT_LEVEL { get; set; }
        public int? CREATOR_LEVEL { get; set; }
        // Kosong = alur default per level; terisi = alur khusus Document Category (2026-11-06)
        public int? DOCUMENT_ID { get; set; }
        public int? WORKFLOW_SEQ { get; set; }
        public int? POSITION_ID { get; set; }
        public string? POSITION_NAME { get; set; }
        public string? LABEL { get; set; }
        // Approver wajib: kalau tidak ditemukan dokumen tidak bisa dibuat (2026-11-06)
        public bool? IS_MANDATORY { get; set; }
    }
    public partial class WorkflowDocArr : BaseModel
    {
        public int? WORKFLOW_DOC_ID { get; set; }
        public int? DOCUMENT_LEVEL { get; set; }
        public int? CREATOR_LEVEL { get; set; }
        public int? DOCUMENT_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public int? WORKFLOW_SEQ { get; set; }
        public string? POSITION_ID_ARR { get; set; }
        public string? POSITION_NAME { get; set; }
        public string? CREATOR_NAME { get; set; }
        public string? NOTES { get; set; }
    }

    /// <summary>Satu langkah pratinjau approval chain (sp_WorkflowDoc_Preview).</summary>
    public class WorkflowPreviewStep
    {
        public string STATUS { get; set; }              // OK / AUTO / SKIP / MISSING / NO_CREATOR / NO_WORKFLOW
        public string? CREATOR_POSITION { get; set; }
        public bool IS_CATEGORY_WORKFLOW { get; set; }
        public int? WORKFLOW_SEQ { get; set; }
        public int? ACTUAL_SEQ { get; set; }
        public int? POSITION_ID { get; set; }
        public string? POSITION_NAME { get; set; }
        public string? LABEL { get; set; }
        public bool IS_MANDATORY { get; set; }
        public string? APPROVER { get; set; }
        public string? APPROVER_NAME { get; set; }
    }

    /// <summary>Jabatan yang bisa muncul di approval chain sebuah kategori (sp_WorkflowDoc_CategoryPositions).</summary>
    public class WorkflowCategoryPositions
    {
        public Dictionary<int, string> Positions { get; } = new Dictionary<int, string>();
        public int MaxSteps { get; set; }
        public bool HasWorkflow => Positions.Count > 0;
    }
}
