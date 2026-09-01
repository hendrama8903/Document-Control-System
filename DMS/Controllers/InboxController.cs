using DMS.Common.Controllers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    public class InboxController : BaseController
    {
        DBContext db;
        public InboxController(DBContext db)
        {
            this.db = db;
        }

        public NotificationRepo notificationRepo = NotificationRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/Inbox/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            ViewData["Title"] = "Inbox";

            return View();
        }

        public JsonResult Search()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                Response.StatusCode = 401;
                return Json(new { status = false, message = "Session expired. Please login again." });
            }

            try
            {
                Notification filter = new Notification { USERNAME = GetLoginUsername() };
                var data = notificationRepo.Search(filter, db);

                return Json(new { status = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult MarkRead(int notificationId, string status)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                Response.StatusCode = 401;
                return Json(new { status = false, message = "Session expired. Please login again." });
            }

            try
            {
                Notification notification = new Notification
                {
                    NOTIFICATION_ID = notificationId,
                    STATUS = status
                };

                var result = notificationRepo.Update(notification, GetLoginUsername(), db);

                return Json(new { status = result.status, message = result.message });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult MarkAllRead()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                Response.StatusCode = 401;
                return Json(new { status = false, message = "Session expired. Please login again." });
            }

            try
            {
                var result = notificationRepo.MarkAllRead(GetLoginUsername(), GetLoginUsername(), db);

                return Json(new { status = result.status, message = result.message });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult ClearAll()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                Response.StatusCode = 401;
                return Json(new { status = false, message = "Session expired. Please login again." });
            }

            try
            {
                var result = notificationRepo.ClearAll(GetLoginUsername(), GetLoginUsername(), db);

                return Json(new { status = result.status, message = result.message });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
