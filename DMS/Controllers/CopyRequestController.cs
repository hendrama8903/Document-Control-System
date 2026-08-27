using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using MDC.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    // Modul "Controlled Copy Request" (QCD/FR-QMS-00/028) - permintaan salinan
    // dokumen yang SUDAH terdaftar & approved di P4D, lewat form + approval
    // SATU LANGKAH oleh QMS (bukan registrasi dokumen baru, itu domain
    // DocumentSubmission). Pola header+detail (bukan approval-nya) tetap
    // mirror DocumentSubmissionController.
    //
    // Menggantikan tombol "Request Document" satu-klik lama di UserDashboard -
    // request user 2026-08-11.
    public class CopyRequestController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;

        public CopyRequestController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private CopyRequestRepo copyRequestRepo = CopyRequestRepo.Instance;
        private P4DMaintenanceRepo p4DMaintenanceRepo = P4DMaintenanceRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/CopyRequest/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("COPYREQUEST-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("COPYREQUEST-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("COPYREQUEST-DELETE");
            ViewData["Submit"] = HttpContext.Session.GetString("functionList").Contains("COPYREQUEST-SUBMIT");
            ViewData["Print"] = HttpContext.Session.GetString("functionList").Contains("COPYREQUEST-PRINT");
            ViewData["Approve"] = HttpContext.Session.GetString("functionList").Contains("COPYREQUEST-APPROVE");

            ViewData["Title"] = "Print Request";

            return View();
        }

        public JsonResult GetByKey(CopyRequest data)
        {
            try
            {
                CopyRequest result = copyRequestRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(CopyRequest data, bool initialMode)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int pageNumber = skip / pageSize + 1;
                int recordsTotal = 0;

                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = "" };
                    return Ok(jsonData);
                }
                else
                {
                    // Scoping department vs "QMS lihat semua" sekarang ditentukan
                    // di SP dari username, bukan di sini - lihat komentar di
                    // CopyRequestRepo.Search / sp_CopyRequest_Search.
                    var listData = copyRequestRepo.Search(data, GetLoginUsername(), db, pageNumber, pageSize);
                    var dataCount = copyRequestRepo.Search(data, GetLoginUsername(), db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult AddEditAsync(string screenMode, CopyRequest data)
        {
            DBResult result;

            try
            {
                data.DIVISION = GetLoginDivision();
                data.DEPARTMENT_ID = GetLoginDepartmentId();
                data.REQUESTED_BY = GetLoginUsername();

                if (screenMode == "ADD")
                {
                    result = copyRequestRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = copyRequestRepo.Update(data, GetLoginUsername(), db);
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(CopyRequest data)
        {
            try
            {
                DBResult result = copyRequestRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Submit(int requestId)
        {
            try
            {
                DBResult result = copyRequestRepo.Submit(requestId, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Cancel(int requestId)
        {
            try
            {
                DBResult result = copyRequestRepo.UpdateStatus(new CopyRequest { REQUEST_ID = requestId, STATUS = "4" }, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult SearchDetail(CopyRequestDetail data)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var listData = copyRequestRepo.SearchDetail(data, db, null, null);
                var jsonData = new { draw = draw, recordsFiltered = listData.Count, recordsTotal = listData.Count, data = listData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        // Panel monitoring "Print" - rincian eksekusi cetak per baris dokumen
        // (PrintTrack), termasuk percobaan yang gagal, untuk kolom Print di
        // grid CopyRequest/Index (request Hendra 2026-08-15).
        public ActionResult SearchPrintLog(int requestId)
        {
            try
            {
                var listData = copyRequestRepo.SearchPrintLog(requestId, db);
                return Json(new { status = true, data = listData });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Document picker: SEMUA dokumen yang SUDAH terdaftar P4D dan sudah
        // Received/Published (STATUS 2 atau 5), lintas department - TIDAK
        // dibatasi ke department requester sendiri (request Hendra 2026-08-14,
        // sebelumnya dibatasi). loginUser sengaja null supaya scoping
        // division/department bawaan sp_P4DMaintenance_Search (lihat SP:
        // filter by TB_M_USER_POS milik @USERNAME kalau bukan QMS) juga tidak
        // ikut membatasi hasilnya. Beda dari
        // DocumentSubmissionController.SearchP4DPendingDocuments yang justru
        // mencari STATUS='0' (belum diregister). OPERATION_TYPE=1 = registrasi
        // P4D asli, bukan baris hasil Request/Copy Request user lain.
        //
        // Dulu dokumen yang department requester sudah jadi target distribusi
        // resminya (softcopy P4D) disembunyikan dari sini - tapi Print
        // Request selalu untuk HARDCOPY (COPY_TYPE cuma Hitam Putih/Warna,
        // tidak ada varian softcopy), jadi department yang sudah punya akses
        // softcopy via distribusi tetap berhak minta cetak fisik terpisah
        // (mis. untuk ditempel di workstation). Filter itu dihapus (request
        // Hendra 2026-08-18) - lihat juga sp_CopyRequest_Submit yang jaring
        // pengaman sisi server-nya juga sudah dicabut.
        public JsonResult SearchApprovedDocuments()
        {
            try
            {
                var searchData = new DocumentControlMaintenance
                {
                    STATUS = "2,5",
                    OPERATION_TYPE = 1
                };

                IList<DocumentControlMaintenance> dataList = p4DMaintenanceRepo.Search(searchData, null, db, null, null).ToList();

                return Json(new { status = true, data = dataList });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult AddEditDetail(string screenMode, CopyRequestDetail data)
        {
            try
            {
                DBResult result = screenMode == "ADD"
                    ? copyRequestRepo.InsertDetail(data, GetLoginUsername(), db)
                    : copyRequestRepo.UpdateDetail(data, GetLoginUsername(), db);

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult DeleteDetail(CopyRequestDetail data)
        {
            try
            {
                DBResult result = copyRequestRepo.DeleteDetail(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Approval satu langkah oleh QMS (fungsi COPYREQUEST-APPROVE) - tidak
        // ada chain internal berjenjang seperti Document Submission (keputusan
        // Hendra 2026-08-11). Fulfillment (bikin baris TB_R_CTRL_DOCUMENT)
        // dijalankan begitu status berhasil pindah ke Approved.
        // Fulfillment (dulu bikin baris TB_R_CTRL_DOCUMENT di UserDashboard
        // begitu Approved, lewat sp_UserDashboard_Request) SENGAJA dicabut -
        // requester sekarang cetak sendiri lewat PrintTrack begitu status
        // Approved, tidak perlu lagi acknowledge terpisah di modul lain
        // (request Hendra 2026-08-15). Lihat SQL/CopyRequest_RemoveAcceptStep.sql.
        public JsonResult ApproveReject(int requestId, string mode, string remark)
        {
            try
            {
                bool isApproved = mode == "approve";
                DBResult result = copyRequestRepo.ApproveReject(requestId, isApproved, remark, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Cetak form fisik QCD/FR-QMS-00/028. Dulu sempat pakai NPOI untuk isi
        // template baru dikonversi PDF via DevExpress (pola
        // DocumentSubmissionController), TAPI khusus untuk template ini itu
        // TIDAK BISA DIPAKAI: checkbox "Jenis Dokumen" & kotak tanda tangan
        // "DOKUMEN KONTROL" di template asli QMS adalah gambar "camera"
        // ter-link (legacy VML) ke range tersembunyi - begitu file disentuh
        // NPOI (sekadar isi cell lain pun cukup), gambar itu SELALU hilang
        // total saat dirender DevExpress (dikonfirmasi 2026-08-11 lewat
        // pengujian bertahap: NPOI sendiri fine, DevExpress sendiri fine,
        // kombinasi keduanya gagal). DevExpress SENDIRI (tanpa NPOI sama
        // sekali) TERBUKTI tidak merusak gambar itu - makanya sekarang
        // template DIMUAT & DIISI langsung pakai DevExpress Spreadsheet
        // Document API dari awal, NPOI tidak dipakai sama sekali di modul ini.
        //
        // Checkbox "Jenis Dokumen" DIISI dari DOC_CATEGORY (keputusan Hendra
        // 2026-08-11) - beda dari kotak tanda tangan "DOKUMEN KONTROL" yang
        // TETAP sengaja dikosongkan (diisi/ditandatangani manual nanti).
        // Checkbox-nya sendiri di template REVISI TERBARU ternyata CELL BIASA
        // (bukan gambar camera-link seperti kotak tanda tangan), jadi aman
        // diisi via DevExpress - lihat CategoryCheckboxCellMap.
        public IActionResult PrintForm(int requestId)
        {
            CopyRequest header = copyRequestRepo.GetByKey(new CopyRequest { REQUEST_ID = requestId }, db);
            if (header == null)
            {
                return BadRequest("Request not found");
            }

            IList<CopyRequestDetail> details = copyRequestRepo.SearchDetail(new CopyRequestDetail { REQUEST_ID = requestId }, db, null, null);

            using DevExpress.Spreadsheet.Workbook workbook = new DevExpress.Spreadsheet.Workbook();
            PopulateWorkbook(workbook, header, details);

            using MemoryStream stream = new MemoryStream();
            workbook.ExportToPdf(stream);

            // Overload 2-argumen (tanpa fileDownloadName) supaya tampil inline
            // di tab baru, bukan didownload - lihat komentar sama di
            // DocumentSubmissionController.PrintForm.
            return File(stream.ToArray(), "application/pdf");
        }

        // Export ke format Excel resmi QCD/FR-QMS-00/028 - isi nilai langsung ke
        // sel-sel template asli QMS (wwwroot/Document/CopyRequest/
        // FR-PERMOHONAN CC DOKUMEN.xlsx), tambahan di samping PrintForm (PDF).
        public IActionResult ExportExcel(int requestId)
        {
            CopyRequest header = copyRequestRepo.GetByKey(new CopyRequest { REQUEST_ID = requestId }, db);
            if (header == null)
            {
                return BadRequest("Request not found");
            }

            IList<CopyRequestDetail> details = copyRequestRepo.SearchDetail(new CopyRequestDetail { REQUEST_ID = requestId }, db, null, null);

            using DevExpress.Spreadsheet.Workbook workbook = new DevExpress.Spreadsheet.Workbook();
            PopulateWorkbook(workbook, header, details);
            byte[] excelBytes = workbook.SaveDocument(DevExpress.Spreadsheet.DocumentFormat.Xlsx);

            string fileSuffix = (header.REQUEST_NO ?? requestId.ToString()).Replace("/", "-");
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FORM-CCR-" + fileSuffix + ".xlsx");
        }

        // Baris 17-30 (14 baris) - BUKAN 17-34 (18 baris) walau print area
        // A1:K35 masih ada ruang sampai baris 34. Gambar "camera" kotak
        // "DOKUMEN KONTROL" melayang di atas cell mulai baris 31 (TopLeftCell
        // asli Picture 4 = $H$31 (versi template lama). Template DIGANTI LAGI
        // 2026-08-11 (revisi Hendra, dokumen sekarang benar QCD/FR-QMS-00/028,
        // 5 kategori Jenis Dokumen sama seperti Document Submission) - kolom
        // tabel jadi jauh lebih lebar (pola kolom-bantu-halus seperti area
        // tersembunyi di template lama, dipakai untuk tabel utama juga di
        // versi ini): B/C=No., D=No.Dokumen, M=Nama Dokumen(M:X),
        // Y=Revisi Ke-(Y:AD), AE=QTY(AE:AH), AI=Hitam Putih/Warna(AI:AS),
        // AT=Alasan(AT:BH), BI=Countermeasure(BI:BR), BS=Remarks(BS:BU).
        // Baris data mulai 19 (header 5 baris, row14:18 merge). Kotak
        // "DOKUMEN KONTROL" (Picture 6) melayang mulai baris 35, baris 34-35
        // masih dipakai list validasi HP/W (B34/B35) - batas aman 14
        // baris/halaman (19-32) supaya tidak ketutupan/ketiban, sama seperti
        // batas versi form sebelumnya - verifikasi ulang pakai render kalau
        // template diganti lagi.
        private const int DetailFirstRow = 19;
        private const int DetailLastRow = 32;
        private const int RowsPerPage = DetailLastRow - DetailFirstRow + 1;

        // Nama file INI yang dipakai Hendra (bukan penamaan
        // "QCD-FR-QMS-00-028-Template.xlsx" versi sebelumnya) - dia yang
        // update file ini langsung tiap ada revisi template.
        private const string TemplateFileName = "FR-PERMOHONAN CC DOKUMEN.xlsx";

        // Checkbox "Jenis Dokumen" - cell biasa (bordered box kosong) persis
        // sebelum tiap label, dikonfirmasi via inspeksi Borders 2026-08-11.
        // Kode SYSTEM_CODE harus sinkron dengan DOC_SUBMISSION_CATEGORY:
        // 1=Pedoman Perusahaan, 2=SOP/EIS/IK/OPL/ACUAN, 3=Prosedur,
        // 4=Checksheet/Form, 5=Lain Lain.
        private static readonly Dictionary<string, string> CategoryCheckboxCellMap = new Dictionary<string, string>
        {
            { "1", "AB10" },
            { "2", "AN10" },
            { "3", "AB12" },
            { "4", "AN12" },
            { "5", "BC10" }
        };

        private void PopulateWorkbook(DevExpress.Spreadsheet.Workbook workbook, CopyRequest header, IList<CopyRequestDetail> details)
        {
            string templatePath = Path.Combine(Environment.WebRootPath, "Document", "CopyRequest", TemplateFileName);
            workbook.LoadDocument(templatePath);

            DevExpress.Spreadsheet.Worksheet baseSheet = workbook.Worksheets[0];
            int pageCount = Math.Max(1, (int)Math.Ceiling(details.Count / (double)RowsPerPage));

            var pageSheets = new List<DevExpress.Spreadsheet.Worksheet> { baseSheet };
            for (int p = 2; p <= pageCount; p++)
            {
                DevExpress.Spreadsheet.Worksheet cloned = workbook.Worksheets.Add(baseSheet.Name + " (" + p + ")");
                cloned.CopyFrom(baseSheet);
                pageSheets.Add(cloned);
            }

            for (int p = 0; p < pageSheets.Count; p++)
            {
                var sheet = pageSheets[p];

                // Template hanya set FitToWidth=1 (FitToHeight=0, scale tetap
                // 60%) - konten 14 baris kadang lebih tinggi sedikit dari 1
                // halaman di scale itu, jadi "REVISI: 01" nyasar ke halaman 2
                // yang nyaris kosong (dikonfirmasi 2026-08-11 lewat render
                // test). Paksa FitToHeight=1 juga supaya selalu pas 1 halaman
                // per sheet.
                sheet.PrintOptions.FitToHeight = 1;

                var pageDetails = details.Skip(p * RowsPerPage).Take(RowsPerPage).ToList();
                PopulateSheet(sheet, header, pageDetails, p * RowsPerPage + 1, p + 1, pageCount);
            }

            workbook.Worksheets.ActiveWorksheet = pageSheets[0];
        }

        private static void PopulateSheet(DevExpress.Spreadsheet.Worksheet sheet, CopyRequest header, IList<CopyRequestDetail> pageDetails,
            int startLineNo, int pageNumber, int totalPages)
        {
            // Kolom J (Division/Department/dst) & M12 ("of") sudah berisi
            // tanda titik dua/pemisah statis di template - K/N cuma diisi
            // nilainya polos, TANPA prefix ": " lagi (beda dari versi
            // template sebelumnya).
            SetCellValue(sheet, "K8", header.DIVISION_NAME ?? header.DIVISION ?? "");
            SetCellValue(sheet, "K9", header.DEPARTMENT_NAME ?? "");
            SetCellValue(sheet, "K10", header.SECTION_NAME ?? "");
            SetCellValue(sheet, "K11", header.REQUEST_DATE.HasValue ? header.REQUEST_DATE.Value.ToString("dd-MM-yyyy") : "");
            SetCellValue(sheet, "K12", pageNumber);
            SetCellValue(sheet, "N12", totalPages);

            foreach (var cat in (header.DOC_CATEGORY ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (CategoryCheckboxCellMap.TryGetValue(cat.Trim(), out var cellRef))
                {
                    SetCheckbox(sheet, cellRef);
                }
            }

            for (int i = 0; i < pageDetails.Count && i < RowsPerPage; i++)
            {
                CopyRequestDetail item = pageDetails[i];
                int r = DetailFirstRow + i;

                SetCellValue(sheet, "B" + r, startLineNo + i);
                SetCellValue(sheet, "D" + r, item.DOCUMENT_CODE ?? "");
                SetCellValue(sheet, "M" + r, item.DOCUMENT_NAME ?? "");
                if (item.REVISION_NO.HasValue) SetCellValue(sheet, "Y" + r, item.REVISION_NO.Value);
                if (item.COPY_QTY.HasValue) SetCellValue(sheet, "AE" + r, item.COPY_QTY.Value);

                // Hitam Putih/Warna (AI), Alasan (AT), Countermeasure (BI),
                // Remarks (BS) SENGAJA dikosongkan dulu (keputusan Hendra
                // 2026-08-11) - belum diisi otomatis dari sistem.
            }

            // Kotak tanda tangan "DOKUMEN KONTROL" (gambar camera-link, lihat
            // catatan di [[dms-copyrequest-approval-simplified]]) SENGAJA
            // tidak disentuh - diisi/ditandatangani manual.
        }

        private static void SetCellValue(DevExpress.Spreadsheet.Worksheet sheet, string cellRef, string value)
        {
            var cell = sheet.Cells[cellRef];
            cell.Value = value;
            cell.Font.Color = System.Drawing.Color.Black;
            // Beberapa cell template (mis. Revisi Ke-/QTY) ternyata bawa font
            // Wingdings bawaan - angka biasa jadi ke-render sebagai simbol
            // (mis. ikon folder), bukan digit. Dipaksa Arial di sini supaya
            // konsisten dengan sisa form, terlepas dari font bawaan cell-nya.
            cell.Font.Name = "Arial";
        }

        // Karakter centang di font Marlett - "a" (0x61) me-render jadi tanda
        // centang, dikonfirmasi dari teks instruksi form sendiri ("Berikan
        // tanda (a) :" ditulis pakai font Marlett dengan karakter ini persis).
        private static void SetCheckbox(DevExpress.Spreadsheet.Worksheet sheet, string cellRef)
        {
            var cell = sheet.Cells[cellRef];
            cell.Value = "a";
            cell.Font.Name = "Marlett";
            cell.Font.Size = 11;
        }

        private static void SetCellValue(DevExpress.Spreadsheet.Worksheet sheet, string cellRef, double value)
        {
            var cell = sheet.Cells[cellRef];
            cell.Value = value;
            cell.Font.Color = System.Drawing.Color.Black;
            cell.Font.Name = "Arial";
        }
    }
}
