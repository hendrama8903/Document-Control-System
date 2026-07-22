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

        // True for the row with the highest Revision within its Document Code group -
        // that one becomes the active TB_R_DOCUMENT row (with the uploaded file); every
        // other row in the group is a past revision carried into TB_R_DOCUMENT_HISTORY.
        public bool IsCurrentRevision { get; set; }

        // True when this Document Code already has an active document in the system - the
        // whole group is then treated as a history-only backfill (no row becomes "current",
        // since the real active document isn't part of this upload at all).
        public bool IsHistoryOnlyAppend { get; set; }

        public void AddError(string message)
        {
            Valid = false;
            Errors.Add(message);
        }
    }
}
