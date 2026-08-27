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

        public PrintTrackController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            this.environment = environment;
        }

        private string CurrentUsername => User?.Identity?.Name ?? string.Empty;

        // Antrian cetak - baris Copy Request yang sudah Approved dan token
        // cetaknya belum dipakai, milik requester yang login (setiap orang
        // cetak requestnya sendiri di PC masing-masing). Pengajuan & approval
        // sudah 100% di web (CopyRequestController) - desktop murni alat
        // eksekusi cetak (request Hendra 2026-08-15, menggantikan endpoint
        // lama ApprovedDocuments/CreateRequest/Status yang dulu dipakai
        // desktop untuk mengajukan request sendiri).
        [HttpGet("copyrequest/queue")]
        public IActionResult Queue()
        {
            IList<CopyRequestPrintQueueItem> dataList = copyRequestRepo.SearchPrintQueue(CurrentUsername, db);
            return Json(new { status = true, data = dataList });
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
