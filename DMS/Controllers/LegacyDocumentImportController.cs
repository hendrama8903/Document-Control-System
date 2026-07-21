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
                return StatusCode(403);
            }

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
                    IList<DocumentMaintenance> existingDocs = documentMaintenanceRepo.Search(new DocumentMaintenance(), null, db, null, null);
                    HashSet<string> existingCodes = existingDocs
                        .Where(x => !string.IsNullOrEmpty(x.DOCUMENT_CODE))
                        .Select(x => x.DOCUMENT_CODE.ToUpper())
                        .ToHashSet();
                    HashSet<string> seenInBatch = new HashSet<string>();

                    foreach (LegacyDocumentImportRow row in rows)
                    {
                        ValidateRow(row, zipEntries, categories, divisions, departments, classifications, existingCodes, seenInBatch);
                    }

                    if (!commitMode)
                    {
                        return Json(new
                        {
                            status = true,
                            data = rows.Select(x => new
                            {
                                x.RowNumber,
                                x.FileName,
                                x.DocumentCode,
                                x.DocumentName,
                                x.Valid,
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

                    foreach (LegacyDocumentImportRow row in rows)
                    {
                        if (!row.Valid)
                        {
                            results.Add(new ImportRowResult { RowNumber = row.RowNumber, DocumentCode = row.DocumentCode, Status = false, Message = string.Join("; ", row.Errors) });
                            continue;
                        }

                        try
                        {
                            ZipArchiveEntry entry = zipEntries[row.FileName.ToLower()];
                            string extension = Path.GetExtension(entry.Name);
                            string safeName = row.DocumentCode.Replace(" ", "_").Replace("/", "_");
                            string savedFileName = safeName + "-" + DateTime.Now.ToFileTime() + extension;
                            string savedFullPath = fullFolderPath + savedFileName;

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

                    return Json(new
                    {
                        status = true,
                        data = results,
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

        private void ValidateRow(LegacyDocumentImportRow row, Dictionary<string, ZipArchiveEntry> zipEntries,
            IList<DocumentMaster> categories, IList<DivisionMaster> divisions, IList<DepartmentMaster> departments,
            IList<MSystem> classifications, HashSet<string> existingCodes, HashSet<string> seenInBatch)
        {
            if (string.IsNullOrWhiteSpace(row.FileName))
            {
                row.AddError("File Name is required.");
            }
            else if (!zipEntries.ContainsKey(row.FileName.ToLower()))
            {
                row.AddError("File '" + row.FileName + "' was not found inside the ZIP.");
            }

            if (string.IsNullOrWhiteSpace(row.DocumentCode))
            {
                row.AddError("Document Code is required.");
            }
            else
            {
                string codeUpper = row.DocumentCode.ToUpper();
                if (existingCodes.Contains(codeUpper))
                {
                    row.AddError("Document Code '" + row.DocumentCode + "' is already registered in the system.");
                }
                else if (!seenInBatch.Add(codeUpper))
                {
                    row.AddError("Document Code '" + row.DocumentCode + "' appears more than once in this batch.");
                }
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
                else if (!string.IsNullOrWhiteSpace(row.DivisionCode) && !string.Equals(matchedDepartment.DIVISION, row.DivisionCode, StringComparison.OrdinalIgnoreCase))
                {
                    row.AddError("Department '" + row.DepartmentCode + "' does not belong to Division '" + row.DivisionCode + "'.");
                }
                else
                {
                    row.ResolvedDepartmentId = matchedDepartment.DEPARTMENT_ID;
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
