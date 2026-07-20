using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DMS.Models.DB
{
    [Table("TB_M_EXCEL_TEMPLATE")]
    public class ExcelTemplateMaster
    {
        [Key]
        public int? TEMPLATE_ID { get; set; }
        public int? DOCUMENT_ID { get; set; }
        public int? SHEET_ORIENTATION { get; set; }
        public string? FIELD_NAME { get; set; }
        public int? ROW { get; set; }
        public int? COL { get; set; }
        public int? TYPE { get; set; }
        public int? MERGE_CELL_ROW { get; set; }
        public int? MERGE_CELL_COL { get; set; }
        public int? CHECK_SHEET_POSITION { get; set; }
        public int? SHEET_POSITION { get; set; }
    }
}
