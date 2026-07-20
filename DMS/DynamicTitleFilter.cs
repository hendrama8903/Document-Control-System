using DMS.Models;
using DMS.Models.DB;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DMS
{
    //Ambil judul halaman & breadcrumb (ViewData["Title"]) langsung dari TB_M_MENU
    //berdasarkan URL "/{controller}/{action}" halaman yang sedang diakses, supaya rename
    //menu lewat halaman Menu & Function otomatis nyambung ke semua halaman tanpa perlu
    //ubah kode. Kalau URL tidak cocok menu manapun (mis. sub-halaman seperti Document
    //Preview atau User Profile), ViewData["Title"] yang sudah di-set controller dibiarkan
    //apa adanya - filter ini best-effort, tidak boleh sampai menggagalkan render halaman.
    public class DynamicTitleFilter : IActionFilter
    {
        private readonly DBContext db;

        public DynamicTitleFilter(DBContext db)
        {
            this.db = db;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Result is not ViewResult viewResult)
            {
                return;
            }

            try
            {
                string controllerName = context.RouteData.Values["controller"]?.ToString();
                string actionName = context.RouteData.Values["action"]?.ToString();
                string url = "/" + controllerName + "/" + actionName;

                Menu menu = MenuRepo.Instance.GetAll(db).FirstOrDefault(m => m.MENU_URL == url);
                if (menu != null)
                {
                    viewResult.ViewData["Title"] = menu.MENU_NAME;
                }
            }
            catch
            {
                //Best-effort - biarkan ViewData["Title"] statis dari controller sebagai fallback.
            }
        }
    }
}
