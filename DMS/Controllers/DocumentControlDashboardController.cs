using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Hubs;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Hangfire;
using MDC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Globalization;
using System.Text;
//Recommit DINA
namespace DMS.Controllers
{
    public class DocumentControlDashboardController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;

        public DocumentControlDashboardController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private DocumentControlDashboardRepo DocumentControlDashboardRepo = DocumentControlDashboardRepo.Instance;
        private P4DMaintenanceRepo P4DMaintenanceRepo = P4DMaintenanceRepo.Instance;
        private DocumentFolderRepo DocumentFolderRepo = DocumentFolderRepo.Instance;

        public IActionResult Index(string DOCUMENT_CODE)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                string param = "/DocumentControlDashboard/Index?DOCUMENT_CODE=" + DOCUMENT_CODE;
                return RedirectToAction("Login", "Auth", new { param });
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentControlDashboard/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["DocumentControlAccess"] = GetDocumentAccessControl();
            ViewData["ExportPdf"] = Services.DocumentAccessService.HasFunction(HttpContext, Services.DocumentExportService.FunctionId);


            // Management System filter (2026-11-19): a Document Control of exactly one system starts on it
            var systems = Services.MgmtSystemService.AllSystems(db);
            string login = HttpContext.Session.GetString("USERNAME");
            var mine = systems.Where(s => Services.MgmtSystemService.IsDocumentControlFor(db, login, s.Code)).Select(s => s.Code).ToList();
            ViewData["MgmtSystems"] = systems;
            ViewData["DefaultMgmtSystem"] = mine.Count == 1 && mine.Count < systems.Count ? mine[0] : null;

            ViewData["Title"] = "Document Control";

