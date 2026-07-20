using DMS.Common.Models;

namespace DMS.Models.DB
{
    public partial class WorkflowDoc : BaseModel
    {
        public int? WORKFLOW_DOC_ID { get; set; }
        public int? DOCUMENT_LEVEL { get; set; }
        public int? CREATOR_LEVEL { get; set; }
        public int? WORKFLOW_SEQ { get; set; }
        public int? POSITION_ID { get; set; }
        public string? POSITION_NAME { get; set; }
        
    }
    public partial class WorkflowDocArr : BaseModel
    {
        public int? WORKFLOW_DOC_ID { get; set; }
        public int? DOCUMENT_LEVEL { get; set; }
        public int? CREATOR_LEVEL { get; set; }
        public int? WORKFLOW_SEQ { get; set; }
        public string? POSITION_ID_ARR { get; set; }
        public string? POSITION_NAME { get; set; }
        public string? CREATOR_NAME { get; set; }
    }
}