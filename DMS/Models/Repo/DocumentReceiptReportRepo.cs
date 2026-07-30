using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class DocumentReceiptReportRepo : BaseRepo
    {
        private DocumentReceiptReportRepo() { }

        #region Singleton
        private static DocumentReceiptReportRepo instance = null;
        public static DocumentReceiptReportRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DocumentReceiptReportRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<DocumentReceiptReport> Search(DocumentReceiptReport data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_DocumentReceiptReport_Search] @DOCUMENT_CODE, @DEPARTMENT_ID, @PageNumber, @PageSize";
            IList<DocumentReceiptReport> Result = db.DocumentReceiptReport.FromSqlRaw<DocumentReceiptReport>(query, param.ToArray()).ToList();

            return Result;
        }
    }
}
