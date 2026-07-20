namespace DMS.Common.Models
{
    public class DBResult
    {
        public DBResult(bool status, string message)
        {
            this.status = status;
            this.message = message;
        }

        public DBResult(bool status, object data)
        {
            this.status = status;
            this.data = data;
        }

        public DBResult(bool status, string message, int returnId)
        {
            this.status = status;
            this.message = message;
            this.returnId = returnId;
        }

        public bool status { get; set; }
        public string message { get; set; }
        public int returnId { get; set; }
        public object data { get; set; }
    }
}
