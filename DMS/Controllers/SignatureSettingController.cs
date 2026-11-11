using DMS.Common.Controllers;
using DMS.Models;
using DMS.Services;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    /// <summary>
    /// Administration > Signature Settings (request Hendra 2026-11-09): one signing method
    /// for the whole system - TULIS (draw/type), QR, or UPLOAD (profile signature image).
    /// The signature dialog reads the active method from Current().
    /// </summary>
    public class SignatureSettingController : BaseController
    {
        private readonly DBContext db;
        private readonly IWebHostEnvironment environment;

        public SignatureSettingController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            this.environment = environment;
        }

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null) return Redirect("/Auth/Login");
            if (!HasMenuAccess("/SignatureSetting/Index"))
            {
                Response.StatusCode = 403;
                return View("Error403");
            }

            ViewData["Title"] = "Signature Settings";
            ViewData["CanEdit"] = (HttpContext.Session.GetString("functionList") ?? "").Contains("SIGNATURESETTING-EDIT");
            ViewData["Method"] = SignatureService.ActiveMethod(db);
            ViewData["VerifyBaseUrl"] = SignatureService.VerifyBaseUrl(db);
            return View();
        }

        [HttpPost]
        public JsonResult Save(string method, string verifyBaseUrl)
        {
            JsonResult denied = DenyWithoutFunction("SIGNATURESETTING-EDIT");
            if (denied != null) return denied;

            method = (method ?? "").Trim().ToUpperInvariant();
            if (!SignatureService.SettingMethods.Contains(method))
                return Json(new { status = false, message = "Choose a signing method." });

            verifyBaseUrl = (verifyBaseUrl ?? "").Trim().TrimEnd('/');
            if (verifyBaseUrl.Length > 0 &&
                (!Uri.TryCreate(verifyBaseUrl, UriKind.Absolute, out Uri uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
                return Json(new { status = false, message = "Server address must start with http:// or https://, for example http://dcs.hmmi.co.id" });

            try
            {
                SignatureService.SaveSettings(db, method, verifyBaseUrl, GetLoginUsername());
                return Json(new { status = true, message = "Signature settings saved." });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        /// <summary>Active method for the signature dialog, plus the login user's profile signature for UPLOAD.</summary>
        [HttpGet]
        public JsonResult Current()
        {
            JsonResult denied = DenyWithoutLogin();
            if (denied != null) return denied;

            string method = SignatureService.ActiveMethod(db);
            string profileImage = null;
            if (method == SignatureService.MethodUpload)
            {
                string full = SignatureService.ProfileSignaturePath(db, environment.WebRootPath, GetLoginUsername());
                if (full != null)
                {
                    string relative = Path.GetRelativePath(UploadPath.Root, full).Replace('\\', '/');
                    profileImage = "/" + relative + "?v=" + System.IO.File.GetLastWriteTimeUtc(full).Ticks;
                }
            }

            return Json(new { status = true, method, profileImage });
        }
    }
}
