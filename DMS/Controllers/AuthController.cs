using DMS.Common.Controllers;
using DMS.Helpers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class AuthController : BaseController
    {
        DBContext db;
        IConfiguration Configuration;
        public AuthController(DBContext db, IConfiguration configuration)
        {
            this.db = db;
            Configuration = configuration;
        }

        private LoginRepo loginRepo = LoginRepo.Instance;
        private NotificationRepo notificationRepo = NotificationRepo.Instance;

        public IActionResult Login(string param)
        {
            if (HttpContext.Session.GetString("USERNAME") != null)
            {
                return Redirect("/Home/Index");
            }

            ViewData["Title"] = "Login";
            ViewData["ParamURL"] = param;
            return View();
        }

        public ActionResult Validate(User data, string param)
        {
            User user = loginRepo.GetUserByUsername(data, db);
            String menuURL = "";
            String functionList = "";

            string module = "Login";
            string function = "Validate";
            string location = "Login";

            LogHeader logH = new LogHeader();
            logH.MODULE = module;
            logH.FUNCTION = function;

            long processid = LogMonitoringRepo.Instance.StartLog(logH, location, "admin", db);

            try
            {
                if (user != null)
                {
                    bool isPasswordValid;
                    string invalidMessage = "Invalid Password!";

                    if (user.AD_USER == "1")
                    {
                        string domain = Configuration["ActiveDirectory:Domain"];
                        (bool Success, string Message) adResult = ActiveDirectoryHelper.ValidateUser(domain, data.USERNAME, data.PASSWORD);
                        isPasswordValid = adResult.Success;
                        if (!adResult.Success) { invalidMessage = adResult.Message; }
                    }
                    else
                    {
                        //Encrypt to MD5 with key
                        data.PASSWORD = EncryptWithKey(data.PASSWORD);
                        isPasswordValid = user.PASSWORD == data.PASSWORD;
                    }

                    if (isPasswordValid)
                    {
                        List<Menu> menus = loginRepo.GetMenuByRole(user, db);
                        foreach (Menu oMenu in menus)
                        {
                            menuURL = menuURL + ", " + oMenu.MENU_URL;
                        }
                        List<Function> functions = loginRepo.GetFunctionByRole(user, db);
                        foreach (Function oFunc in functions)
                        {
                            functionList = functionList + ", " + oFunc.FUNCTION_ID;
                        }

                        CreateSession(user, GenerateMenu(menus), menuURL, functionList);

                        if (param != null) { param = param.Replace("&amp;", "&"); }

                        return Json(new { status = true, message = "Login Successfull!", data = param });
                    }
                    else
                    {
                        LogMonitoringRepo.Instance.WriteLog(processid, "3", "ERR", invalidMessage, location, "admin", db);
                        return Json(new { status = false, message = invalidMessage });
                    }
                }
                else
                {
                    LogMonitoringRepo.Instance.WriteLog(processid, "3", "ERR", "invalid username", location, "admin", db);
                    return Json(new { status = false, message = "Invalid Username!" });
                }
            }
            catch (Exception ex)
            {
                LogMonitoringRepo.Instance.WriteLog(processid, "3", "ERR", ex.Message, location, "admin", db);
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult getListNotification(Notification notification)
        {
            try
            {
                var data = notificationRepo.Search(notification, db);

                return Json(new { status = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult UpdateNotification(string notificationId)
        {
            try
            {
                Notification notification = new Notification();
                notification.NOTIFICATION_ID = Int32.Parse(notificationId);
                notification.STATUS = "1";

                var data = notificationRepo.Update(notification, GetLoginUsername(), db);

                return Json(new { status = true, data = data });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Logout()
        {
            ClearSession();
            return RedirectToAction("Login", "Auth");
        }
    }
}