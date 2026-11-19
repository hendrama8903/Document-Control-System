using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using DMS.Services;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;

namespace DMS.Controllers
{
    public class LegacyDocumentImportController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        private readonly ILogger<LegacyDocumentImportController> logger;

        // Upload limits shown on the page (Excel 10 MB, ZIP 100 MB); the request limit leaves
        // room for both plus the multipart overhead (2026-11-17).
        private const long MaxExcelBytes = 10L * 1024 * 1024;
        private const long MaxZipBytes = 100L * 1024 * 1024;
        private const long MaxImportRequestBytes = 115L * 1024 * 1024;

        public LegacyDocumentImportController(DBContext db, IWebHostEnvironment environment, ILogger<LegacyDocumentImportController> logger)
        {
            this.db = db;
            Environment = environment;
            this.logger = logger;
        }

        // Technical details (exception text, SQL errors, server paths) go to the log only; the
        // user gets a plain message plus a reference to quote to IT (2026-11-17).
        private string UnexpectedError(Exception ex, string whileDoing)
        {
            string reference = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
            logger.LogError(ex, "Legacy document import: error while {WhileDoing} (ref {Reference}, user {User})",
                whileDoing, reference, HttpContext.Session.GetString("USERNAME"));
            return "An unexpected error occurred while " + whileDoing + ". Please contact IT and quote reference " + reference + ".";
        }

