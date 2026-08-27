using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class CopyRequestRepo : BaseRepo
    {
        private CopyRequestRepo() { }

        #region Singleton
        private static CopyRequestRepo instance = null;
        public static CopyRequestRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new CopyRequestRepo();
                }
                return instance;
            }
        }
        #endregion

        // Scoping department/QMS-lihat-semua sekarang ditentukan di SP dari
        // @USERNAME (lihat sp_CopyRequest_Search - pola sama sp_P4DMaintenance_Search),
        // BUKAN dikirim sudah ter-resolve dari C# seperti sebelumnya - supaya
        // tim QMS (department ber-DOCUMENT_CONTROL_ACCESS=1) otomatis lihat
        // semua department, bukan cuma department mereka sendiri.
        public IList<CopyRequest> Search(CopyRequest data, string loginUser, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@REQUEST_NO", CheckNullValue(data.REQUEST_NO) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@USERNAME", CheckNullValue(loginUser) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_CopyRequest_Search] @REQUEST_NO, @STATUS, @USERNAME, @PageNumber, @PageSize";
            IList<CopyRequest> Result = db.CopyRequest.FromSqlRaw<CopyRequest>(query, param.ToArray()).ToList();

            return Result;
        }

        public CopyRequest GetByKey(CopyRequest data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@REQUEST_ID", CheckNullValue(data.REQUEST_ID) )
            };

            string query = "EXEC [dbo].[sp_CopyRequest_GetByKey] @REQUEST_ID";
            CopyRequest Result = db.CopyRequest.FromSqlRaw<CopyRequest>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(CopyRequest data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");
            SqlParameter returnId = CreateSqlParameterOutputString("@RETURN_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@REQUEST_DATE", CheckNullValue(data.REQUEST_DATE) ),
                new SqlParameter ( "@DOC_CATEGORY", CheckNullValue(data.DOC_CATEGORY) ),
                new SqlParameter ( "@REMARK", CheckNullValue(data.REMARK) ),
                new SqlParameter ( "@REQUESTED_BY", CheckNullValue(data.REQUESTED_BY) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg,
                returnId
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_Insert] @DIVISION, @DEPARTMENT_ID, @SECTION_CODE, @REQUEST_DATE, @DOC_CATEGORY, " +
                "@REMARK, @REQUESTED_BY, @LOGIN_USER, @RETURN_MSG OUTPUT, @RETURN_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString(), Convert.ToInt32(returnId.Value));
            return result;
        }

        public DBResult Update(CopyRequest data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_ID", CheckNullValue(data.REQUEST_ID) ),
                new SqlParameter ( "@SECTION_CODE", CheckNullValue(data.SECTION_CODE) ),
                new SqlParameter ( "@REQUEST_DATE", CheckNullValue(data.REQUEST_DATE) ),
                new SqlParameter ( "@DOC_CATEGORY", CheckNullValue(data.DOC_CATEGORY) ),
                new SqlParameter ( "@REMARK", CheckNullValue(data.REMARK) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_Update] @REQUEST_ID, @SECTION_CODE, @REQUEST_DATE, @DOC_CATEGORY, @REMARK, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(CopyRequest data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_ID", CheckNullValue(data.REQUEST_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_Delete] @REQUEST_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UpdateStatus(CopyRequest data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_ID", CheckNullValue(data.REQUEST_ID) ),
                new SqlParameter ( "@STATUS", CheckNullValue(data.STATUS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_UpdateStatus] @REQUEST_ID, @STATUS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult ApproveReject(int requestId, bool isApproved, string remark, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_ID", requestId ),
                new SqlParameter ( "@IS_APPROVED", isApproved ? "Y" : "N" ),
                new SqlParameter ( "@REMARK", CheckNullValue(remark) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_ApproveReject] @REQUEST_ID, @IS_APPROVED, @REMARK, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Submit(int requestId, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_ID", requestId ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_Submit] @REQUEST_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<CopyRequestDetail> SearchDetail(CopyRequestDetail data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@REQUEST_ID", CheckNullValue(data.REQUEST_ID) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_CopyRequest_SearchDetail] @REQUEST_ID, @PageNumber, @PageSize";
            IList<CopyRequestDetail> Result = db.CopyRequestDetail.FromSqlRaw<CopyRequestDetail>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult InsertDetail(CopyRequestDetail data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_ID", CheckNullValue(data.REQUEST_ID) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@REVISION_NO", CheckNullValue(data.REVISION_NO) ),
                new SqlParameter ( "@COPY_TYPE", CheckNullValue(data.COPY_TYPE) ),
                new SqlParameter ( "@COPY_QTY", CheckNullValue(data.COPY_QTY) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@COUNTERMEASURE", CheckNullValue(data.COUNTERMEASURE) ),
                new SqlParameter ( "@REMARKS", CheckNullValue(data.REMARKS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_InsertDetail] @REQUEST_ID, @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DOCUMENT_NAME, @REVISION_NO, " +
                "@COPY_TYPE, @COPY_QTY, @REASON, @COUNTERMEASURE, @REMARKS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UpdateDetail(CopyRequestDetail data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_DETAIL_ID", CheckNullValue(data.REQUEST_DETAIL_ID) ),
                new SqlParameter ( "@DOCUMENT_TRANSACTION_ID", CheckNullValue(data.DOCUMENT_TRANSACTION_ID) ),
                new SqlParameter ( "@DOCUMENT_CODE", CheckNullValue(data.DOCUMENT_CODE) ),
                new SqlParameter ( "@DOCUMENT_NAME", CheckNullValue(data.DOCUMENT_NAME) ),
                new SqlParameter ( "@REVISION_NO", CheckNullValue(data.REVISION_NO) ),
                new SqlParameter ( "@COPY_TYPE", CheckNullValue(data.COPY_TYPE) ),
                new SqlParameter ( "@COPY_QTY", CheckNullValue(data.COPY_QTY) ),
                new SqlParameter ( "@REASON", CheckNullValue(data.REASON) ),
                new SqlParameter ( "@COUNTERMEASURE", CheckNullValue(data.COUNTERMEASURE) ),
                new SqlParameter ( "@REMARKS", CheckNullValue(data.REMARKS) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_UpdateDetail] @REQUEST_DETAIL_ID, @DOCUMENT_TRANSACTION_ID, @DOCUMENT_CODE, @DOCUMENT_NAME, @REVISION_NO, " +
                "@COPY_TYPE, @COPY_QTY, @REASON, @COUNTERMEASURE, @REMARKS, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Dipakai PrintTrack (desktop) - lookup satu baris dokumen langsung by
        // REQUEST_DETAIL_ID (beda dari SearchDetail yang butuh REQUEST_ID),
        // sekalian bawa HEADER_STATUS & FILE_PATH untuk validasi token cetak.
        public CopyRequestDetailForPrint GetDetailByKey(int requestDetailId, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@REQUEST_DETAIL_ID", requestDetailId )
            };

            string query = "EXEC [dbo].[sp_CopyRequest_GetDetailByKey] @REQUEST_DETAIL_ID";
            CopyRequestDetailForPrint Result = db.CopyRequestDetailForPrint.FromSqlRaw<CopyRequestDetailForPrint>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        // Dipakai PrintTrack (desktop) setelah eksekusi cetak - lihat
        // sp_CopyRequest_PrintLog_Insert untuk validasi & logika kunci token.
        public DBResult InsertPrintLog(CopyRequestPrintLog data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_DETAIL_ID", CheckNullValue(data.REQUEST_DETAIL_ID) ),
                new SqlParameter ( "@COMPUTER_NAME", CheckNullValue(data.COMPUTER_NAME) ),
                new SqlParameter ( "@PRINTER_NAME", CheckNullValue(data.PRINTER_NAME) ),
                new SqlParameter ( "@PAGE_COUNT", CheckNullValue(data.PAGE_COUNT) ),
                new SqlParameter ( "@COPY_COUNT", CheckNullValue(data.COPY_COUNT) ),
                new SqlParameter ( "@PRINT_JOB_ID", CheckNullValue(data.PRINT_JOB_ID) ),
                new SqlParameter ( "@PRINT_STATUS", CheckNullValue(data.PRINT_STATUS) ),
                new SqlParameter ( "@ERROR_DETAIL", CheckNullValue(data.ERROR_DETAIL) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_PrintLog_Insert] @REQUEST_DETAIL_ID, @COMPUTER_NAME, @PRINTER_NAME, @PAGE_COUNT, @COPY_COUNT, " +
                "@PRINT_JOB_ID, @PRINT_STATUS, @ERROR_DETAIL, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult DeleteDetail(CopyRequestDetail data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@REQUEST_DETAIL_ID", CheckNullValue(data.REQUEST_DETAIL_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_CopyRequest_DeleteDetail] @REQUEST_DETAIL_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        // Antrian cetak PrintTrack (desktop) - baris Approved yang token
        // cetaknya belum dipakai, di-scope ke requester yang login (request
        // Hendra 2026-08-15). Menggantikan alur lama di mana desktop
        // menyimpan sendiri daftar requestId yang pernah dibuatnya.
        public IList<CopyRequestPrintQueueItem> SearchPrintQueue(string username, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", CheckNullValue(username) )
            };

            string query = "EXEC [dbo].[sp_CopyRequest_PrintQueue] @USERNAME";
            IList<CopyRequestPrintQueueItem> Result = db.CopyRequestPrintQueueItem.FromSqlRaw<CopyRequestPrintQueueItem>(query, param.ToArray()).ToList();

            return Result;
        }

        // Panel monitoring "Print" di web CopyRequest/Index - satu baris per
        // percobaan cetak (termasuk yang gagal) plus dokumen yang belum
        // pernah dicetak sama sekali (request Hendra 2026-08-15).
        public IList<CopyRequestPrintLogItem> SearchPrintLog(int requestId, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@REQUEST_ID", requestId )
            };

            string query = "EXEC [dbo].[sp_CopyRequest_PrintLogSearch] @REQUEST_ID";
            IList<CopyRequestPrintLogItem> Result = db.CopyRequestPrintLogItem.FromSqlRaw<CopyRequestPrintLogItem>(query, param.ToArray()).ToList();

            return Result;
        }
    }
}
