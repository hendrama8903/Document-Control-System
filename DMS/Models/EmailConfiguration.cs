namespace MDC.Models
{
    public class EmailConfiguration
    {
        public string? username { get; set; }
        public string? password { get; set; }
        public string? smtpServer { get; set; }
        public int? port { get; set; }
    }
}