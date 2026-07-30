using DMS.Common.Controllers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace DMS.Controllers
{
    public class DocumentReceiptReportController : BaseController
    {
        DBContext db;

        public DocumentReceiptReportController(DBContext db)
        {
            this.db = db;
        }

        private DocumentReceiptReportRepo documentReceiptReportRepo = DocumentReceiptReportRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentReceiptReport/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Title"] = "Document Receipt Report";

            return View();
        }

        public ActionResult Search(DocumentReceiptReport data, bool initialMode)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int pageNumber = pageSize > 0 ? (skip / pageSize) + 1 : 1;
                int recordsTotal = 0;
                if (initialMode == true)
                {
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = new List<object>() };
                    return Ok(jsonData);
                }
                else
                {
                    var listData = documentReceiptReportRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = documentReceiptReportRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    draw = 0,
                    recordsFiltered = 0,
                    recordsTotal = 0,
                    data = new List<object>(),
                    error = ex.Message
                });
            }
        }

        //Cetak "Tanda Terima Dokumen" (form QMS Hino QCD/FR-QMS-00/006) untuk baris-baris yang
        //dipilih di grid. Dibangun dari nol dengan PdfSharp (bukan overlay template) karena
        //ini tabel data dinamis, bukan watermark di atas dokumen yang sudah ada.
        public IActionResult PrintReceipt(List<int> publishHistoryIds)
        {
            if (publishHistoryIds == null || publishHistoryIds.Count == 0)
            {
                return BadRequest("No document selected");
            }

            IList<DocumentReceiptReport> allData = documentReceiptReportRepo.Search(new DocumentReceiptReport(), db, null, null);
            List<DocumentReceiptReport> items = allData
                .Where(x => x.PUBLISH_HISTORY_ID.HasValue && publishHistoryIds.Contains(x.PUBLISH_HISTORY_ID.Value))
                .OrderBy(x => x.PUBLISH_HISTORY_ID)
                .ToList();

            if (items.Count == 0)
            {
                return BadRequest("Selected document(s) not found");
            }

            if (PdfSharp.Fonts.GlobalFontSettings.FontResolver is not DocumentMaintenanceController.CustomFontResolver)
            {
                PdfSharp.Fonts.GlobalFontSettings.FontResolver = new DocumentMaintenanceController.CustomFontResolver();
            }

            byte[] pdfBytes = BuildReceiptPdf(items);

            return File(pdfBytes, "application/pdf",
                "TANDA-TERIMA-DOKUMEN-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".pdf");
        }

        private static byte[] BuildReceiptPdf(List<DocumentReceiptReport> items)
        {
            //Lebar kolom dasar (poin) - proporsinya mengikuti form contoh, diskalakan
            //supaya pas dengan lebar halaman A4 landscape.
            double[] colWidth = { 22, 95, 160, 40, 55, 55, 60, 38, 26, 32, 38, 100, 50 };

            const double margin = 28;
            const double dataRowHeight = 20;
            const double titleBoxHeight = 22;
            const double pageInfoHeight = 16;
            const double topBoxHeight = 34;
            const double headerRow1Height = 16;
            const double headerRow2Height = 24;
            const double footerHeight = 16;

            XFont fontCompany = new XFont("Arial", 9, XFontStyleEx.Bold);
            XFont fontCompanySub = new XFont("Arial", 8);
            XFont fontFormCode = new XFont("Arial", 8);
            XFont fontTitle = new XFont("Arial", 13, XFontStyleEx.Bold);
            XFont fontPageInfo = new XFont("Arial", 8);
            XFont fontHeader = new XFont("Arial", 7, XFontStyleEx.Bold);
            XFont fontData = new XFont("Arial", 7.5);
            XFont fontFooter = new XFont("Arial", 7.5);

            XPen borderPen = new XPen(XColors.Black, 0.75);
            XStringFormat center = new XStringFormat { Alignment = XStringAlignment.Center, LineAlignment = XLineAlignment.Center };
            XStringFormat left = new XStringFormat { Alignment = XStringAlignment.Near, LineAlignment = XLineAlignment.Center };

            PdfDocument document = new PdfDocument();

            //Dimensi A4 landscape dalam poin (1 pt = 1/72 inci) - dihardcode langsung
            //(tidak perlu bikin halaman sungguhan cuma untuk baca ukurannya).
            const double pageWidth = 841.89;
            const double pageHeight = 595.28;

            //Header block + table header memakan tempat tetap di tiap halaman -
            //hitung dulu berapa baris data yang muat per halaman, baru tentukan total halaman.
            double contentWidth = pageWidth - (2 * margin);
            double scale = contentWidth / colWidth.Sum();
            double[] scaledWidth = colWidth.Select(w => w * scale).ToArray();

            double fixedBlockHeight = topBoxHeight + titleBoxHeight + pageInfoHeight + headerRow1Height + headerRow2Height + footerHeight;
            int rowsPerPage = (int)Math.Max(1, Math.Floor((pageHeight - (2 * margin) - fixedBlockHeight) / dataRowHeight));
            int totalPages = (int)Math.Max(1, Math.Ceiling(items.Count / (double)rowsPerPage));

            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                PdfPage page = document.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                XGraphics gfx = XGraphics.FromPdfPage(page);

                double y = margin;
                double x = margin;

                //--- Kotak kop: logo + nama perusahaan (kiri), kode form (kanan) ---
                XRect topBox = new XRect(x, y, contentWidth, topBoxHeight);
                gfx.DrawRectangle(borderPen, topBox);

                double logoSize = topBoxHeight - 8;
                XRect logoRect = new XRect(x + 6, y + 4, logoSize, logoSize);
                gfx.DrawEllipse(borderPen, logoRect);
                gfx.DrawString("HINO", new XFont("Arial", 6.5, XFontStyleEx.Bold), XBrushes.Black, logoRect, center);

                XRect companyRect = new XRect(x + logoSize + 14, y, 320, topBoxHeight);
                gfx.DrawString("PT. Hino Motors Manufacturing Indonesia", fontCompany, XBrushes.Black,
                    new XRect(companyRect.X, y + 6, companyRect.Width, 12), left);
                gfx.DrawString("QMS", fontCompanySub, XBrushes.Black,
                    new XRect(companyRect.X, y + 18, companyRect.Width, 12), left);

                XRect formCodeRect = new XRect(x + contentWidth - 140, y, 134, topBoxHeight);
                gfx.DrawString("QCD/FR-QMS-00/006", fontFormCode, XBrushes.Black, formCodeRect,
                    new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Center });

                y += topBoxHeight;

                //--- Judul ---
                XRect titleBox = new XRect(x, y, contentWidth, titleBoxHeight);
                gfx.DrawRectangle(borderPen, titleBox);
                gfx.DrawString("TANDA TERIMA DOKUMEN", fontTitle, XBrushes.Black, titleBox, center);
                y += titleBoxHeight;

                //--- Info halaman ---
                XRect pageInfoBox = new XRect(x, y, contentWidth, pageInfoHeight);
                gfx.DrawRectangle(borderPen, pageInfoBox);
                gfx.DrawString("  Halaman : " + (pageIndex + 1) + " of " + totalPages, fontPageInfo, XBrushes.Black,
                    pageInfoBox, left);
                y += pageInfoHeight;

                //--- Header tabel: kolom 0-3 & 11-12 rowspan 2 baris, kolom 4-7 ("Diterima
                //Oleh") & 8-10 ("Penarikan Dokumen Lama") colspan dengan sub-header baris ke-2 ---
                double[] colX = new double[scaledWidth.Length];
                {
                    double runningX = x;
                    for (int c = 0; c < scaledWidth.Length; c++)
                    {
                        colX[c] = runningX;
                        runningX += scaledWidth[c];
                    }
                }

                double headerRow1Y = y;
                double fullHeaderHeight = headerRow1Height + headerRow2Height;

                (int col, int span, string text, bool fullHeight)[] row1Cells =
                {
                    (0, 1, "No.", true),
                    (1, 1, "No. Dokumen", true),
                    (2, 1, "Nama Dokumen", true),
                    (3, 1, "No.\nRevisi", true),
                    (4, 4, "Diterima Oleh", false),
                    (8, 3, "Penarikan Dokumen Lama", false),
                    (11, 1, "Keterangan", true),
                    (12, 1, "Paraf\nQMS", true),
                };

                foreach (var cell in row1Cells)
                {
                    double spanWidth = 0;
                    for (int c = cell.col; c < cell.col + cell.span; c++)
                    {
                        spanWidth += scaledWidth[c];
                    }

                    double h = cell.fullHeight ? fullHeaderHeight : headerRow1Height;
                    XRect rect = new XRect(colX[cell.col], headerRow1Y, spanWidth, h);
                    gfx.DrawRectangle(borderPen, rect);
                    DrawMultilineText(gfx, cell.text, fontHeader, rect, center);
                }

                y += headerRow1Height;

                (int col, string text)[] row2Cells =
                {
                    (4, "Tanggal"), (5, "Div. /\nDept.OF"), (6, "Nama"), (7, "Paraf"),
                    (8, "Ya"), (9, "Tidak"), (10, "Jumlah")
                };

                foreach (var cell in row2Cells)
                {
                    XRect rect = new XRect(colX[cell.col], y, scaledWidth[cell.col], headerRow2Height);
                    gfx.DrawRectangle(borderPen, rect);
                    DrawMultilineText(gfx, cell.text, fontHeader, rect, center);
                }

                y += headerRow2Height;

                //--- Baris data ---
                int startIdx = pageIndex * rowsPerPage;
                int endIdx = Math.Min(startIdx + rowsPerPage, items.Count);

                for (int i = startIdx; i < endIdx; i++)
                {
                    DocumentReceiptReport item = items[i];

                    string[] values = {
                        (i + 1).ToString(),
                        item.DOCUMENT_CODE ?? "",
                        item.DOCUMENT_NAME ?? "",
                        (item.REVISION ?? 0).ToString(),
                        item.RECEIVED_DATE?.ToString("dd-MMM-yy") ?? "",
                        item.DEPARTMENT_CODE ?? "",
                        item.RECEIVED_BY_NAME ?? "",
                        "",
                        (item.REVISION ?? 0) > 0 ? "V" : "",
                        (item.REVISION ?? 0) > 0 ? "" : "V",
                        "",
                        "",
                        ""
                    };

                    for (int c = 0; c < scaledWidth.Length; c++)
                    {
                        XRect cellRect = new XRect(colX[c], y, scaledWidth[c], dataRowHeight);
                        gfx.DrawRectangle(borderPen, cellRect);

                        //Document Code & Document Name rata kiri dengan sedikit padding, sisanya di tengah
                        bool alignLeft = c == 1 || c == 2;
                        XRect textRect = alignLeft
                            ? new XRect(cellRect.X + 3, cellRect.Y, cellRect.Width - 6, cellRect.Height)
                            : cellRect;

                        gfx.DrawString(values[c], fontData, XBrushes.Black, textRect, alignLeft ? left : center);
                    }

                    y += dataRowHeight;
                }

                //--- Footer: revisi form ---
                gfx.DrawString("REVISI: 00", fontFooter, XBrushes.Black,
                    new XRect(x, pageHeight - margin - footerHeight, contentWidth, footerHeight),
                    new XStringFormat { Alignment = XStringAlignment.Far, LineAlignment = XLineAlignment.Near });
            }

            using MemoryStream stream = new MemoryStream();
            document.Save(stream, false);
            return stream.ToArray();
        }

        //XGraphics.DrawString tidak mengartikan "\n" sebagai baris baru (malah tercetak
        //sebagai kotak glyph kosong) - pecah manual per baris lalu susun ke tengah blok.
        private static void DrawMultilineText(XGraphics gfx, string text, XFont font, XRect rect, XStringFormat format)
        {
            if (!text.Contains('\n'))
            {
                gfx.DrawString(text, font, XBrushes.Black, rect, format);
                return;
            }

            string[] lines = text.Split('\n');
            double lineHeight = font.GetHeight();
            double totalHeight = lineHeight * lines.Length;
            double startY = rect.Y + Math.Max(0, (rect.Height - totalHeight) / 2);

            for (int i = 0; i < lines.Length; i++)
            {
                XRect lineRect = new XRect(rect.X, startY + (i * lineHeight), rect.Width, lineHeight);
                gfx.DrawString(lines[i], font, XBrushes.Black, lineRect,
                    new XStringFormat { Alignment = format.Alignment, LineAlignment = XLineAlignment.Center });
            }
        }
    }
}
