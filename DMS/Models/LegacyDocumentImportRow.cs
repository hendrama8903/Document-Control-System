namespace DMS.Models
{
    public class LegacyDocumentImportRow
    {
        public int RowNumber { get; set; }
        public string? FileName { get; set; }
        public string? DocumentCode { get; set; }
        public string? DocumentName { get; set; }
        public string? CategoryCode { get; set; }
        public string? DivisionCode { get; set; }
        public string? DepartmentCode { get; set; }
        public string? ClassificationCode { get; set; }
        public int? Revision { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string? DocumentCreator { get; set; }

        public bool Valid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();

        public int? ResolvedDocumentId { get; set; }
        public int? ResolvedLevelCode { get; set; }
        public int? ResolvedDepartmentId { get; set; }

        public void AddError(string message)
        {
            Valid = false;
            Errors.Add(message);
        }
    }
}
