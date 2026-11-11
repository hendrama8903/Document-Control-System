using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DMS
{
    /// <summary>
    /// Every action needs a login session (security fix 2026-11-11, Kadai No. 53). Before this,
    /// only the Index pages and some POST actions checked the session, so Search, GetByKey,
    /// DownloadExcel and PrintForm answered without login - and SPs that skip their scope filter
    /// when @USERNAME is NULL returned data of every division.
    ///
    /// Page and file requests are sent to the login page (and back afterwards); AJAX requests get
    /// 401 with a JSON message, which the layout turns into a redirect to the login page.
    /// Open: the login itself and the error page. Registered globally in Startup.
    /// </summary>
    public class RequireLoginFilter : IActionFilter
    {
        static readonly HashSet<string> OpenActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Auth/Login", "Auth/Validate", "Auth/Logout", "Home/Error",
        };

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;
            if (!string.IsNullOrEmpty(http.Session.GetString("USERNAME"))) return;

            string controller = context.RouteData.Values["controller"]?.ToString();
            string action = context.RouteData.Values["action"]?.ToString();
            if (OpenActions.Contains(controller + "/" + action)) return;

            context.Result = Deny(http, 401, "Session expired. Please log in again.");
        }

        public void OnActionExecuted(ActionExecutedContext context) { }

        /// <summary>
        /// A browser navigation (page, file download, form posted to a new tab) wants HTML;
        /// jQuery and fetch calls do not.
        /// </summary>
        internal static bool IsPageRequest(HttpRequest request)
        {
            if (string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
                return false;
            string accept = request.Headers["Accept"].ToString();
            return accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }

        internal static IActionResult Deny(HttpContext http, int statusCode, string message)
        {
            if (statusCode == 401 && IsPageRequest(http.Request))
            {
                string back = HttpMethods.IsGet(http.Request.Method)
                    ? http.Request.Path + http.Request.QueryString
                    : "";
                return new RedirectResult(back.Length > 0
                    ? "/Auth/Login?param=" + Uri.EscapeDataString(back)
                    : "/Auth/Login");
            }
            return new JsonResult(new { status = false, message }) { StatusCode = statusCode };
        }
    }

    /// <summary>
    /// The action is only for users whose role has the given menu (same rule as the Index page
    /// of that menu). For data endpoints that only their own page uses, e.g. the user list,
    /// activity log and role/menu configuration (Kadai No. 53).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireMenuAttribute : ActionFilterAttribute
    {
        readonly string menuUrl;

        public RequireMenuAttribute(string menuUrl)
        {
            this.menuUrl = menuUrl;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;
            if (string.IsNullOrEmpty(http.Session.GetString("USERNAME")))
            {
                context.Result = RequireLoginFilter.Deny(http, 401, "Session expired. Please log in again.");
                return;
            }

            if (HasMenu(http, menuUrl)) return;

            if (RequireLoginFilter.IsPageRequest(http.Request))
            {
                var controller = (Controller)context.Controller;
                http.Response.StatusCode = 403;
                context.Result = new ViewResult
                {
                    ViewName = "Error403",
                    ViewData = new ViewDataDictionary(controller.ViewData),
                    StatusCode = 403,
                };
                return;
            }
            context.Result = new JsonResult(new { status = false, message = "You are not authorized to perform this action." }) { StatusCode = 403 };
        }

        /// <summary>Exact match against the menu URLs stored at login (comma separated).</summary>
        internal static bool HasMenu(HttpContext http, string menuUrl)
        {
            string list = http.Session.GetString("menuURL");
            if (string.IsNullOrEmpty(list)) return false;
            return list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(u => string.Equals(u, menuUrl, StringComparison.OrdinalIgnoreCase));
        }
    }
}
