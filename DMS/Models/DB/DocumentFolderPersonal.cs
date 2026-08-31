using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    // Personal folder tree for My Documents / UserDashboard (request Hendra
    // 2026-08-29) - same self-referencing shape as DocumentFolder.cs/
    // TB_M_DOCUMENT_FOLDER (Document Control's global tree), but scoped by
    // USERNAME and fully separate: a document's placement here (see
    // TB_R_DOCUMENT_FOLDER_PERSONAL) is independent per user, unlike Document
    // Control's single global TB_R_DOCUMENT.FOLDER_ID column.
    [Table("TB_M_DOCUMENT_FOLDER_PERSONAL")]
    public class DocumentFolderPersonal : BaseModel
    {
        [Key]
        public int? FOLDER_ID { get; set; }
        public int? PARENT_ID { get; set; }
        public string? PARENT_NAME { get; set; }
        public string? FOLDER_NAME { get; set; }
        public string? USERNAME { get; set; }
        public int? DOCUMENT_COUNT { get; set; }
        public int? DELETE_FLAG { get; set; }
    }
}
