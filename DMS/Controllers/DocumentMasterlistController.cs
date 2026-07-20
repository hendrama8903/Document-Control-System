using DMS.Common.Controllers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DMS.Controllers
{
    public class DocumentMasterlistController : BaseController
    {
        DBContext db;

        public DocumentMasterlistController(DBContext db)
        {
            this.db = db;
        }

        private DocumentMaintenanceRepo documentMaintenanceRepo = DocumentMaintenanceRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentMasterlist/Index"))
                {
                    return StatusCode(403);
                }
            }

            ViewData["Title"] = "Document Masterlist";

            return View();
        }

        public ActionResult Search(bool initialMode)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                int recordsTotal = 0;

                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = "" };
                    return Ok(jsonData);
                }
                else
                {
                    var listData = documentMaintenanceRepo.GetMasterlist(db);
                    recordsTotal = listData.Count;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        //Export masterlist ke Excel - dibangun dari nol (bukan template) supaya mudah
        //dirawat; kolom mengikuti grid di halaman (pola sama seperti
        //P4DMaintenanceController.Download, tapi tulis ke MemoryStream, bukan file fisik
        //sementara - pola sama seperti DocumentMaintenanceController.DownloadReport).
        public IActionResult DownloadExcel()
        {
            IList<DocumentMasterlist> listData = documentMaintenanceRepo.GetMasterlist(db);

            var memoryStream = new MemoryStream();
            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Document Masterlist");

            string[] headers = {
                "No", "Document Code", "Document Name", "Category", "Division", "Department",
                "Classified", "Revision", "Status", "Effective Date", "Last Change", "Owner"
            };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (DocumentMasterlist item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.DOCUMENT_CODE);
                row.CreateCell(2).SetCellValue(item.DOCUMENT_TRANSACTION_NAME);
                row.CreateCell(3).SetCellValue(item.DOCUMENT_CATEGORY);
                row.CreateCell(4).SetCellValue(item.DIVISION + " - " + item.DIVISION_NAME);
                row.CreateCell(5).SetCellValue(item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME);
                row.CreateCell(6).SetCellValue(item.CLASSIFIED_VAL);
                row.CreateCell(7).SetCellValue(item.REVISION ?? 0);
                row.CreateCell(8).SetCellValue(item.STATUS_DISPLAY);
                row.CreateCell(9).SetCellValue(item.DOCUMENT_DATE?.ToString("dd-MM-yyyy") ?? "");
                row.CreateCell(10).SetCellValue(string.Join(" - ", new[] { item.ITEM_CHANGED, item.REASON }.Where(x => !string.IsNullOrEmpty(x))));
                row.CreateCell(11).SetCellValue(item.DOCUMENT_CREATOR);

                rowIndex++;
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            workbook.Write(memoryStream);
            memoryStream.Position = 0;

            return File(memoryStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "DOCUMENT-MASTERLIST-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
        }
    }
}
