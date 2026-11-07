using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;

namespace DMS.Controllers
{
    // Activate data master yang dinonaktifkan. Memakai izin Delete modulnya, yang
    // sekarang berarti Deactivate, supaya hak mengaktifkan dan menonaktifkan sama.
    public class MasterStatusController : BaseController
    {
        static readonly Dictionary<string, string> EntityFunctions = new Dictionary<string, string>
        {
            { "DEPARTMENT", "DEPARTMENTMASTER-DELETE" },
            { "DIVISION", "DIVISIONMASTER-DELETE" },
            { "SECTION", "SECTIONMASTER-DELETE" },
            { "POSITION", "POSITIONMASTER-DELETE" },
            { "DOCUMENT", "DOCUMENTMASTER-DELETE" }
        };

        DBContext db;
        public MasterStatusController(DBContext db)
        {
            this.db = db;
        }

        [HttpPost]
        public JsonResult Activate(string entity, List<int> ids)
        {
            if (entity == null || !EntityFunctions.TryGetValue(entity, out string function))
            {
                return Json(new { status = false, message = "Unknown master data type." });
            }

            JsonResult denied = DenyWithoutFunction(function);
            if (denied != null)
            {
                return denied;
            }

            try
            {
                foreach (int id in ids)
                {
                    DBResult result = MasterStatusRepo.Instance.Activate(entity, id, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        return Json(result);
                    }
                }
                return Json(new { status = true, message = "Successfully activated" });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
