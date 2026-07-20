using DMS.Models.DB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMS
{
    public class AuthorizedAction : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            if (filterContext.HttpContext.Session.GetString("USERNAME") == null)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary { { "controller", "Auth" }, { "action", "Login" } });
                return;
            }

            var menus = JsonConvert.DeserializeObject<List<Menu>>(filterContext.HttpContext.Session.GetString("menus"));
            var controllerName = filterContext.RouteData.Values["controller"];
            var actionName = filterContext.RouteData.Values["action"];
            string url = "/" + controllerName + "/" + actionName;

            if (!menus.Where(s => s.MENU_URL == url).Any())
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary { { "controller", "Auth" }, { "action", "Login" } });
                return;
            }
        }
    }
}
