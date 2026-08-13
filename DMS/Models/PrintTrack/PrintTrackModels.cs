namespace DMS.Models.PrintTrack
{
    public class PrintTrackLoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }

    public class CreateCopyRequestPayload
    {
        public string? SectionCode { get; set; }
        public string? DocCategory { get; set; }
        public string? Remark { get; set; }
        public List<CreateCopyRequestLine> Lines { get; set; } = new();
    }

    public class CreateCopyRequestLine
    {
        public int? DocumentTransactionId { get; set; }
        public string? DocumentCode { get; set; }
        public string? DocumentName { get; set; }
        public int? RevisionNo { get; set; }
        public string? CopyType { get; set; }
        public int? CopyQty { get; set; }
        public string? Reason { get; set; }
        public string? Countermeasure { get; set; }
        public string? Remarks { get; set; }
    }

    // PrintStatus: "Success" | "Failed" | "Cancelled" - lihat
    // sp_CopyRequest_PrintLog_Insert, hanya "Success" yang mengunci token.
    public class PrintLogPayload
    {
        public string? ComputerName { get; set; }
        public string? PrinterName { get; set; }
        public int? PageCount { get; set; }
        public int? CopyCount { get; set; }
        public int? PrintJobId { get; set; }
        public string PrintStatus { get; set; } = "Success";
        public string? ErrorMessage { get; set; }
    }
}