            return View();
        }

        // Rincian per-department untuk popup "Acceptance Detail" (QMS-only, klik
        // dari kolom Acceptance di grid) - reuse SearchDocumentDistribution yang
        // sama persis dipakai popup Distribution di P4D, supaya datanya (termasuk
        // ACCEPTED_FLAG) konsisten (request Hendra 2026-08-15).
        public JsonResult SearchDistributionDetail(int documentTransactionId)
        {
            try
            {
                IList<DocumentDistribution> dataList = P4DMaintenanceRepo.SearchDocumentDistribution(
                    new DocumentDistribution { DOCUMENT_TRANSACTION_ID = documentTransactionId }, db, null, null);

                return Json(new { status = true, data = dataList });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Copy Register (request Hendra 2026-11-11): controlled copies of every revision of the
        // document and their withdrawal. Same role function as Download PDF, which issues them.
        public JsonResult SearchCopyRegister(int documentTransactionId)
        {
            if (!Services.DocumentAccessService.HasFunction(HttpContext, Services.DocumentExportService.FunctionId))
                return Json(new { status = false, message = "You are not authorized to view the copy register." });
            try
            {
                return Json(new { status = true, data = Services.DocumentExportService.CopyRegister(db, documentTransactionId) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult WithdrawCopy(int exportId, string note)
        {
            if (!Services.DocumentAccessService.HasFunction(HttpContext, Services.DocumentExportService.FunctionId))
                return Json(new { status = false, message = "You are not authorized to withdraw copies." });
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note?.Length > 500)
                return Json(new { status = false, message = "The note can be at most 500 characters." });
            try
            {
                Services.DocumentExportService.Withdraw(db, exportId, note, GetLoginUsername());
                return Json(new { status = true, message = "Copy marked as withdrawn." });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 50003)
            {
                return Json(new { status = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(DocumentControlMaintenance data, bool initialMode)
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
                    // OPERATION_TYPE is forced to 1 (real P4D registrations only)
                    // inside DocumentControlDashboardRepo.Search - see the comment
                    // there for why OPERATION_TYPE=2 "Request Document" rows must not
                    // show up here (request user 2026-08-11).
                    var listData = DocumentControlDashboardRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = DocumentControlDashboardRepo.Search(data, db, null, null).Count;
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

        // ---------- Folder tree (request Hendra 2026-08-28) ----------
        // Not gated behind "DocumentControlAccess" (that flag is department-specific,
        // e.g. QMS/QPD - DMS-ADMIN and other departments with access to this menu
        // still don't have it, which hid the whole feature for them). Folders are just
        // an organizational structure, not sensitive data, so anyone who can reach this
        // menu (already checked in Index()) can manage them - same as every other AJAX
        // endpoint in this app having no extra per-action guard by default.

        public JsonResult GetFolderTree()
        {
            try
            {
                IList<DocumentFolder> result = DocumentFolderRepo.GetTree(db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult CreateFolder(string folderName, int? parentId)
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null)
            {
                return denied;
            }

            try
            {
                DBResult result = DocumentFolderRepo.Insert(new DocumentFolder { FOLDER_NAME = folderName, PARENT_ID = parentId }, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult RenameFolder(int folderId, string folderName)
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null)
            {
                return denied;
            }

            try
            {
                DocumentFolder existing = DocumentFolderRepo.GetTree(db).FirstOrDefault(f => f.FOLDER_ID == folderId);
                if (existing == null)
                {
                    return Json(new { status = false, message = "Folder not found" });
                }

                DBResult result = DocumentFolderRepo.Update(new DocumentFolder { FOLDER_ID = folderId, FOLDER_NAME = folderName, PARENT_ID = existing.PARENT_ID }, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteFolder(int folderId)
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null)
            {
                return denied;
            }

            try
            {
                DBResult result = DocumentFolderRepo.Delete(new DocumentFolder { FOLDER_ID = folderId }, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult MoveDocumentsToFolder(List<int> documentTransactionIds, int? folderId)
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null)
            {
                return denied;
            }

            try
            {
                foreach (var id in documentTransactionIds)
                {
                    DBResult result = DocumentControlDashboardRepo.AssignFolder(id, folderId, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        return Json(result);
                    }
                }

                return Json(new { status = true, message = "Document(s) moved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // ---------- Binders (bantex) and "Needs Action" (request Hendra 2026-11-15) ----------
        // A folder can be a master binder or a controlled copy binder kept by a department, see
        // Services/DocumentBinderService and SQL/DocumentBinder_Migration.sql. Binder details
        // follow the folder endpoints above (anyone who reaches this menu); the copies filed in
        // a binder follow the Copy Register (role function Download PDF).

        bool CanManageCopies() =>
            Services.DocumentAccessService.HasFunction(HttpContext, Services.DocumentExportService.FunctionId);

        public JsonResult GetBinderDepartments()
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null) return denied;
            try
            {
                return Json(new { status = true, data = Services.DocumentBinderService.Departments(db) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SaveBinder(int folderId, string folderType, string folderCode, int? departmentId, string location, string remarks)
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null) return denied;

            folderType = string.IsNullOrWhiteSpace(folderType) ? null : folderType.Trim().ToUpperInvariant();
            folderCode = string.IsNullOrWhiteSpace(folderCode) ? null : folderCode.Trim();
            location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
            remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            if (folderCode?.Length > 50 || location?.Length > 200 || remarks?.Length > 500)
                return Json(new { status = false, message = "Code can be at most 50, location 200 and notes 500 characters." });
            try
            {
                Services.DocumentBinderService.Save(db, folderId, folderType, folderCode, departmentId, location, remarks, GetLoginUsername());
                return Json(new { status = true, message = "Binder details saved." });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number >= 50011 && ex.Number <= 50018)
            {
                return Json(new { status = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Binder Index: the copies in circulation of a controlled copy binder (Download PDF
        // function, as the Copy Register) or the documents of a master binder / folder.
        public JsonResult GetBinderContents(int folderId)
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null) return denied;
            try
            {
                DocumentFolder folder = DocumentFolderRepo.GetTree(db).FirstOrDefault(f => f.FOLDER_ID == folderId);
                if (folder == null) return Json(new { status = false, message = "Folder not found" });
                if (folder.FOLDER_TYPE == Services.DocumentBinderService.Copy && !CanManageCopies())
                    return Json(new { status = false, message = "You are not authorized to view the controlled copies of this binder." });

                return Json(new { status = true, folder, data = Services.DocumentBinderService.Contents(db, folderId) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult AssignCopyToBinder(int exportId, int? folderId)
        {
            if (!CanManageCopies())
                return Json(new { status = false, message = "You are not authorized to file controlled copies." });
            try
            {
                Services.DocumentBinderService.AssignCopy(db, exportId, folderId);
                return Json(new { status = true, message = folderId == null ? "Copy taken out of its binder." : "Copy filed in the binder." });
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 50021 || ex.Number == 50022)
            {
                return Json(new { status = false, message = ex.Message });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetActionItems(string mgmtSystem)
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null) return denied;
            try
            {
                // A chip that leads to another menu is shown only to a role that has that menu.
                string menuURLList = HttpContext.Session.GetString("menuURL") ?? "";
                return Json(new
                {
                    status = true,
                    data = Services.DocumentBinderService.GetActionItems(db, mgmtSystem),
                    canRegistration = menuURLList.Contains("/P4DMaintenance/Index"),
                    canPrintRequest = menuURLList.Contains("/CopyRequest/Index")
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Printable index sheet and spine label of a binder (opens in a new tab).
        public IActionResult BinderIndex(int folderId)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
                return RedirectToAction("Login", "Auth");
            string menuURLList = HttpContext.Session.GetString("menuURL") ?? "";
            if (!menuURLList.Contains("/DocumentControlDashboard/Index"))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            DocumentFolder folder = DocumentFolderRepo.GetTree(db).FirstOrDefault(f => f.FOLDER_ID == folderId);
            if (folder == null) return NotFound();
            if (folder.FOLDER_TYPE == Services.DocumentBinderService.Copy && !CanManageCopies())
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            ViewData["Folder"] = folder;
            ViewData["Items"] = Services.DocumentBinderService.Contents(db, folderId);
            ViewData["PrintedBy"] = GetLoginUsername();
            return View();
        }

        public IActionResult DownloadDocument(string path)
        {
            IActionResult fileDenied = FileAccessDenied(db, path);
            if (fileDenied != null) return fileDenied;

            string webRootPath = Environment.WebRootPath;
            // Hanya file di wwwroot/Upload/ATTACHMENT/ (perbaikan path traversal 2026-11-06)
            string fullPath = Services.UploadPath.Resolve(webRootPath, path);
            if (fullPath == null || !System.IO.File.Exists(fullPath)) return NotFound();
            string fileName = Path.GetFileName(fullPath);

            // Source file only for the creator and Document Control (2026-11-10); others get the
            // uncontrolled PDF from DocumentMaintenance/DownloadAttachment.
            int? ownerDocumentId = Services.DocumentAccessService.FindDocumentByFile(db, path);
            if (path.Contains("/DOCUMENT_TRANSACTION/", StringComparison.OrdinalIgnoreCase)
                && !Services.DocumentAccessService.CanDownloadSource(db, GetLoginUsername(), ownerDocumentId))
            {
                return RedirectToAction("DownloadAttachment", "DocumentMaintenance", new { FILE_PATH = path, DOCUMENT_TRANSACTION_ID = ownerDocumentId });
            }

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

            // Audit trail akses (2026-11-06)
            Services.DocumentAccessLogService.Log(db, HttpContext, Services.DocumentAccessLogService.Download, filePath: path);

            return File(bytes, "application/force-download", fileName);
        }

        public IActionResult DownloadExcel()
        {
            IList<DocumentControlDashboardDocument> listData = DocumentControlDashboardRepo.Search(
                new DocumentControlMaintenance(), db, null, null);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Control");

            string[] headers = {
                "No", "Document No", "Document Name", "Category", "Revision", "Status",
                "Effective Date", "Next Review", "Document Owner", "Acceptance",
                "Division", "Department", "Classification", "Last Updated"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (DocumentControlDashboardDocument item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_CODE);
                row.CreateCell(2).SetCellValue(item.DOCUMENT_NAME);
                row.CreateCell(3).SetCellValue(item.CATEGORY_CODE);
                row.CreateCell(4).SetCellValue(item.REVISION ?? 0);
                row.CreateCell(5).SetCellValue(item.DOC_STATUS_VAL);
                row.CreateCell(6).SetCellValue(item.DOCUMENT_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(7).SetCellValue(item.NEXT_REVIEW_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(8).SetCellValue(item.OWNER_FULL_NAME);
                row.CreateCell(9).SetCellValue((item.ACK_DONE ?? 0) + "/" + (item.ACK_TOTAL ?? 0));
                // Division added 2026-11-18, same as the grid
                row.CreateCell(10).SetCellValue(string.IsNullOrEmpty(item.DIVISION) ? ""
                    : string.IsNullOrEmpty(item.DIVISION_NAME) ? item.DIVISION : item.DIVISION + " - " + item.DIVISION_NAME);
                row.CreateCell(11).SetCellValue(item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME);
                row.CreateCell(12).SetCellValue(item.CLASSIFIED_VAL);
                row.CreateCell(13).SetCellValue((item.CHANGED_DT ?? item.CREATED_DT)?.ToString("dd-MM-yyyy HH:mm") ?? "");

                rowIndex++;
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                workbook.Write(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DOCUMENT-CONTROL-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }

    }
}
