namespace DMS.Models.DB
{
    public class ApiResponse
    {
        public string message { get; set; } = string.Empty;
        public bool status { get; set; } = false;
        public object? data { get; set; }
    }
}
