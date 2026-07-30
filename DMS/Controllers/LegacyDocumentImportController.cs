using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.IO.Compression;

namespace DMS.Controllers
{
    public class LegacyDocumentImportController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;

        public LegacyDocumentImportController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;
        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private DivisionMasterRepo divisionMasterRepo = DivisionMasterRepo.Instance;
        private DepartmentMasterRepo departmentMasterRepo = DepartmentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;

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

            IList<DocumentMaster> categories = documentMasterRepo.Search(new DocumentMaster(), db, null, null);
            IList<DivisionMaster> divisions = divisionMasterRepo.Search(new DivisionMaster(), db, null, null);
            IList<DepartmentMaster> departments = departmentMasterRepo.Search(new DepartmentMaster(), db, null, null);
            IList<MSystem> classifications = mSystemRepo.Search(new MSystem { SYSTEM_TYPE = "CLASSIFIED" }, db, null, null);

            IWorkbook workbook = new XSSFWorkbook();
            {
                ISheet templateSheet = workbook.CreateSheet("Template");
                string[] headers = { "File Name", "Document Code", "Document Name", "Category Code", "Division Code", "Department Code", "Classification Code", "Revision", "Document Date (DD-MM-YYYY)", "Document Creator" };
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

                // NPOI's Write(Stream) closes the stream it's given (this NPOI version has no
                // leaveOpen overload), so write through a wrapper that swallows Dispose/Close
                // and keep the real MemoryStream readable afterward.
                MemoryStream stream = new MemoryStream();
                workbook.Write(new NonClosingStreamWrapper(stream));
                stream.Position = 0;
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Import_Legacy_Document_Template.xlsx");
            }
        }

        public JsonResult ProcessImport(IFormFile excelFile, IFormFile zipFile, bool commitMode)
        {
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

                List<LegacyDocumentImportRow> rows = ParseExcel(excelFile);

                if (rows.Count == 0)
                {
                    return Json(new { status = false, message = "No data rows found in the Excel file." });
                }

                IList<DocumentMaintenance> existingDocs = documentMaintenanceRepo.Search(new DocumentMaintenance(), null, db, null, null);
                Dictionary<string, int> existingCodeRevisions = existingDocs
                    .Where(x => !string.IsNullOrEmpty(x.DOCUMENT_CODE))
                    .GroupBy(x => x.DOCUMENT_CODE.ToUpper())
                    .ToDictionary(g => g.Key, g => g.Max(x => x.REVISION ?? 0));

                AssignRevisionGroups(rows, existingCodeRevisions);

                using (ZipArchive archive = new ZipArchive(zipFile.OpenReadStream(), ZipArchiveMode.Read))
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
                                errors = x.Errors
                            }),
                            validCount = rows.Count(x => x.Valid),
                            invalidCount = rows.Count(x => !x.Valid)
                        });
                    }

                    string username = GetLoginUsername();
                    MSystem uploadFolder = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "DOCUMENT_TRANSACTION" }, db);
                    string webRootPath = Environment.WebRootPath;
                    string folderPath = "/Upload/" + uploadFolder.SYSTEM_VALUE.Trim();
                    string fullFolderPath = webRootPath + folderPath;

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
                                results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = false, Message = string.Join("; ", row.Errors) });
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
                                        REASON = "Digitalisasi dokumen lama"
                                    };

                                    DBResult result = documentMaintenanceRepo.ImportLegacyInsert(data, username, db);
                                    currentRevisionSaved = result.status;

                                    if (result.status)
                                    {
                                        results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = true, Message = "Imported" });
                                    }
                                    else
                                    {
                                        if (System.IO.File.Exists(savedFullPath))
                                        {
                                            System.IO.File.Delete(savedFullPath);
                                        }
                                        results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = false, Message = result.message });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = false, Message = ex.Message });
                                }
                            }
                            else
                            {
                                // History-only append groups (Document Code already has an active
                                // document outside this batch) never have a "current" row to wait
                                // on - they go straight to the history insert below.
                                if (!row.IsHistoryOnlyAppend && !currentRevisionSaved)
                                {
                                    results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = false, Message = "Skipped: the current revision for Document Code '" + row.DocumentCode + "' was not saved successfully." });
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
                                        REASON = "Digitalisasi dokumen lama (histori)"
                                    };

                                    DBResult historyResult = documentMaintenanceRepo.ImportLegacyHistoryInsert(historyData, username, db);

                                    if (historyResult.status)
                                    {
                                        results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = true, Message = "Imported as history (revision " + row.Revision + ")" });
                                    }
                                    else
                                    {
                                        results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = false, Message = historyResult.message });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = false, Message = ex.Message });
                                }
                            }
                        }
                    }

                    return Json(new
                    {
                        status = true,
                        data = results.Select(x => new
                        {
                            rowNumber = x.RowNumber,
                            documentCode = x.DocumentCode,
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
                return Json(new { status = false, message = ex.Message });
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
            public int RowNumber { get; set; }
            public string DocumentCode { get; set; }
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
                        DocumentCreator = GetCellString(excelRow, 9)
                    };

                    rows.Add(row);
                }
            }

            return rows;
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
