using DMS.Common.Models;

namespace DMS.Models.DB
{
    public partial class Workflow : BaseModel
    {
        public int? WORKFLOW_ID { get; set; }
        public string? WORKFLOW_CODE { get; set; }
        public string? WORKFLOW_NAME { get; set; }
        public int? WORKFLOW_SEQ { get; set; }
        public string? APPROVER { get; set; }
        public string? APPROVER_NAME { get; set; }
    }
}