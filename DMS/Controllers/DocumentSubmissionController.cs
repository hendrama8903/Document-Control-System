using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using MDC.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace DMS.Controllers
{
    public class DocumentSubmissionController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;

        public DocumentSubmissionController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private DocumentSubmissionRepo documentSubmissionRepo = DocumentSubmissionRepo.Instance;
        private P4DMaintenanceRepo p4DMaintenanceRepo = P4DMaintenanceRepo.Instance;
        private ApprovalRepo approvalRepo = ApprovalRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentSubmission/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("DOCSUBMISSION-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("DOCSUBMISSION-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("DOCSUBMISSION-DELETE");
            ViewData["Submit"] = HttpContext.Session.GetString("functionList").Contains("DOCSUBMISSION-SUBMIT");
            ViewData["Print"] = HttpContext.Session.GetString("functionList").Contains("DOCSUBMISSION-PRINT");
            ViewData["Approve"] = HttpContext.Session.GetString("functionList").Contains("DOCSUBMISSION-APPROVE");

            ViewData["Title"] = "Document Submission Form";

            return View();
        }

        public JsonResult GetByKey(DocumentSubmission data)
        {
            try
            {
                DocumentSubmission result = documentSubmissionRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(DocumentSubmission data, bool initialMode)
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

                string division = GetLoginDivision();
                int? departmentId = GetLoginDepartmentId();

                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = "" };
                    return Ok(jsonData);
                }
                else
                {
                    var listData = documentSubmissionRepo.Search(data, division, departmentId, db, pageNumber, pageSize);
                    var dataCount = documentSubmissionRepo.Search(data, division, departmentId, db, null, null).Count;
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

        public JsonResult AddEditAsync(string screenMode, DocumentSubmission data)
        {
            DBResult result;

            try
            {
                data.DIVISION = GetLoginDivision();
                data.DEPARTMENT_ID = GetLoginDepartmentId();
                data.DOCUMENT_CREATOR = GetLoginUsername();

                if (screenMode == "ADD")
                {
                    result = documentSubmissionRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = documentSubmissionRepo.Update(data, GetLoginUsername(), db);
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(DocumentSubmission data)
        {
            try
            {
                DBResult result = documentSubmissionRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Submit(int submissionId)
        {
            try
            {
                DBResult result = documentSubmissionRepo.Submit(submissionId, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Cancel(int submissionId)
        {
            try
            {
                DBResult result = documentSubmissionRepo.UpdateStatus(new DocumentSubmission { SUBMISSION_ID = submissionId, STATUS = "4" }, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult SearchDetail(DocumentSubmissionDetail data)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var listData = documentSubmissionRepo.SearchDetail(data, db, null, null);
                var jsonData = new { draw = draw, recordsFiltered = listData.Count, recordsTotal = listData.Count, data = listData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        // Untuk Jenis Pengajuan Baru/Revisi: user pilih dari dokumen yang SUDAH
        // teregistrasi di P4D (TB_R_CTRL_DOCUMENT) tapi belum di-approve di sana
        // (STATUS=0/Draft) - supaya tidak perlu ketik ulang data yang sudah ada.
        // param = SUBMISSION_TYPE ("01" Baru / "02" Revisi). sp_P4DMaintenance_Search
        // sudah me-resolve DOCUMENT_TYPE ke teks TB_M_SYSTEM ("New"/"Revision"),
        // bukan kode mentahnya - jadi filternya harus dicocokkan ke teks itu, bukan
        // ke "01"/"02" langsung. OPERATION_TYPE=1 = layar "Document Registration"
        // biasa (bukan antrian DocumentControlDashboard yang pakai OPERATION_TYPE=2).
        // Pemusnahan tidak ada di sini karena P4D tidak punya konsep itu - tetap
        // diisi manual di form.
        private static readonly Dictionary<string, string> SubmissionTypeToP4DDocumentType = new Dictionary<string, string>
        {
            { "01", "New" },
            { "02", "Revision" }
        };

        public JsonResult GetP4DPendingDocuments(string q, string pageLimit, string page, string param)
        {
            try
            {
                var searchData = new DocumentControlMaintenance
                {
                    DEPARTMENT_ID = GetLoginDepartmentId(),
                    STATUS = "0",
                    OPERATION_TYPE = 1
                };

                if (q != null)
                    searchData.DOCUMENT_CODE = '*' + q + '*';

                string documentTypeText = !string.IsNullOrEmpty(param) && SubmissionTypeToP4DDocumentType.TryGetValue(param, out var mapped) ? mapped : null;

                IList<DocumentControlMaintenance> dataList = p4DMaintenanceRepo.Search(searchData, GetLoginUsername(), db, null, null)
                    .Where(x => documentTypeText == null || x.DOCUMENT_TYPE == documentTypeText)
                    .Take(int.Parse(pageLimit))
                    .ToList();

                var list = dataList.Select(x => new Select2 { id = x.DOCUMENT_CTRL_ID.ToString(), text = x.DOCUMENT_CODE + " - " + x.DOCUMENT_NAME }).ToList();
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Untuk pop-up "Add Document" versi multi-select (checkbox banyak dokumen
        // sekaligus) - beda dari GetP4DPendingDocuments (select2 satu-per-satu,
        // masih dipakai jalur "Add Manually"). Data lengkap diambil sekaligus,
        // search/paging-nya ditangani DataTables di sisi client (jumlah dokumen
        // pending per department realistis kecil).
        public JsonResult SearchP4DPendingDocuments()
        {
            try
            {
                var searchData = new DocumentControlMaintenance
                {
                    DEPARTMENT_ID = GetLoginDepartmentId(),
                    STATUS = "0",
                    OPERATION_TYPE = 1
                };

                IList<DocumentControlMaintenance> dataList = p4DMaintenanceRepo.Search(searchData, GetLoginUsername(), db, null, null).ToList();

                var docCodes = dataList.Select(x => x.DOCUMENT_CODE).Where(c => c != null).Distinct().ToList();
                var levelMap = db.DocumentMaintenance
                    .Where(d => docCodes.Contains(d.DOCUMENT_CODE))
                    .Join(db.DocumentMaster, d => d.DOCUMENT_ID, m => m.DOCUMENT_ID, (d, m) => new { d.DOCUMENT_CODE, m.LEVEL })
                    .ToList()
                    .GroupBy(x => x.DOCUMENT_CODE)
                    .ToDictionary(g => g.Key, g => g.First().LEVEL);

                foreach (var item in dataList)
                {
                    int? level = item.DOCUMENT_CODE != null && levelMap.TryGetValue(item.DOCUMENT_CODE, out var lvl) ? lvl : null;
                    item.DOC_CATEGORY_CODE = MapDocumentLevelToSubmissionCategory(level);
                }

                return Json(new { status = true, data = dataList });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // TB_M_DOCUMENT.LEVEL -> kode DOC_SUBMISSION_CATEGORY (1=Pedoman Perusahaan,
        // 2=SOP/EIS/IK/OPL/ACUAN, 3=Prosedur, 4=Checksheet/Form, 5=Lain Lain).
        // Level 1=PDM, Level 2=PRO/SPR, Level 3=SOP/IK/EIS/OPL/ACU/SOE, Level 4=CS/FR/RE/SM.
        private static string MapDocumentLevelToSubmissionCategory(int? level)
        {
            switch (level)
            {
                case 1: return "1";
                case 2: return "3";
                case 3: return "2";
                case 4: return "4";
                default: return "5";
            }
        }

        public JsonResult GetP4DDocumentDetail(int documentCtrlId)
        {
            try
            {
                DocumentControlMaintenance result = p4DMaintenanceRepo.Search(new DocumentControlMaintenance { DOCUMENT_CTRL_ID = documentCtrlId }, null, db, 1, 1).FirstOrDefault();
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult AddEditDetail(string screenMode, DocumentSubmissionDetail data)
        {
            try
            {
                DBResult result = screenMode == "ADD"
                    ? documentSubmissionRepo.InsertDetail(data, GetLoginUsername(), db)
                    : documentSubmissionRepo.UpdateDetail(data, GetLoginUsername(), db);

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult DeleteDetail(DocumentSubmissionDetail data)
        {
            try
            {
                DBResult result = documentSubmissionRepo.DeleteDetail(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetApprovalHeader(int approvalId)
        {
            try
            {
                var result = approvalRepo.GetApprovalHeader(approvalId, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetApprovalDetail(int approvalId)
        {
            try
            {
                var result = approvalRepo.GetApprovalDetail(approvalId, db, null, null);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetApprovalHistoryDetail(ApprovalDetail data)
        {
            try
            {
                var result = approvalRepo.GetApprovalHistoryDetail(data, db, null, null);
                result = result.Where(x => x.REMARK != null).ToList();
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult ApproveRejectAsync(int approvalId, int workflowSeq, string approver, string remark, string mode, DocumentSubmission data)
        {
            DBResult result;

            try
            {
                db.Database.BeginTransaction();

                if (mode == "approve")
                {
                    IList<ApprovalDetail> approvalDetails = approvalRepo.GetApprovalDetail(approvalId, db, null, null);

                    if (approvalDetails.Count == 0)
                    {
                        db.Database.RollbackTransaction();
                        return Json(new { status = false, message = "Approval data not found" });
                    }

                    var maxSeq = approvalDetails.Where(x => x.APPROVAL_ID == approvalId).Max(x => x.WORKFLOW_SEQ);

                    if (maxSeq == workflowSeq)
                    {
                        result = documentSubmissionRepo.UpdateStatus(new DocumentSubmission { SUBMISSION_ID = data.SUBMISSION_ID, STATUS = "2" }, GetLoginUsername(), db);
                        if (!result.status)
                        {
                            db.Database.RollbackTransaction();
                            return Json(new { status = false, message = result.message });
                        }
                    }

                    result = approvalRepo.Approve(approvalId, workflowSeq, approver, remark, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        db.Database.RollbackTransaction();
                        return Json(new { status = false, message = result.message });
                    }

                    db.Database.CommitTransaction();
                    return Json(result);
                }
                else
                {
                    result = documentSubmissionRepo.UpdateStatus(new DocumentSubmission { SUBMISSION_ID = data.SUBMISSION_ID, STATUS = "3" }, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        db.Database.RollbackTransaction();
                        return Json(new { status = false, message = result.message });
                    }

                    result = approvalRepo.Reject(approvalId, workflowSeq, approver, remark, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        db.Database.RollbackTransaction();
                        return Json(new { status = false, message = result.message });
                    }

                    db.Database.CommitTransaction();
                    return Json(result);
                }
            }
            catch (Exception ex)
            {
                db.Database.RollbackTransaction();
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Cetak form QMS "QCD/FR-QMS-00/003" - dulu digambar manual dari nol pakai
        // PdfSharp, sekarang diganti (request user 2026-08-09): generate Excel-nya
        // dulu (BuildSubmissionExcel, isi sheet P4D asli QMS) baru dikonversi ke PDF
        // pakai DevExpress Spreadsheet Document API (pola sama seperti
        // DocumentMaintenanceController.ConvertToPdf) - supaya PDF & Excel selalu
        // identik dan keduanya persis mengikuti format resmi, bukan approksimasi.
        public IActionResult PrintForm(int submissionId)
        {
            DocumentSubmission header = documentSubmissionRepo.GetByKey(new DocumentSubmission { SUBMISSION_ID = submissionId }, db);
            if (header == null)
            {
                return BadRequest("Submission not found");
            }

            IList<DocumentSubmissionDetail> details = documentSubmissionRepo.SearchDetail(new DocumentSubmissionDetail { SUBMISSION_ID = submissionId }, db, null, null);

            byte[] excelBytes = BuildSubmissionExcel(header, details);
            byte[] pdfBytes = ConvertExcelToPdf(excelBytes);

            // Overload 2-argumen (tanpa fileDownloadName) supaya Content-Disposition
            // TIDAK "attachment" - biar langsung tampil di PDF viewer bawaan browser
            // (preview), bukan malah men-download filenya. Kalau pakai overload
            // 3-argumen dengan filename, browser selalu paksa download alih-alih
            // preview inline, sehingga tab yang dibuka otomatis setelah Save
            // (lihat finishSaveHeader di _Javascript.cshtml) tetap kosong.
            return File(pdfBytes, "application/pdf");
        }

        private static byte[] ConvertExcelToPdf(byte[] excelBytes)
        {
            string tempXlsxPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx");
            string tempPdfPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");

            try
            {
                System.IO.File.WriteAllBytes(tempXlsxPath, excelBytes);

                using (var workbook = new DevExpress.Spreadsheet.Workbook())
                {
                    workbook.LoadDocument(tempXlsxPath);
                    workbook.ExportToPdf(tempPdfPath);
                }

                return System.IO.File.ReadAllBytes(tempPdfPath);
            }
            finally
            {
                if (System.IO.File.Exists(tempXlsxPath)) System.IO.File.Delete(tempXlsxPath);
                if (System.IO.File.Exists(tempPdfPath)) System.IO.File.Delete(tempPdfPath);
            }
        }

        // Export ke format Excel resmi QCD/FR-QMS-00/003 (sheet "P4D") - bukan
        // gambar ulang manual seperti PrintForm (PDF), tapi isi nilai langsung ke
        // sel-sel template asli QMS (wwwroot/Document/DocumentSubmission/
        // FORM-P4D-Template.xlsx) supaya formatnya persis sama dengan dokumen QMS
        // resmi. Tambahan di samping PrintForm (PDF), bukan pengganti - request
        // user 2026-08-09.
        public IActionResult ExportExcel(int submissionId)
        {
            DocumentSubmission header = documentSubmissionRepo.GetByKey(new DocumentSubmission { SUBMISSION_ID = submissionId }, db);
            if (header == null)
            {
                return BadRequest("Submission not found");
            }

            IList<DocumentSubmissionDetail> details = documentSubmissionRepo.SearchDetail(new DocumentSubmissionDetail { SUBMISSION_ID = submissionId }, db, null, null);

            byte[] excelBytes = BuildSubmissionExcel(header, details);

            string fileSuffix = (header.SUBMISSION_NO ?? submissionId.ToString()).Replace("/", "-");
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FORM-P4D-" + fileSuffix + ".xlsx");
        }

        // Posisi sel di sheet "P4D" template, dipetakan dari file asli QMS
        // (C:\Users\it04\Downloads\template QMS\form\003. FORM-P4D (Rev. 01) -
        // REGISTRASI.xlsx) - lihat sheet SAMPLE-nya untuk referensi nilai terisi.
        private static readonly Dictionary<string, string> ExcelCategoryCellMap = new Dictionary<string, string>
        {
            { "1", "AB10" }, // Pedoman Perusahaan
            { "2", "AQ10" }, // SOP/EIS/IK/OPL/ACUAN
            { "3", "AB12" }, // Prosedur
            { "4", "AQ12" }, // Checksheet/Form
            { "5", "BF10" }  // Lain Lain
        };

        // Baris dokumen di sheet P4D cuma nampung 17 baris per halaman (19-33 =
        // 15 baris utama, 37-38 = 2 baris tambahan) - kalau dokumennya lebih
        // banyak, sheet "P4D" di-clone jadi "P4D (2)", "P4D (3)", dst, masing-
        // masing diberi nomor "Halaman X of Y" sesuai kolom yang sudah disediakan
        // template (request user 2026-08-09, opsi "duplikasi sheet per 17 baris").
        private static readonly int[] ExcelDataRows = { 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 37, 38 };
        private const int ExcelRowsPerPage = 17;
        private static readonly string[] DistributionColumns = { "BT", "BU", "BV", "BW", "BX", "BY", "BZ" };
        private const string ExcelCheckMark = "\u00FC"; // "ü" - checkmark di template (font checkbox sudah melekat di style sel, bukan di karakternya)

        private byte[] BuildSubmissionExcel(DocumentSubmission header, IList<DocumentSubmissionDetail> details)
        {
            string templatePath = Path.Combine(Environment.WebRootPath, "Document", "DocumentSubmission", "FORM-P4D-Template.xlsx");

            IWorkbook workbook;
            using (FileStream fs = new FileStream(templatePath, FileMode.Open, FileAccess.Read))
            {
                workbook = new XSSFWorkbook(fs);
            }

            // Beberapa sel checkbox di template ASLI ternyata salah font (harusnya
            // Wingdings/Marlett biar karakter "ü" tampil sebagai centang, tapi
            // sebagian kepasang Calibri/Arial biasa - kelihatan dari hasil export
            // yang nampilin huruf "ü" polos dalam kotak, bukan tanda centang).
            // Jadi font Wingdings dipaksa manual di sini tiap kali nulis centang,
            // bukan mengandalkan style sel yang sudah ada di template.
            IFont wingdingsFont = workbook.CreateFont();
            wingdingsFont.FontName = "Wingdings";
            wingdingsFont.FontHeightInPoints = 10;
            wingdingsFont.IsBold = true;

            int p4dIndex = workbook.GetSheetIndex("P4D");
            int pageCount = Math.Max(1, (int)Math.Ceiling(details.Count / (double)ExcelRowsPerPage));

            var pageSheets = new List<ISheet> { workbook.GetSheetAt(p4dIndex) };
            for (int p = 2; p <= pageCount; p++)
            {
                ISheet cloned = workbook.CloneSheet(p4dIndex);
                workbook.SetSheetName(workbook.GetSheetIndex(cloned), "P4D (" + p + ")");
                pageSheets.Add(cloned);
            }

            RemoveSheetIfExists(workbook, "SAMPLE");
            RemoveSheetIfExists(workbook, "LAMPIRAN");

            List<int> distributionDeptIds = details
                .Where(d => !string.IsNullOrEmpty(d.DISTRIBUTION_DEPT_IDS))
                .SelectMany(d => d.DISTRIBUTION_DEPT_IDS.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .Take(DistributionColumns.Length)
                .ToList();

            Dictionary<int, string> deptCodeById = distributionDeptIds.Count == 0
                ? new Dictionary<int, string>()
                : db.DepartmentMaster
                    .Where(d => d.DEPARTMENT_ID.HasValue && distributionDeptIds.Contains(d.DEPARTMENT_ID.Value))
                    .ToDictionary(d => d.DEPARTMENT_ID.Value, d => d.DEPARTMENT_CODE ?? "");

            for (int p = 0; p < pageSheets.Count; p++)
            {
                var pageDetails = details.Skip(p * ExcelRowsPerPage).Take(ExcelRowsPerPage).ToList();
                PopulateExcelPage(workbook, pageSheets[p], header, pageDetails, p * ExcelRowsPerPage + 1, p + 1, pageCount, distributionDeptIds, deptCodeById, wingdingsFont);
            }

            for (int i = 0; i < workbook.NumberOfSheets; i++)
            {
                workbook.GetSheetAt(i).IsSelected = i == 0;
            }
            workbook.SetActiveSheet(0);

            using MemoryStream stream = new MemoryStream();
            workbook.Write(stream);
            return stream.ToArray();
        }

        private static void RemoveSheetIfExists(IWorkbook workbook, string sheetName)
        {
            int idx = workbook.GetSheetIndex(sheetName);
            if (idx >= 0) workbook.RemoveSheetAt(idx);
        }

        private static void PopulateExcelPage(IWorkbook workbook, ISheet sheet, DocumentSubmission header, IList<DocumentSubmissionDetail> pageDetails,
            int startLineNo, int pageNumber, int totalPages, List<int> distributionDeptIds, Dictionary<int, string> deptCodeById, IFont wingdingsFont)
        {
            SetCellValue(sheet, "K8", header.DIVISION_NAME ?? header.DIVISION ?? "");
            SetCellValue(sheet, "K9", header.DEPARTMENT_NAME ?? "");
            SetCellValue(sheet, "K10", header.SECTION_NAME ?? "");
            if (header.SUBMISSION_DATE.HasValue) SetCellDate(sheet, "K11", header.SUBMISSION_DATE.Value);
            SetCellValue(sheet, "K12", pageNumber);
            SetCellValue(sheet, "N12", totalPages);

            foreach (var cat in (header.DOC_CATEGORY ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (ExcelCategoryCellMap.TryGetValue(cat.Trim(), out var cellRef))
                {
                    SetCheckboxChecked(workbook, sheet, cellRef, wingdingsFont);
                }
            }

            for (int i = 0; i < distributionDeptIds.Count && i < DistributionColumns.Length; i++)
            {
                string code = deptCodeById.TryGetValue(distributionDeptIds[i], out var c) ? c : "";
                SetCellValue(sheet, DistributionColumns[i] + "18", code);
            }

            // Baris yang tidak dipakai (sisa slot 17 baris tanpa data) masih bawa
            // default "0" bawaan template di kolom Qty - kosongkan supaya baris
            // kosong benar-benar kosong, bukan nampilin "0" nyasar.
            for (int i = pageDetails.Count; i < ExcelDataRows.Length; i++)
            {
                SetCellValue(sheet, "BR" + ExcelDataRows[i], "");
            }

            for (int i = 0; i < pageDetails.Count && i < ExcelDataRows.Length; i++)
            {
                DocumentSubmissionDetail item = pageDetails[i];
                int r = ExcelDataRows[i];

                SetCellValue(sheet, "B" + r, startLineNo + i);
                SetCellValue(sheet, "D" + r, item.DOCUMENT_NO ?? "");
                SetCellValue(sheet, "M" + r, item.DOCUMENT_NAME ?? "");

                if (item.CLASSIFICATION == 2) SetCheckboxChecked(workbook, sheet, "AB" + r, wingdingsFont);
                else SetCheckboxChecked(workbook, sheet, "Y" + r, wingdingsFont);

                if (item.SUBMISSION_TYPE == "02")
                {
                    SetCheckboxChecked(workbook, sheet, "AG" + r, wingdingsFont);
                    if (item.REVISION_NO.HasValue) SetCellValue(sheet, "AI" + r, item.REVISION_NO.Value);
                }
                else if (item.SUBMISSION_TYPE == "03")
                {
                    SetCheckboxChecked(workbook, sheet, "AK" + r, wingdingsFont);
                }
                else
                {
                    SetCheckboxChecked(workbook, sheet, "AE" + r, wingdingsFont);
                }

                SetCellValue(sheet, "AN" + r, item.ITEM_CHANGED ?? "");
                SetCellValue(sheet, "AY" + r, item.REASON ?? "");
                if (item.EFFECTIVE_DATE.HasValue) SetCellDate(sheet, "BJ" + r, item.EFFECTIVE_DATE.Value);

                // Kolom "Kebutuhan Dokumen Controlled Copy" tidak wajib diisi (sama
                // seperti PrintForm/PDF, request user 2026-08-08) - kosongkan
                // default "0" bawaan template kalau memang belum diisi.
                string copyText = item.COPY_TYPE == "2" ? "W" : item.COPY_TYPE == "1" ? "HP" : "";
                SetCellValue(sheet, "BN" + r, copyText);
                if (item.COPY_QTY.HasValue) SetCellValue(sheet, "BR" + r, item.COPY_QTY.Value);
                else SetCellValue(sheet, "BR" + r, "");

                if (!string.IsNullOrEmpty(item.DISTRIBUTION_DEPT_IDS))
                {
                    foreach (var idStr in item.DISTRIBUTION_DEPT_IDS.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!int.TryParse(idStr.Trim(), out var deptId)) continue;
                        int colIdx = distributionDeptIds.IndexOf(deptId);
                        if (colIdx >= 0 && colIdx < DistributionColumns.Length)
                        {
                            SetCellValue(sheet, DistributionColumns[colIdx] + r, "1");
                        }
                    }
                }
            }
        }

        private static void SetCellValue(ISheet sheet, string cellRef, string value)
        {
            GetOrCreateCell(sheet, cellRef).SetCellValue(value);
        }

        // Font checkbox-nya dipaksa Wingdings di sini (bukan pakai style sel yang
        // sudah ada) karena beberapa sel checkbox di template asli salah font -
        // clone style sel biar border/alignment tetap sama, cuma font-nya diganti.
        private static void SetCheckboxChecked(IWorkbook workbook, ISheet sheet, string cellRef, IFont wingdingsFont)
        {
            ICell cell = GetOrCreateCell(sheet, cellRef);

            ICellStyle checkedStyle = workbook.CreateCellStyle();
            checkedStyle.CloneStyleFrom(cell.CellStyle);
            checkedStyle.SetFont(wingdingsFont);
            cell.CellStyle = checkedStyle;

            cell.SetCellValue(ExcelCheckMark);
        }

        private static void SetCellValue(ISheet sheet, string cellRef, double value)
        {
            GetOrCreateCell(sheet, cellRef).SetCellValue(value);
        }

        private static void SetCellDate(ISheet sheet, string cellRef, DateTime value)
        {
            GetOrCreateCell(sheet, cellRef).SetCellValue(value);
        }

        private static ICell GetOrCreateCell(ISheet sheet, string cellRef)
        {
            CellReference cr = new CellReference(cellRef);
            IRow row = sheet.GetRow(cr.Row) ?? sheet.CreateRow(cr.Row);
            return row.GetCell(cr.Col) ?? row.CreateCell(cr.Col);
        }
    }
}
