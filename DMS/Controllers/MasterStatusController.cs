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

        // ---------- Delete never-used Division / Department / Section (request Hendra 2026-11-16) ----------
        // Same rule as Document Category: only data that was never used, still a soft delete
        // (DELETE_FLAG = 2). Same permission as Deactivate. See SQL/MasterDataRemove_Migration.sql.
        static readonly HashSet<string> RemovableEntities = new HashSet<string> { "DIVISION", "DEPARTMENT", "SECTION" };

        public JsonResult Usage(string entity)
        {
            if (entity == null || !RemovableEntities.Contains(entity))
            {
                return Json(new { status = false, message = "Unknown master data type." });
            }
            JsonResult denied = DenyWithoutLogin();
            if (denied != null)
            {
                return denied;
            }

            try
            {
                Dictionary<int, string> usage = MasterStatusRepo.Instance.Usage(entity, db);
                return Json(new { status = true, data = usage.Select(u => new { id = u.Key, usedBy = u.Value }) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Remove(string entity, List<int> ids)
        {
            if (entity == null || !RemovableEntities.Contains(entity) || !EntityFunctions.TryGetValue(entity, out string function))
            {
                return Json(new { status = false, message = "Unknown master data type." });
            }

            JsonResult denied = DenyWithoutFunction(function);
            if (denied != null)
            {
                return denied;
            }

            int removed = 0;
            try
            {
                foreach (int id in ids ?? new List<int>())
                {
                    DBResult result = MasterStatusRepo.Instance.Remove(entity, id, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        string message = result.message?.StartsWith("ERROR: ") == true ? result.message.Substring(7) : result.message;
                        if (removed > 0)
                        {
                            message = removed + " deleted before this one. " + message;
                        }
                        return Json(new { status = false, message });
                    }
                    removed++;
                }
                return Json(new { status = true, message = removed == 1 ? "Successfully deleted" : removed + " items deleted" });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }
    }
}
