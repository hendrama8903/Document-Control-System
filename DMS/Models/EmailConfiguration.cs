namespace MDC.Models
{
    public class EmailConfiguration
    {
        // false: email tidak dikirim, hanya dicatat. Kosong dianggap true.
        public bool? enabled { get; set; }
        // Alamat dan nama pengirim. Alamat kosong berarti memakai username.
        public string? fromAddress { get; set; }
        public string? displayName { get; set; }
        public string? username { get; set; }
        public string? password { get; set; }
        public string? smtpServer { get; set; }
        public int? port { get; set; }
    }
}