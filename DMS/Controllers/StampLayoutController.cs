using DMS.Common.Controllers;
using DMS.Models;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Text.Json;

namespace DMS.Controllers
{
    /// <summary>
    /// Master Data > Stamp Layout (request Hendra 2026-11-11, stage 1): where the MASTER,
    /// CONTROLLED COPY, UNCONTROLLED COPY and OBSOLETE stamps go on Portrait and Landscape
    /// pages, globally and per Document Category. Changing needs STAMPLAYOUT-EDIT.
    /// </summary>
    [RequireMenu("/StampLayout/Index")]
    public class StampLayoutController : BaseController
    {
        private readonly DBContext db;
        private readonly IWebHostEnvironment environment;

        // Same files as DocumentMaintenanceController.Stamp*Image.
        private static readonly Dictionary<string, string> StampImages = new Dictionary<string, string>
        {
            [StampLayoutService.Master] = "/images/02_MASTER.png",
            [StampLayoutService.Controlled] = "/images/03_CONTROLLED_COPY.png",
            [StampLayoutService.Uncontrolled] = "/images/04_UNCONTROLLED_COPY.png",
            [StampLayoutService.Obsolete] = "/images/01_OBSOLETE.png",
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public StampLayoutController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            this.environment = environment;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "Stamp Layout";
            ViewData["CanEdit"] = (HttpContext.Session.GetString("functionList") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(StampLayoutService.FunctionEdit);
            ViewData["Categories"] = StampLayoutService.Categories(db);
            ViewData["StampImages"] = StampImages;
            return View();
        }

        /// <summary>
        /// Positions of one scope (documentId null = global). For a category without its own
        /// set, positions are the global ones and isCustom is false.
        /// </summary>
        [HttpGet]
        public JsonResult Get(int? documentId)
        {
            try
            {
                List<StampLayoutService.Position> global = StampLayoutService.Global(db);
                if (documentId == null)
                    return Json(new { status = true, isCustom = true, positions = global, global });

                if (!StampLayoutService.CategoryExists(db, documentId.Value))
                    return Json(new { status = false, message = "Document category not found." });

                List<StampLayoutService.Position> own = StampLayoutService.Load(db, documentId);
                if (own.Count == 0)
                    return Json(new { status = true, isCustom = false, positions = global, global });

                // Complete a partial set with the global positions.
                var merged = global.ToDictionary(p => (p.STAMP_TYPE, p.ORIENTATION));
                foreach (var p in own) merged[(p.STAMP_TYPE, p.ORIENTATION)] = p;
                return Json(new { status = true, isCustom = true, positions = merged.Values, global });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Save(int? documentId, string positions)
        {
            JsonResult denied = DenyWithoutFunction(StampLayoutService.FunctionEdit);
            if (denied != null) return denied;

            try
            {
                List<StampLayoutService.Position> list = Parse(positions, out string error);
                if (error != null) return Json(new { status = false, message = error });
                if (documentId != null && !StampLayoutService.CategoryExists(db, documentId.Value))
                    return Json(new { status = false, message = "Document category not found." });

                StampLayoutService.Save(db, documentId, list, GetLoginUsername());
                return Json(new
                {
                    status = true,
                    message = documentId == null ? "Global stamp layout saved." : "Stamp layout for this category saved."
                });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        /// <summary>Drops a category's own layout; it follows the global layout again.</summary>
        [HttpPost]
        public JsonResult UseGlobal(int documentId)
        {
            JsonResult denied = DenyWithoutFunction(StampLayoutService.FunctionEdit);
            if (denied != null) return denied;

            try
            {
                StampLayoutService.UseGlobal(db, documentId);
                return Json(new { status = true, message = "This category now uses the global stamp layout." });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Sample PDF of the (unsaved) positions: one blank A4 page per stamp and orientation,
        /// drawn by the same code as the documents. Posted from a form into a new tab.
        /// </summary>
        [HttpPost]
        public IActionResult Preview(string positions)
        {
            List<StampLayoutService.Position> list = Parse(positions, out string error);
            if (error != null) return Content(error);

            if (PdfSharp.Fonts.GlobalFontSettings.FontResolver is not DocumentMaintenanceController.CustomFontResolver)
                PdfSharp.Fonts.GlobalFontSettings.FontResolver = new DocumentMaintenanceController.CustomFontResolver();

            var layout = new StampLayoutService.Layout(list);
            var labels = new Dictionary<string, string>
            {
                [StampLayoutService.Master] = "MASTER",
                [StampLayoutService.Controlled] = "CONTROLLED COPY",
                [StampLayoutService.Uncontrolled] = "UNCONTROLLED COPY",
                [StampLayoutService.Obsolete] = "OBSOLETE",
            };
            var notes = new Dictionary<string, string>
            {
                [StampLayoutService.Controlled] = "Copy No. 1 · To: Sample Department · Issued by " + GetLoginUsername() + " on " + DateTime.Now.ToString("dd MMM yyyy HH:mm"),
                [StampLayoutService.Uncontrolled] = "This document is not controlled by the QMS - Document Control",
            };

            using var document = new PdfDocument();
            document.Version = 17;
            var gridPen = new XPen(XColor.FromArgb(40, 120, 130, 150), 0.4);
            var safePen = new XPen(XColor.FromArgb(160, 36, 80, 216), 0.6) { DashStyle = XDashStyle.Dash };
            var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
            var textFont = new XFont("Arial", 9, XFontStyleEx.Regular);
            var gray = new XSolidBrush(XColor.FromArgb(255, 120, 130, 150));
            var red = new XSolidBrush(XColor.FromArgb(220, 198, 40, 40));

            foreach (string orientation in StampLayoutService.Orientations)
            {
                foreach (string type in StampLayoutService.StampTypes)
                {
                    PdfPage page = document.AddPage();
                    page.Size = PdfSharp.PageSize.A4;
                    page.Orientation = orientation == StampLayoutService.Landscape
                        ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
                    double w = page.Width.Point, h = page.Height.Point;

                    using XGraphics gfx = XGraphics.FromPdfPage(page);
                    for (double x = 0; x < w; x += 28.35) gfx.DrawLine(gridPen, x, 0, x, h);   // 1 cm grid
                    for (double y = 0; y < h; y += 28.35) gfx.DrawLine(gridPen, 0, y, w, y);
                    double m = StampLayoutService.SafeMarginPt;
                    gfx.DrawRectangle(safePen, m, m, w - 2 * m, h - 2 * m);

                    gfx.DrawString((orientation == StampLayoutService.Portrait ? "Portrait" : "Landscape") + " · " + labels[type],
                        titleFont, gray, new XRect(0, h / 2 - 24, w, 20), XStringFormats.Center);
                    gfx.DrawString("Sample page. Grid = 1 cm, dashed line = 5 mm print margin.",
                        textFont, gray, new XRect(0, h / 2, w, 14), XStringFormats.Center);

                    string imagePath = environment.WebRootPath + StampImages[type];
                    if (!System.IO.File.Exists(imagePath)) continue;
                    using XImage image = XImage.FromFile(imagePath);
                    notes.TryGetValue(type, out string note);
                    XRect rect = layout.Place(type, w, h, (double)image.PixelHeight / image.PixelWidth, note == null ? 0 : 1);
                    gfx.DrawImage(image, rect);
                    if (note != null)
                        StampLayoutService.DrawNote(gfx, w, rect, new[] { note },
                            new XFont("Arial", type == StampLayoutService.Controlled ? 7 : 6.5,
                                type == StampLayoutService.Controlled ? XFontStyleEx.Bold : XFontStyleEx.Regular), red);
                }
            }

            using var stream = new MemoryStream();
            document.Save(stream, false);
            var disposition = new System.Net.Mime.ContentDisposition { Inline = true, FileName = "Stamp Layout Preview.pdf" };
            Response.Headers["Content-Disposition"] = disposition.ToString();
            return File(stream.ToArray(), "application/pdf");
        }

        private static List<StampLayoutService.Position> Parse(string json, out string error)
        {
            error = null;
            List<StampLayoutService.Position> list;
            try
            {
                list = JsonSerializer.Deserialize<List<StampLayoutService.Position>>(json ?? "", JsonOptions);
            }
            catch (JsonException)
            {
                error = "Stamp positions are not valid.";
                return null;
            }
            error = StampLayoutService.Validate(list);
            return error == null ? list : null;
        }
    }
}
