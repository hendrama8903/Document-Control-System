using DMS.Common.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    // Virtual folder tree for DocumentControlDashboard (request Hendra 2026-08-28) -
    // self-referencing hierarchy, same PARENT_ID pattern as Menu.cs/TB_M_MENU.
    // PARENT_NAME/DOCUMENT_COUNT are read-only rollup columns from sp_DocumentFolder_Tree,
    // plain properties (not [NotMapped]) same as Menu.cs's PARENT_NAME - fine since this
    // entity is only ever materialized via FromSqlRaw, never SaveChanges.
    [Table("TB_M_DOCUMENT_FOLDER")]
    public class DocumentFolder : BaseModel
    {
        [Key]
        public int? FOLDER_ID { get; set; }
        public int? PARENT_ID { get; set; }
        public string? PARENT_NAME { get; set; }
        public string? FOLDER_NAME { get; set; }
        public int? DOCUMENT_COUNT { get; set; }
        public int? DELETE_FLAG { get; set; }
    }
}
