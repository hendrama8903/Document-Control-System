namespace DMS.Models.PrintTrack
{
    public class PrintTrackLoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
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
