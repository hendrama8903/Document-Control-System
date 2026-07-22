namespace DMS.Models.DB
{
    //Model khusus untuk laporan Document Masterlist - sengaja ramping (bukan reuse
    //DocumentMaintenance) karena FromSqlRaw<T> mewajibkan SEMUA properti T ada di hasil
    //query; DocumentMaintenance punya ~30 kolom yang tidak relevan untuk laporan ini.
    public class DocumentMasterlist
    {
        public int? DOCUMENT_TRANSACTION_ID { get; set; }
        public string? DOCUMENT_CODE { get; set; }
        public string? DOCUMENT_TRANSACTION_NAME { get; set; }
        public string? DOCUMENT_CATEGORY { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? CLASSIFIED_VAL { get; set; }
        public int? REVISION { get; set; }
        public string? STATUS { get; set; }
        public string? STATUS_DISPLAY { get; set; }
        public DateTime? DOCUMENT_DATE { get; set; }
        public string? ITEM_CHANGED { get; set; }
        public string? REASON { get; set; }
        public string? DOCUMENT_CREATOR { get; set; }
        public string? FILE_PATH { get; set; }
        public string? CREATED_BY { get; set; }
        public DateTime? CREATED_DT { get; set; }
        public string? CHANGED_BY { get; set; }
        public DateTime? CHANGED_DT { get; set; }
    }
}