        // The import SPs return either a business message ("ERROR: Document Code X is already
        // registered") or, from their CATCH block, "ERROR: <procedure>: <sql error>, at line = n".
        // Business messages are shown without the "ERROR: " prefix; SQL errors are logged.
        private string FriendlyDbMessage(string message, string whileDoing)
        {
            string text = (message ?? "").Trim();
            if (text.Length == 0 || text.Contains("at line =", StringComparison.OrdinalIgnoreCase))
            {
                return UnexpectedError(new Exception(text.Length == 0 ? "Empty message from the database." : text), whileDoing);
            }
            return text.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase) ? text.Substring(6).Trim() : text;
        }

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private DivisionMasterRepo divisionMasterRepo = DivisionMasterRepo.Instance;
        private DepartmentMasterRepo departmentMasterRepo = DepartmentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;
        private SectionMasterRepo sectionMasterRepo = SectionMasterRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }

            if (!HasMenuAccess("/LegacyDocumentImport/Index"))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            // add authorization function
            ViewData["Import"] = HttpContext.Session.GetString("functionList").Contains("LEGACYDOCUMENTIMPORT-IMPORT");

            ViewData["Title"] = "Document Import";

            return View();
        }

        public IActionResult DownloadTemplate()
        {
            if (!HasMenuAccess("/LegacyDocumentImport/Index"))
            {
                return StatusCode(403);
            }

            IWorkbook workbook = BuildTemplate(out _);
            return XlsxFile(workbook, "Import_Legacy_Document_Template.xlsx");
        }

        // Template sheet (header only) + Reference sheet with the master codes. Column K
        // Revision Reason is optional (2026-11-12, filled by the master list converter).
        private IWorkbook BuildTemplate(out ISheet templateSheet)
        {
            IList<DocumentMaster> categories = documentMasterRepo.Search(new DocumentMaster(), db, null, null);
            IList<DivisionMaster> divisions = divisionMasterRepo.Search(new DivisionMaster(), db, null, null);
            IList<DepartmentMaster> departments = departmentMasterRepo.Search(new DepartmentMaster(), db, null, null);
            IList<MSystem> classifications = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "CLASSIFIED" }, db, null, null);

            IWorkbook workbook = new XSSFWorkbook();
            {
                templateSheet = workbook.CreateSheet("Template");
                string[] headers = { "File Name", "Document Code", "Document Name", "Category Code", "Division Code", "Department Code", "Classification Code", "Revision", "Document Date (DD-MM-YYYY)", "Document Creator", "Revision Reason (optional)", "Distribution (optional)" };
                IRow headerRow = templateSheet.CreateRow(0);
                for (int i = 0; i < headers.Length; i++)
                {
                    headerRow.CreateCell(i).SetCellValue(headers[i]);
                    templateSheet.SetColumnWidth(i, 22 * 256);
                }

                ISheet referenceSheet = workbook.CreateSheet("Reference");
                int refRow = 0;

                IRow noteTitle = referenceSheet.CreateRow(refRow++);
                noteTitle.CreateCell(0).SetCellValue("Document Revision History");
                IRow noteLine1 = referenceSheet.CreateRow(refRow++);
                noteLine1.CreateCell(0).SetCellValue("A Document Code may repeat across multiple rows, one row per past revision (e.g. Revision 0, 1, 2...).");
                IRow noteLine2 = referenceSheet.CreateRow(refRow++);
                noteLine2.CreateCell(0).SetCellValue("File Name is required only on the row with the highest Revision for that Document Code (it becomes the active document). Older revision rows may leave File Name blank.");
                IRow noteLine3 = referenceSheet.CreateRow(refRow++);
                noteLine3.CreateCell(0).SetCellValue("Distribution (optional, latest revision row only): departments that already hold the document and the date they received it, e.g. \"APP:12-04-2017; INF:12-04-2017\". The document is then registered in P4D as sent and acknowledged on that date.");
                refRow += 1;

                IRow catTitle = referenceSheet.CreateRow(refRow++);
                catTitle.CreateCell(0).SetCellValue("Category Code");
                catTitle.CreateCell(1).SetCellValue("Category Name");
                foreach (DocumentMaster category in categories)
                {
                    IRow row = referenceSheet.CreateRow(refRow++);
                    row.CreateCell(0).SetCellValue(category.DOCUMENT_CODE);
                    row.CreateCell(1).SetCellValue(category.DOCUMENT_NAME);
                }

                refRow += 2;
                IRow divTitle = referenceSheet.CreateRow(refRow++);
                divTitle.CreateCell(0).SetCellValue("Division Code");
                divTitle.CreateCell(1).SetCellValue("Division Name");
                foreach (DivisionMaster division in divisions)
                {
                    IRow row = referenceSheet.CreateRow(refRow++);
                    row.CreateCell(0).SetCellValue(division.DIVISION_CODE);
                    row.CreateCell(1).SetCellValue(division.DIVISION_NAME);
                }

                refRow += 2;
                IRow deptTitle = referenceSheet.CreateRow(refRow++);
                deptTitle.CreateCell(0).SetCellValue("Department Code");
                deptTitle.CreateCell(1).SetCellValue("Department Name");
                deptTitle.CreateCell(2).SetCellValue("Division Code");
                foreach (DepartmentMaster department in departments)
                {
                    IRow row = referenceSheet.CreateRow(refRow++);
                    row.CreateCell(0).SetCellValue(department.DEPARTMENT_CODE);
                    row.CreateCell(1).SetCellValue(department.DEPARTMENT_NAME);
                    row.CreateCell(2).SetCellValue(department.DIVISION);
                }

                refRow += 2;
                IRow clsTitle = referenceSheet.CreateRow(refRow++);
                clsTitle.CreateCell(0).SetCellValue("Classification Code");
                clsTitle.CreateCell(1).SetCellValue("Classification Name");
                foreach (MSystem classification in classifications)
                {
                    IRow row = referenceSheet.CreateRow(refRow++);
                    row.CreateCell(0).SetCellValue(classification.SYSTEM_CODE);
                    row.CreateCell(1).SetCellValue(classification.SYSTEM_VALUE);
                }

                referenceSheet.SetColumnWidth(0, 20 * 256);
                referenceSheet.SetColumnWidth(1, 30 * 256);
                referenceSheet.SetColumnWidth(2, 16 * 256);
            }
            return workbook;
        }

        private static string SizeText(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " MB";

        private FileStreamResult XlsxFile(IWorkbook workbook, string fileName)
        {
            // NPOI's Write(Stream) closes the stream it's given (this NPOI version has no
            // leaveOpen overload), so write through a wrapper that swallows Dispose/Close
            // and keep the real MemoryStream readable afterward.
            MemoryStream stream = new MemoryStream();
            workbook.Write(new NonClosingStreamWrapper(stream));
            stream.Position = 0;
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // Master List converter (request Hendra 2026-11-12): the departments' own master lists
        // (form QCD/FR-QMS-00/004) in, a filled import template out - see MasterListConverter.
        // documentFiles = JSON array of the names of the documents the user picked; only the
        // names are sent, the files themselves go up later in the ZIP of the normal import.
        // Nothing is saved here.
        [HttpPost]
        [RequestSizeLimit(60 * 1024 * 1024)]
        public IActionResult ConvertMasterList(List<IFormFile> masterLists, string documentFiles)
        {
            if (!HasMenuAccess("/LegacyDocumentImport/Index"))
            {
                return Json(new { status = false, message = "You are not authorized to use this feature." });
            }

            try
            {
                if (masterLists == null || masterLists.Count == 0)
                {
                    return Json(new { status = false, message = "Choose at least one master list (.xlsx or .xls)." });
                }
                if (masterLists.Any(f => !new[] { ".xlsx", ".xls", ".xlsm" }.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())))
                {
                    return Json(new { status = false, message = "Master lists must be Excel files (.xlsx, .xls)." });
                }

                List<string> names = string.IsNullOrWhiteSpace(documentFiles)
                    ? new List<string>()
                    : System.Text.Json.JsonSerializer.Deserialize<List<string>>(documentFiles) ?? new List<string>();

                IList<DocumentMaintenance> existingDocs = documentMaintenanceRepo.Search(new DocumentMaintenance(), null, db, null, null);
                var masters = new MasterListConverter.Masters
                {
                    Categories = documentMasterRepo.Search(new DocumentMaster(), db, null, null),
                    Divisions = divisionMasterRepo.Search(new DivisionMaster(), db, null, null),
                    Departments = departmentMasterRepo.Search(new DepartmentMaster(), db, null, null),
                    Sections = sectionMasterRepo.Search(new SectionMaster(), db, null, null),
                    Classifications = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "CLASSIFIED" }, db, null, null),
                    CompanyCodes = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "COMPANY_CODE" }, db, null, null),
                    ProcessCodes = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "PROCESS_CODE" }, db, null, null),
                    ExistingRevisions = existingDocs
                        .Where(x => !string.IsNullOrEmpty(x.DOCUMENT_CODE))
                        .GroupBy(x => x.DOCUMENT_CODE.ToUpperInvariant())
                        .ToDictionary(g => g.Key, g => g.Max(x => x.REVISION ?? 0)),
                    // former department -> current department(s): the Organization Change register
                    // (2026-11-19; it replaced System Parameters DEPT_SUCCESSOR, still read as a fallback)
                    Successors = Services.OrgChangeService.LoadSuccessors(db)
                };

                // WorkbookFactory needs a stream it can seek in.
                var lists = new List<(string, Stream)>();
                foreach (IFormFile file in masterLists)
                {
                    var copy = new MemoryStream();
                    file.CopyTo(copy);
                    copy.Position = 0;
                    lists.Add((Path.GetFileName(file.FileName), copy));
                }

                MasterListConverter.Result result = MasterListConverter.Convert(lists, names, masters);
                if (result.Rows.Count == 0)
                {
                    string why = string.Join(" ", result.Files.Where(f => f.Problem != null).Select(f => f.Name + ": " + f.Problem));
                    return Json(new { status = false, message = "No documents found in the master list. " + why });
                }

                IWorkbook workbook = BuildTemplate(out ISheet templateSheet);
                MasterListConverter.Write(workbook, templateSheet, result);

                MemoryStream stream = new MemoryStream();
                workbook.Write(new NonClosingStreamWrapper(stream));

                // JSON instead of the bare file (2026-11-17): besides the filled template the page
                // gets the whole conversion log, so it can show every problem - and every code the
                // system does not know yet - in one popup instead of only in column N of the file.
                return Json(new
                {
                    status = true,
                    fileName = "Import_Template_From_Master_List_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx",
                    file = System.Convert.ToBase64String(stream.ToArray()),
                    rows = result.Rows.Count,
                    errorRows = result.ErrorRows,
                    warningRows = result.WarningRows,
                    filesGiven = result.FilesGiven,
                    unusedFiles = result.UnusedFiles,
                    files = result.Files.Select(f => new { name = f.Name, documents = f.Documents, rows = f.Rows, problem = f.Problem }),
                    missingCodes = result.MissingCodes.Select(x => new
                    {
                        type = x.Type,
                        code = x.Code,
                        severity = x.IsError ? "error" : "warning",
                        usedIn = string.Join(", ", x.UsedIn),
                        documents = x.Documents,
                        masterLists = x.MasterLists
                    }),
                    // One line per message; a message repeated on every revision row of the same
                    // document is shown once, with the revisions it applies to.
                    log = result.Rows
                        .SelectMany(r => r.Errors.Select(e => (Row: r, Level: "error", Message: e))
                            .Concat(r.Warnings.Select(w => (Row: r, Level: "warning", Message: w))))
                        .GroupBy(x => (x.Row.Source, x.Row.DocumentCode, x.Level, x.Message))
                        .Select(g => new
                        {
                            source = g.Key.Source,
                            documentCode = g.Key.DocumentCode,
                            revisions = string.Join(", ", g.Where(x => x.Row.Revision != null).Select(x => x.Row.Revision.Value).Distinct()),
                            level = g.Key.Level,
                            message = g.Key.Message
                        })
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = UnexpectedError(ex, "converting the master list") });
            }
        }

        [HttpPost]
        [RequestSizeLimit(MaxImportRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxImportRequestBytes)]
        public JsonResult ProcessImport(IFormFile excelFile, IFormFile zipFile, bool commitMode)
        {
            JsonResult denied = DenyWithoutFunction("LEGACYDOCUMENTIMPORT-IMPORT");
            if (denied != null)
            {
                return denied;
            }

            try
            {
                if (!HasMenuAccess("/LegacyDocumentImport/Index"))
                {
                    return Json(new { status = false, message = "You are not authorized to use this feature." });
                }

                if (excelFile == null || excelFile.Length == 0)
                {
                    return Json(new { status = false, message = "Excel file is required." });
                }

                if (zipFile == null || zipFile.Length == 0)
                {
                    return Json(new { status = false, message = "ZIP file is required." });
                }

                if (excelFile.Length > MaxExcelBytes)
                {
                    return Json(new { status = false, message = "The Excel file is " + SizeText(excelFile.Length) + "; the limit is 10 MB." });
                }

                if (zipFile.Length > MaxZipBytes)
                {
                    return Json(new { status = false, message = "The ZIP file is " + SizeText(zipFile.Length) + "; the limit is 100 MB. Split the documents into several ZIP files and import them in batches." });
                }

                List<LegacyDocumentImportRow> rows;
                try
                {
                    rows = ParseExcel(excelFile);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Legacy document import: Excel file '{File}' could not be read", excelFile.FileName);
                    return Json(new { status = false, message = "The Excel file cannot be read. Save it as an Excel Workbook (.xlsx) based on Download Template, then try again." });
                }

                if (rows.Count == 0)
                {
                    return Json(new { status = false, message = "No data rows found in the Excel file. Fill the Template sheet from row 2 onwards." });
                }

                IList<DocumentMaintenance> existingDocs = documentMaintenanceRepo.Search(new DocumentMaintenance(), null, db, null, null);
                Dictionary<string, int> existingCodeRevisions = existingDocs
                    .Where(x => !string.IsNullOrEmpty(x.DOCUMENT_CODE))
                    .GroupBy(x => x.DOCUMENT_CODE.ToUpper())
                    .ToDictionary(g => g.Key, g => g.Max(x => x.REVISION ?? 0));

                AssignRevisionGroups(rows, existingCodeRevisions);

                ZipArchive archive;
                try
                {
                    archive = new ZipArchive(zipFile.OpenReadStream(), ZipArchiveMode.Read);
                }
                catch (InvalidDataException ex)
                {
                    logger.LogWarning(ex, "Legacy document import: ZIP file '{File}' could not be opened", zipFile.FileName);
                    return Json(new { status = false, message = "The ZIP file cannot be opened. It may be damaged or not a .zip archive (RAR/7z are not supported)." });
                }

                using (archive)
                {
                    Dictionary<string, ZipArchiveEntry> zipEntries = archive.Entries
                        .Where(x => !string.IsNullOrEmpty(x.Name))
                        .GroupBy(x => x.Name.ToLower())
                        .ToDictionary(g => g.Key, g => g.First());

                    IList<DocumentMaster> categories = documentMasterRepo.Search(new DocumentMaster(), db, null, null);
                    IList<DivisionMaster> divisions = divisionMasterRepo.Search(new DivisionMaster(), db, null, null);
                    IList<DepartmentMaster> departments = departmentMasterRepo.Search(new DepartmentMaster(), db, null, null);
                    IList<MSystem> classifications = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "CLASSIFIED" }, db, null, null);

                    foreach (LegacyDocumentImportRow row in rows)
                    {
                        ValidateRow(row, zipEntries, categories, divisions, departments, classifications);
                    }

                    string DistributionText(LegacyDocumentImportRow r) =>
                        string.Join(", ", r.ResolvedDistribution.Select(d => departments.FirstOrDefault(p => p.DEPARTMENT_ID == d.DepartmentId)?.DEPARTMENT_CODE + " " + d.Date.ToString("dd-MM-yyyy")));

                    if (!commitMode)
                    {
                        return Json(new
                        {
                            status = true,
                            data = rows.Select(x => new
                            {
                                rowNumber = x.RowNumber,
                                fileName = x.FileName,
                                documentCode = x.DocumentCode,
                                documentName = x.DocumentName,
                                valid = x.Valid,
                                isCurrentRevision = x.IsCurrentRevision,
                                isHistoryOnlyAppend = x.IsHistoryOnlyAppend,
                                revision = x.Revision,
                                distribution = DistributionText(x),
                                errors = x.Errors
                            }),
                            validCount = rows.Count(x => x.Valid),
                            invalidCount = rows.Count(x => !x.Valid)
                        });
                    }

                    // All-or-nothing (request Hendra 2026-08-27): kalau ADA SATU SAJA
                    // baris tidak valid, seluruh batch ditolak sebelum menyentuh
                    // database/disk sama sekali - tidak ada dokumen yang tersimpan
                    // sebagian. Mencegah kondisi tidak sinkron (mis. revisi aktif
                    // tersimpan tapi histori lamanya gagal, atau sebagian Document
                    // Code masuk sementara yang lain ketinggalan karena typo di baris
                    // lain). User harus perbaiki semua error dulu (lihat hasil Preview)
                    // baru bisa Import.
                    if (rows.Any(x => !x.Valid))
                    {
                        int invalidCount = rows.Count(x => !x.Valid);
                        return Json(new
                        {
                            status = false,
                            message = "Import cancelled: " + invalidCount + " row(s) are not valid, so nothing was saved. Fix every error shown in the preview, then validate again."
                        });
                    }

                    string username = GetLoginUsername();
                    MSystem uploadFolder = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "DOCUMENT_TRANSACTION" }, db);
                    string webRootPath = Environment.WebRootPath;
                    string folderPath = "/Upload/" + uploadFolder.SYSTEM_VALUE.Trim();
                    string fullFolderPath = DMS.Services.UploadPath.Root + folderPath;

                    if (!Directory.Exists(fullFolderPath))
                    {
                        Directory.CreateDirectory(fullFolderPath);
                    }

                    List<ImportRowResult> results = new List<ImportRowResult>();

                    // Process per Document Code group so the current (highest) revision is
                    // always saved first - history rows for that same document only get
                    // inserted if the active document was saved successfully.
                    var groups = rows
                        .GroupBy(x => (x.DocumentCode ?? "").Trim().ToUpper())
                        .Select(g => g.OrderByDescending(x => x.IsCurrentRevision).ThenBy(x => x.Revision ?? 0).ToList());

                    foreach (List<LegacyDocumentImportRow> group in groups)
                    {
                        bool currentRevisionSaved = false;

                        foreach (LegacyDocumentImportRow row in group)
                        {
                            if (!row.Valid)
                            {
                                results.Add(new ImportRowResult { Row = row, Status = false, Message = string.Join("; ", row.Errors) });
                                continue;
                            }

                            if (row.IsCurrentRevision)
                            {
                                string savedFullPath = null;

                                try
                                {
                                    ZipArchiveEntry entry = zipEntries[row.FileName.ToLower()];
                                    string extension = Path.GetExtension(entry.Name);
                                    string safeName = row.DocumentCode.Replace(" ", "_").Replace("/", "_");
                                    string savedFileName = safeName + "-" + DateTime.Now.ToFileTime() + extension;
                                    savedFullPath = fullFolderPath + savedFileName;

                                    using (Stream entryStream = entry.Open())
                                    using (FileStream fileStream = new FileStream(savedFullPath, FileMode.Create))
                                    {
                                        entryStream.CopyTo(fileStream);
                                    }

                                    DocumentMaintenance data = new DocumentMaintenance
                                    {
                                        DOCUMENT_CODE = row.DocumentCode,
                                        DOCUMENT_TRANSACTION_NAME = row.DocumentName,
                                        DOCUMENT_ID = row.ResolvedDocumentId,
                                        LEVEL_CODE = row.ResolvedLevelCode,
                                        DIVISION = row.DivisionCode,
                                        DEPARTMENT_ID = row.ResolvedDepartmentId,
                                        CLASSIFIED = int.Parse(row.ClassificationCode),
                                        REVISION = row.Revision,
                                        DOCUMENT_DATE = row.DocumentDate,
                                        FILE_PATH = folderPath + savedFileName,
                                        DOCUMENT_CREATOR = string.IsNullOrEmpty(row.DocumentCreator) ? username : row.DocumentCreator,
                                        REASON = ReasonOf(row, "Digitalisasi dokumen lama")
                                    };

                                    string distribution = row.ResolvedDistribution.Count == 0 ? null
                                        : string.Join(";", row.ResolvedDistribution.Select(d => d.DepartmentId + "|" + d.Date.ToString("yyyy-MM-dd")));
                                    DBResult result = documentMaintenanceRepo.ImportLegacyInsert(data, username, db, distribution);
                                    currentRevisionSaved = result.status;

                                    if (result.status)
                                    {
                                        results.Add(new ImportRowResult { Row = row, Status = true, Message = row.ResolvedDistribution.Count == 0 ? "Imported" : "Imported, distributed to " + row.ResolvedDistribution.Count + " department(s)" });
                                    }
                                    else
                                    {
                                        if (System.IO.File.Exists(savedFullPath))
                                        {
                                            System.IO.File.Delete(savedFullPath);
                                        }
                                        results.Add(new ImportRowResult { Row = row, Status = false, Message = FriendlyDbMessage(result.message, "saving row " + row.RowNumber) });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    if (savedFullPath != null && System.IO.File.Exists(savedFullPath))
                                    {
                                        System.IO.File.Delete(savedFullPath);
                                    }
                                    results.Add(new ImportRowResult { Row = row, Status = false, Message = UnexpectedError(ex, "saving row " + row.RowNumber) });
                                }
                            }
                            else
                            {
                                // History-only append groups (Document Code already has an active
                                // document outside this batch) never have a "current" row to wait
                                // on - they go straight to the history insert below.
                                if (!row.IsHistoryOnlyAppend && !currentRevisionSaved)
                                {
                                    results.Add(new ImportRowResult { Row = row, Status = false, Message = "Skipped: the current revision for Document Code '" + row.DocumentCode + "' was not saved successfully." });
                                    continue;
                                }

                                try
                                {
                                    string historyFilePath = null;

                                    if (!string.IsNullOrWhiteSpace(row.FileName) && zipEntries.TryGetValue(row.FileName.ToLower(), out ZipArchiveEntry historyEntry))
                                    {
                                        string extension = Path.GetExtension(historyEntry.Name);
                                        string safeName = row.DocumentCode.Replace(" ", "_").Replace("/", "_");
                                        string savedFileName = safeName + "-rev" + row.Revision + "-" + DateTime.Now.ToFileTime() + extension;
                                        string savedFullPath = fullFolderPath + savedFileName;

                                        using (Stream entryStream = historyEntry.Open())
                                        using (FileStream fileStream = new FileStream(savedFullPath, FileMode.Create))
                                        {
                                            entryStream.CopyTo(fileStream);
                                        }

                                        historyFilePath = folderPath + savedFileName;
                                    }

                                    DocumentMaintenance historyData = new DocumentMaintenance
                                    {
                                        DOCUMENT_CODE = row.DocumentCode,
                                        DOCUMENT_TRANSACTION_NAME = row.DocumentName,
                                        DOCUMENT_ID = row.ResolvedDocumentId,
                                        LEVEL_CODE = row.ResolvedLevelCode,
                                        DIVISION = row.DivisionCode,
                                        DEPARTMENT_ID = row.ResolvedDepartmentId,
                                        CLASSIFIED = int.Parse(row.ClassificationCode),
                                        REVISION = row.Revision,
                                        DOCUMENT_DATE = row.DocumentDate,
                                        FILE_PATH = historyFilePath,
                                        DOCUMENT_CREATOR = string.IsNullOrEmpty(row.DocumentCreator) ? username : row.DocumentCreator,
                                        REASON = ReasonOf(row, "Digitalisasi dokumen lama (histori)")
                                    };

                                    DBResult historyResult = documentMaintenanceRepo.ImportLegacyHistoryInsert(historyData, username, db);

                                    if (historyResult.status)
                                    {
                                        results.Add(new ImportRowResult { Row = row, Status = true, Message = "Imported as history (revision " + row.Revision + ")" });
                                    }
                                    else
                                    {
                                        results.Add(new ImportRowResult { Row = row, Status = false, Message = FriendlyDbMessage(historyResult.message, "saving row " + row.RowNumber) });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    results.Add(new ImportRowResult { Row = row, Status = false, Message = UnexpectedError(ex, "saving row " + row.RowNumber) });
                                }
                            }
                        }
                    }

                    return Json(new
                    {
                        status = true,
                        // Same columns as the preview, in Excel row order (rows are saved per
                        // Document Code group, so the save order differs) - 2026-11-17.
                        data = results.OrderBy(x => x.Row.RowNumber).Select(x => new
                        {
                            rowNumber = x.Row.RowNumber,
                            fileName = x.Row.FileName,
                            documentCode = x.Row.DocumentCode,
                            documentName = x.Row.DocumentName,
                            revision = x.Row.Revision,
                            distribution = DistributionText(x.Row),
                            status = x.Status,
                            message = x.Message
                        }),
                        successCount = results.Count(x => x.Status),
                        failCount = results.Count(x => !x.Status)
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = UnexpectedError(ex, commitMode ? "importing the documents" : "validating the files") });
            }
        }

        // Delegates all stream operations except Dispose/Close, so callers like NPOI's
        // workbook.Write(stream) can't close a stream the caller still needs afterward.
        private class NonClosingStreamWrapper : Stream
        {
            private readonly Stream inner;

            public NonClosingStreamWrapper(Stream inner)
            {
                this.inner = inner;
            }

            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => inner.CanSeek;
            public override bool CanWrite => inner.CanWrite;
            public override long Length => inner.Length;
            public override long Position { get => inner.Position; set => inner.Position = value; }
            public override void Flush() => inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
            public override void SetLength(long value) => inner.SetLength(value);
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

            protected override void Dispose(bool disposing)
            {
                // Intentionally does not dispose the inner stream.
            }
        }

        private class ImportRowResult
        {
            public LegacyDocumentImportRow Row { get; set; }
            public bool Status { get; set; }
            public string Message { get; set; }
        }

        // Groups rows by Document Code and marks the highest-Revision row in each group as
        // IsCurrentRevision - that row becomes the active document; the rest become history
        // entries carrying the document's past revisions (see plan: legacy import history migration).
        //
        // If the Document Code already has an active document in the system (existingCodeRevisions),
        // the whole group is instead treated as a history-only backfill: no row becomes "current"
        // (the real active document lives outside this batch), and every row's Revision must be
        // lower than the currently active revision - this lets a document already imported/created
        // earlier still receive its missing older history afterwards.
        private void AssignRevisionGroups(List<LegacyDocumentImportRow> rows, Dictionary<string, int> existingCodeRevisions)
        {
            var groups = rows
                .Where(x => !string.IsNullOrWhiteSpace(x.DocumentCode))
                .GroupBy(x => x.DocumentCode.Trim().ToUpper());

            foreach (var group in groups)
            {
                List<LegacyDocumentImportRow> groupRows = group.ToList();
                string codeUpper = group.Key;

                HashSet<int> duplicateRevisions = groupRows
                    .Where(x => x.Revision != null)
                    .GroupBy(x => x.Revision.Value)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToHashSet();

                foreach (LegacyDocumentImportRow row in groupRows)
                {
                    if (row.Revision != null && duplicateRevisions.Contains(row.Revision.Value))
                    {
                        row.AddError("Revision " + row.Revision + " is used by more than one row for Document Code '" + row.DocumentCode + "'.");
                    }
                }

                if (groupRows.Count > 1)
                {
                    LegacyDocumentImportRow first = groupRows[0];
                    foreach (LegacyDocumentImportRow row in groupRows.Skip(1))
                    {
                        if (!string.Equals(row.DocumentName, first.DocumentName, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(row.CategoryCode, first.CategoryCode, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(row.DivisionCode, first.DivisionCode, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(row.DepartmentCode, first.DepartmentCode, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(row.ClassificationCode, first.ClassificationCode, StringComparison.OrdinalIgnoreCase))
                        {
                            row.AddError("Row is not consistent with other revision rows for Document Code '" + row.DocumentCode + "' (Document Name/Category/Division/Department/Classification must match).");
                        }
                    }
                }

                if (existingCodeRevisions.TryGetValue(codeUpper, out int activeRevision))
                {
                    foreach (LegacyDocumentImportRow row in groupRows)
                    {
                        row.IsHistoryOnlyAppend = true;

                        if (row.Revision != null && row.Revision.Value >= activeRevision)
                        {
                            row.AddError("Revision " + row.Revision + " is not lower than the currently active revision (" + activeRevision + ") for Document Code '" + row.DocumentCode + "'. Legacy Import can only backfill older history for a document that already exists - use Document Maintenance to revise the active document instead.");
                        }
                    }
                }
                else
                {
                    int? maxRevision = groupRows.Max(x => x.Revision);
                    if (maxRevision != null)
                    {
                        groupRows.First(x => x.Revision == maxRevision).IsCurrentRevision = true;
                    }
                }
            }
        }

        private void ValidateRow(LegacyDocumentImportRow row, Dictionary<string, ZipArchiveEntry> zipEntries,
            IList<DocumentMaster> categories, IList<DivisionMaster> divisions, IList<DepartmentMaster> departments,
            IList<MSystem> classifications)
        {
            if (string.IsNullOrWhiteSpace(row.FileName))
            {
                if (row.IsCurrentRevision)
                {
                    row.AddError("File Name is required for the current (highest) revision row.");
                }
            }
            else if (!zipEntries.ContainsKey(row.FileName.ToLower()))
            {
                row.AddError("File '" + row.FileName + "' was not found inside the ZIP.");
            }

            if (string.IsNullOrWhiteSpace(row.DocumentCode))
            {
                row.AddError("Document Code is required.");
            }

            if (string.IsNullOrWhiteSpace(row.DocumentName))
            {
                row.AddError("Document Name is required.");
            }

            if (string.IsNullOrWhiteSpace(row.CategoryCode))
            {
                row.AddError("Category Code is required.");
            }
            else
            {
                DocumentMaster category = categories.FirstOrDefault(x => string.Equals(x.DOCUMENT_CODE, row.CategoryCode, StringComparison.OrdinalIgnoreCase));
                if (category == null)
                {
                    row.AddError("Category Code '" + row.CategoryCode + "' does not match any Document Category.");
                }
                else
                {
                    row.ResolvedDocumentId = category.DOCUMENT_ID;
                    row.ResolvedLevelCode = category.LEVEL;
                }
            }

            DepartmentMaster matchedDepartment = null;
            if (string.IsNullOrWhiteSpace(row.DivisionCode))
            {
                row.AddError("Division Code is required.");
            }
            else if (!divisions.Any(x => string.Equals(x.DIVISION_CODE, row.DivisionCode, StringComparison.OrdinalIgnoreCase)))
            {
                row.AddError("Division Code '" + row.DivisionCode + "' does not match any Division.");
            }

            if (string.IsNullOrWhiteSpace(row.DepartmentCode))
            {
                row.AddError("Department Code is required.");
            }
            else
            {
                matchedDepartment = departments.FirstOrDefault(x => string.Equals(x.DEPARTMENT_CODE, row.DepartmentCode, StringComparison.OrdinalIgnoreCase));
                if (matchedDepartment == null)
                {
                    row.AddError("Department Code '" + row.DepartmentCode + "' does not match any Department.");
                }
                else
                {
                    // DepartmentMasterRepo.Search (sp_DepartmentMaster_Search) returns DIVISION
                    // formatted as "{code} - {name}" for dropdown display elsewhere in the app -
                    // extract just the code before comparing against the Excel's raw Division Code.
                    string matchedDivisionCode = matchedDepartment.DIVISION?.Split(" - ", 2)[0].Trim();
                    if (!string.IsNullOrWhiteSpace(row.DivisionCode) && !string.Equals(matchedDivisionCode, row.DivisionCode, StringComparison.OrdinalIgnoreCase))
                    {
                        row.AddError("Department '" + row.DepartmentCode + "' does not belong to Division '" + row.DivisionCode + "'.");
                    }
                    else
                    {
                        row.ResolvedDepartmentId = matchedDepartment.DEPARTMENT_ID;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(row.ClassificationCode))
            {
                row.AddError("Classification Code is required.");
            }
            else if (!classifications.Any(x => string.Equals(x.SYSTEM_CODE, row.ClassificationCode, StringComparison.OrdinalIgnoreCase)))
            {
                row.AddError("Classification Code '" + row.ClassificationCode + "' is not valid.");
            }

            if (row.Revision == null || row.Revision < 0)
            {
                row.AddError("Revision must be a non-negative number.");
            }

            if (row.DocumentDate == null)
            {
                row.AddError("Document Date is required and must be a valid date.");
            }

            ValidateDistribution(row, departments);
        }

        // Column L "APP:12-04-2017; INF:12-04-2017" -> departments + received date (2026-11-12).
        // Only the active revision can carry it: the import registers that revision in P4D.
        private void ValidateDistribution(LegacyDocumentImportRow row, IList<DepartmentMaster> departments)
        {
            row.ResolvedDistribution.Clear();
            if (string.IsNullOrWhiteSpace(row.Distribution)) return;

            if (!row.IsCurrentRevision)
            {
                row.AddError(row.IsHistoryOnlyAppend
                    ? "Distribution cannot be imported for a document that is already in the system; use P4D Maintenance."
                    : "Distribution can only be filled on the latest revision row of the document.");
                return;
            }

            string[] formats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
            foreach (string part in row.Distribution.Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string entry = part.Trim();
                if (entry.Length == 0) continue;

                string[] pieces = entry.Split(':', 2);
                string code = pieces[0].Trim();
                if (pieces.Length < 2 || !DateTime.TryParseExact(pieces[1].Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    row.AddError("Distribution \"" + entry + "\" must be written as DEPARTMENT:DD-MM-YYYY.");
                    continue;
                }

                DepartmentMaster department = departments.FirstOrDefault(x => string.Equals(x.DEPARTMENT_CODE, code, StringComparison.OrdinalIgnoreCase));
                if (department == null)
                {
                    row.AddError("Distribution department '" + code + "' does not match any Department.");
                    continue;
                }
                if (date > DateTime.Today)
                {
                    row.AddError("Distribution date " + date.ToString("dd-MM-yyyy") + " for " + code + " is in the future.");
                    continue;
                }
                if (row.ResolvedDistribution.Any(x => x.DepartmentId == department.DEPARTMENT_ID))
                {
                    row.AddError("Distribution department '" + code + "' is written more than once.");
                    continue;
                }
                row.ResolvedDistribution.Add((department.DEPARTMENT_ID.Value, date));
            }
        }

        private List<LegacyDocumentImportRow> ParseExcel(IFormFile excelFile)
        {
            List<LegacyDocumentImportRow> rows = new List<LegacyDocumentImportRow>();

            using (Stream stream = excelFile.OpenReadStream())
            {
                IWorkbook workbook = new XSSFWorkbook(stream);
                ISheet sheet = workbook.GetSheetAt(0);

                for (int i = 1; i <= sheet.LastRowNum; i++)
                {
                    IRow excelRow = sheet.GetRow(i);
                    if (excelRow == null || IsRowEmpty(excelRow))
                    {
                        continue;
                    }

                    LegacyDocumentImportRow row = new LegacyDocumentImportRow
                    {
                        RowNumber = i + 1,
                        FileName = GetCellString(excelRow, 0),
                        DocumentCode = GetCellString(excelRow, 1),
                        DocumentName = GetCellString(excelRow, 2),
                        CategoryCode = GetCellString(excelRow, 3),
                        DivisionCode = GetCellString(excelRow, 4),
                        DepartmentCode = GetCellString(excelRow, 5),
                        ClassificationCode = GetCellString(excelRow, 6),
                        Revision = GetCellInt(excelRow, 7),
                        DocumentDate = GetCellDate(excelRow, 8),
                        DocumentCreator = GetCellString(excelRow, 9),
                        RevisionReason = GetCellString(excelRow, 10),
                        Distribution = GetCellString(excelRow, 11)
                    };

                    rows.Add(row);
                }
            }

            return rows;
        }

        // Column K of the template (optional): reason of that revision, e.g. taken from the
        // comments of the master list. Empty -> the fixed migration text.
        private static string ReasonOf(LegacyDocumentImportRow row, string fallback)
        {
            string reason = row.RevisionReason?.Trim();
            if (string.IsNullOrEmpty(reason)) return fallback;
            return reason.Length > 255 ? reason.Substring(0, 255) : reason;
        }

        private bool IsRowEmpty(IRow row)
        {
            for (int i = 0; i < 10; i++)
            {
                if (!string.IsNullOrWhiteSpace(GetCellString(row, i)))
                {
                    return false;
                }
            }
            return true;
        }

        private string GetCellString(IRow row, int index)
        {
            ICell cell = row.GetCell(index);
            if (cell == null)
            {
                return null;
            }

            switch (cell.CellType)
            {
                case CellType.String:
                    return cell.StringCellValue.Trim();
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(cell))
                    {
                        return cell.DateCellValue.ToString("dd-MM-yyyy");
                    }
                    return cell.NumericCellValue.ToString();
                case CellType.Formula:
                    return cell.ToString().Trim();
                default:
                    return null;
            }
        }

        private int? GetCellInt(IRow row, int index)
        {
            string value = GetCellString(row, index);
            if (int.TryParse(value, out int result))
            {
                return result;
            }
            return null;
        }

        private DateTime? GetCellDate(IRow row, int index)
        {
            ICell cell = row.GetCell(index);
            if (cell == null)
            {
                return null;
            }

            if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
            {
                return cell.DateCellValue;
            }

            string value = GetCellString(row, index);
            if (DateTime.TryParseExact(value, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime parsed))
            {
                return parsed;
            }
            if (DateTime.TryParse(value, out DateTime parsedGeneral))
            {
                return parsedGeneral;
            }
            return null;
        }
    }
}
