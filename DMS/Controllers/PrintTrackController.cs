using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.PrintTrack;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    // Endpoint API untuk PrintTrack (desktop app terpisah) - menumpang alur
    // Copy Request (QCD/FR-QMS-00/028) yang sudah ada di CopyRequestController,
    // menambahkan bagian yang belum ada di situ: eksekusi cetak fisik & audit
    // jumlah kertas terpakai (lihat SQL/CopyRequest_AddPrintTracking.sql).
    // Auth pakai JWT Bearer (lihat PrintTrackAuthController), BUKAN session
    // cookie seperti controller MVC lain - desktop tidak ikut siklus browser.
    [Authorize]
    [ApiController]
    [Route("api/printtrack")]
    public class PrintTrackController : Controller
    {
        private readonly DBContext db;
        private readonly IWebHostEnvironment environment;
        private CopyRequestRepo copyRequestRepo = CopyRequestRepo.Instance;
        private P4DMaintenanceRepo p4DMaintenanceRepo = P4DMaintenanceRepo.Instance;
        private LoginRepo loginRepo = LoginRepo.Instance;

        public PrintTrackController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            this.environment = environment;
        }

        private string CurrentUsername => User?.Identity?.Name ?? string.Empty;

        // Document picker untuk PrintTrack - dokumen yang sudah Received/
        // Published (STATUS 2 atau 5), sama seperti
        // CopyRequestController.SearchApprovedDocuments, di-scope ke
        // department user yang login.
        [HttpGet("documents/approved")]
        public IActionResult ApprovedDocuments()
        {
            User loginUser = loginRepo.GetUserByUsername(new User { USERNAME = CurrentUsername }, db);
            if (loginUser == null)
            {
                return Json(new { status = false, message = "User not found" });
            }

            var searchData = new DocumentControlMaintenance
            {
                DEPARTMENT_ID = loginUser.DEPARTMENT_ID,
                STATUS = "2,5",
                OPERATION_TYPE = 1
            };

            IList<DocumentControlMaintenance> dataList = p4DMaintenanceRepo.Search(searchData, CurrentUsername, db, null, null).ToList();

            return Json(new { status = true, data = dataList });
        }

        // Bikin Copy Request baru dan langsung Submit (status '1' = Waiting
        // Approval) - PrintTrack tidak punya konsep Draft, request selalu
        // langsung diajukan begitu dikirim dari desktop.
        [HttpPost("copyrequest")]
        public IActionResult CreateRequest([FromBody] CreateCopyRequestPayload payload)
        {
            if (payload == null || payload.Lines == null || payload.Lines.Count == 0)
            {
                return Json(new { status = false, message = "At least one document line is required" });
            }

            User loginUser = loginRepo.GetUserByUsername(new User { USERNAME = CurrentUsername }, db);
            if (loginUser == null)
            {
                return Json(new { status = false, message = "User not found" });
            }

            CopyRequest header = new CopyRequest
            {
                DIVISION = loginUser.DIVISION,
                DEPARTMENT_ID = loginUser.DEPARTMENT_ID,
                SECTION_CODE = payload.SectionCode ?? loginUser.SECTION_CODE,
                REQUEST_DATE = DateTime.Now,
                DOC_CATEGORY = payload.DocCategory,
                REMARK = payload.Remark,
                REQUESTED_BY = CurrentUsername
            };

            DBResult insertResult = copyRequestRepo.Insert(header, CurrentUsername, db);
            if (!insertResult.status)
            {
                return Json(new { status = false, message = insertResult.message });
            }

            int requestId = insertResult.returnId;

            foreach (CreateCopyRequestLine line in payload.Lines)
            {
                copyRequestRepo.InsertDetail(new CopyRequestDetail
                {
                    REQUEST_ID = requestId,
                    DOCUMENT_TRANSACTION_ID = line.DocumentTransactionId,
                    DOCUMENT_CODE = line.DocumentCode,
                    DOCUMENT_NAME = line.DocumentName,
                    REVISION_NO = line.RevisionNo,
                    COPY_TYPE = line.CopyType,
                    COPY_QTY = line.CopyQty,
                    REASON = line.Reason,
                    COUNTERMEASURE = line.Countermeasure,
                    REMARKS = line.Remarks
                }, CurrentUsername, db);
            }

            DBResult submitResult = copyRequestRepo.Submit(requestId, CurrentUsername, db);
            if (!submitResult.status)
            {
                return Json(new { status = false, message = submitResult.message, requestId });
            }

            CopyRequest saved = copyRequestRepo.GetByKey(new CopyRequest { REQUEST_ID = requestId }, db);

            return Json(new { status = true, requestId, requestNo = saved?.REQUEST_NO, message = submitResult.message });
        }

        // Polling status approval. Alternatif lebih responsif: subscribe
        // NotificationsHub (SignalR, sudah ada di app) untuk push begitu QMS
        // approve/reject, supaya desktop tidak perlu polling terus-menerus -
        // di luar cakupan endpoint ini.
        [HttpGet("copyrequest/{requestId}/status")]
        public IActionResult Status(int requestId)
        {
            CopyRequest header = copyRequestRepo.GetByKey(new CopyRequest { REQUEST_ID = requestId }, db);
            if (header == null)
            {
                return NotFound(new { status = false, message = "Request not found" });
            }

            IList<CopyRequestDetail> details = copyRequestRepo.SearchDetail(new CopyRequestDetail { REQUEST_ID = requestId }, db, null, null);

            return Json(new
            {
                status = true,
                data = new
                {
                    requestId = header.REQUEST_ID,
                    requestNo = header.REQUEST_NO,
                    status = header.STATUS,
                    approvedBy = header.APPROVED_BY,
                    approvedDt = header.APPROVED_DT,
                    approvalRemark = header.APPROVAL_REMARK,
                    details = details.Select(d => new
                    {
                        requestDetailId = d.REQUEST_DETAIL_ID,
                        documentCode = d.DOCUMENT_CODE,
                        documentName = d.DOCUMENT_NAME,
                        copyQty = d.COPY_QTY,
                        printStatus = d.PRINT_STATUS
                    })
                }
            });
        }

        // Ambil file dokumen untuk dicetak. Syarat dicek ulang di sini (bukan
        // cuma dipercaya dari desktop): parent request harus Approved
        // (STATUS='2') dan token belum pernah dipakai (PRINT_STATUS='0') -
        // sama seperti validasi di sp_CopyRequest_PrintLog_Insert.
        [HttpGet("copyrequest/detail/{requestDetailId}/document")]
        public IActionResult Document(int requestDetailId)
        {
            CopyRequestDetailForPrint detail = copyRequestRepo.GetDetailByKey(requestDetailId, db);
            if (detail == null)
            {
                return NotFound(new { status = false, message = "Request detail not found" });
            }

            if (detail.HEADER_STATUS != "2")
            {
                return StatusCode(403, new { status = false, message = "Parent request is not Approved" });
            }

            if (detail.PRINT_STATUS == "1")
            {
                return StatusCode(403, new { status = false, message = "This document has already been printed - submit a new request to reprint" });
            }

            if (string.IsNullOrEmpty(detail.FILE_PATH))
            {
                return NotFound(new { status = false, message = "No file attached to this document" });
            }

            // Konkatenasi persis sama seperti DocumentMasterController.DownloadAttachment -
            // FILE_PATH disimpan sudah dengan leading slash relatif ke wwwroot.
            string fullPath = environment.WebRootPath + detail.FILE_PATH;
            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new { status = false, message = "File not found on server" });
            }

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
            string fileName = Path.GetFileName(fullPath);

            return File(bytes, "application/octet-stream", fileName);
        }

        // Lapor hasil eksekusi cetak (sukses ataupun gagal). Validasi &
        // logika kunci token sepenuhnya ada di sp_CopyRequest_PrintLog_Insert,
        // bukan di sini - lihat komentar SP itu.
        [HttpPost("copyrequest/detail/{requestDetailId}/print-log")]
        public IActionResult PrintLog(int requestDetailId, [FromBody] PrintLogPayload payload)
        {
            DBResult result = copyRequestRepo.InsertPrintLog(new CopyRequestPrintLog
            {
                REQUEST_DETAIL_ID = requestDetailId,
                COMPUTER_NAME = payload?.ComputerName,
                PRINTER_NAME = payload?.PrinterName,
                PAGE_COUNT = payload?.PageCount,
                COPY_COUNT = payload?.CopyCount,
                PRINT_JOB_ID = payload?.PrintJobId,
                PRINT_STATUS = payload?.PrintStatus ?? "Success",
                ERROR_DETAIL = payload?.ErrorMessage
            }, CurrentUsername, db);

            return Json(result);
        }
    }
}
