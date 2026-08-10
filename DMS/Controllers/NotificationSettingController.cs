using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    public class NotificationSettingController : BaseController
    {
        DBContext db;
        public NotificationSettingController(DBContext db)
        {
            this.db = db;
        }

        private NotificationSettingRepo notificationSettingRepo = NotificationSettingRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/NotificationSetting/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Toggle"] = HttpContext.Session.GetString("functionList").Contains("NOTIFICATIONSETTING-TOGGLE");
            ViewData["Title"] = "Notification Settings";

            return View();
        }

        public JsonResult Search()
        {
            try
            {
                IList<NotificationSetting> data = notificationSettingRepo.Search(db);
                return Json(new { status = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult ToggleEmail(string notificationType, bool sendEmail)
        {
            try
            {
                DBResult result = notificationSettingRepo.ToggleEmail(notificationType, sendEmail, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
