using DMS.Common.Controllers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    public class DocumentArchiveController : BaseController
    {
        DBContext db;

        public DocumentArchiveController(DBContext db)
        {
            this.db = db;
        }

        private P4DMaintenanceRepo P4DMaintenanceRepo = P4DMaintenanceRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentArchive/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Title"] = "Document Archive (ISO)";

            return View();
        }

        // Read-only: dokumen yang distribusinya sudah sepenuhnya selesai - arsip
        // "bantex" staff ISO/QMS, lintas departemen (loginUser null, tidak di-scope
        // per divisi/departemen seperti P4DMaintenanceController).
        //
        // Filter berdasarkan TB_R_DOCUMENT.STATUS='5' (Published/Effective), BUKAN
        // TB_R_CTRL_DOCUMENT.STATUS='2'. Sempat salah pakai yang kedua - STATUS=2 di situ
        // cuma berarti "registrasi P4D disetujui" (di awal, lewat tombol Approve),
        // bukan "sudah diterima fisik oleh semua departemen tujuan". Sinyal yang benar
        // untuk "sudah fix" adalah TB_R_DOCUMENT.STATUS=5, yang di-set otomatis oleh
        // sp_P4DMaintenance_UpdateDistributionPublish begitu SEMUA departemen tujuan
        // sudah Acknowledge di My Documents.
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
                    data.OPERATION_TYPE = 1;

                    var listData = P4DMaintenanceRepo.Search(data, null, db, pageNumber, pageSize, documentStatus: "5");
                    var dataCount = P4DMaintenanceRepo.Search(data, null, db, null, null, documentStatus: "5").Count;
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
    }
}
