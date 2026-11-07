using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Text;

namespace DMS.Controllers
{
    public class WorkflowDocController : BaseController
    {
        DBContext db;
        public WorkflowDocController(DBContext db)
        {
            this.db = db;
        }

        private WorkflowDocRepo workflowDocRepo         = WorkflowDocRepo.Instance;
        private PositionMasterRepo positionMasterRepo   = PositionMasterRepo.Instance;

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/WorkflowDoc/Index"))
                {
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"]    = HttpContext.Session.GetString("functionList").Contains("WORKFLOWDOC-ADD");
            ViewData["Edit"]   = HttpContext.Session.GetString("functionList").Contains("WORKFLOWDOC-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("WORKFLOWDOC-DELETE");

            ViewData["Title"] = "Approval Workflow Setup";

            return View();
        }

        public ActionResult Search(WorkflowDocArr data, bool initialMode)
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
                    var dataList = workflowDocRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = workflowDocRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = dataList };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult GetByCode(WorkflowDoc data)
        {
            try
            {
                IList<WorkflowDoc> result = workflowDocRepo.GetByCode(data, db);
                WorkflowDoc first = result.FirstOrDefault();
                // CREATOR_LEVEL = POSITION_LEVEL pembuat (bukan POSITION_ID) - teksnya
                // dibentuk dari semua jabatan di level itu, sama seperti dropdown.
                string creatorText = first?.CREATOR_LEVEL == null ? null : CreatorLevelText(first.CREATOR_LEVEL.Value);
                string categoryText = null;
                if (first?.DOCUMENT_ID != null)
                {
                    DocumentMaster category = DocumentMasterRepo.Instance.Search(new DocumentMaster { DOCUMENT_ID = first.DOCUMENT_ID }, db, null, null)
                        .FirstOrDefault(m => m.DOCUMENT_ID == first.DOCUMENT_ID);
                    categoryText = category == null ? "#" + first.DOCUMENT_ID : category.DOCUMENT_CODE + " - " + category.DOCUMENT_NAME;
                }
                return Json(new { status = true, data = result, creatorText, categoryText });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult AddEdit([FromBody] List<WorkflowDoc> data)
        {
            if (data == null || data.Count == 0)
            {
                return Json(new { status = false, message = "Detail Workflow cannot be empty" });
            }
            if (!HasFunction(data[0].WORKFLOW_DOC_ID == null ? "WORKFLOWDOC-ADD" : "WORKFLOWDOC-EDIT"))
            {
                return NotAuthorized();
            }

            // Hapus alur lama + simpan ulang dalam SATU transaksi: dulu kalau simpan
            // header/detail gagal (mis. kombinasi dobel), alur lama sudah terlanjur
            // terhapus.
            using var transaction = db.Database.BeginTransaction();
            try
            {
                WorkflowDoc temp = new WorkflowDoc();
                temp.WORKFLOW_DOC_ID = data[0].WORKFLOW_DOC_ID;
                temp.DOCUMENT_LEVEL  = data[0].DOCUMENT_LEVEL;
                temp.CREATOR_LEVEL   = data[0].CREATOR_LEVEL;
                temp.DOCUMENT_ID     = data[0].DOCUMENT_ID;

                if (temp.WORKFLOW_DOC_ID != null)
                {
                    // Edit: kunci alur dan statusnya diambil dari data tersimpan, bukan
                    // dari kiriman browser, supaya alur nonaktif tetap nonaktif.
                    WorkflowDoc existing = workflowDocRepo.GetByCode(new WorkflowDoc { WORKFLOW_DOC_ID = temp.WORKFLOW_DOC_ID }, db).FirstOrDefault();
                    if (existing == null)
                    {
                        transaction.Rollback();
                        return Json(new { status = false, message = "Workflow not found. It may have been deleted - refresh the page." });
                    }
                    temp.DOCUMENT_LEVEL = existing.DOCUMENT_LEVEL;
                    temp.CREATOR_LEVEL  = existing.CREATOR_LEVEL;
                    temp.DOCUMENT_ID    = existing.DOCUMENT_ID;
                    temp.IS_ACTIVE      = existing.IS_ACTIVE;

                    DBResult deleted = workflowDocRepo.Delete(temp, GetLoginUsername(), db);
                    if (!deleted.status)
                    {
                        transaction.Rollback();
                        return Json(deleted);
                    }
                }

                DBResult resultHeader = workflowDocRepo.InsertHeader(temp, GetLoginUsername(), db);
                if (!resultHeader.status)
                {
                    transaction.Rollback();
                    return Json(resultHeader);
                }

                DBResult result = resultHeader;
                foreach (var workflow in data.OrderBy(w => w.WORKFLOW_SEQ))
                {
                    workflow.WORKFLOW_DOC_ID = resultHeader.returnId;
                    result = workflowDocRepo.InsertDetail(workflow, GetLoginUsername(), db);
                    if (!result.status)
                    {
                        transaction.Rollback();
                        return Json(result);
                    }
                }

                transaction.Commit();
                return Json(result);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Json(new { status = false, message = ex.Message });
            }
        }

        /// <summary>Dropdown "Creator Level": satu pilihan per POSITION_LEVEL, berisi semua jabatan di level itu.</summary>
        public JsonResult GetCreatorLevels(string q, string pageLimit, string page)
        {
            try
            {
                var list = positionMasterRepo.Search(new PositionMaster(), db, null, null)
                    .Where(p => p.POSITION_LEVEL != null)
                    .GroupBy(p => p.POSITION_LEVEL.Value)
                    .OrderBy(g => g.Key)
                    .Select(g => new Select2 { id = g.Key.ToString(), text = "Level " + g.Key + " - " + string.Join(", ", g.Select(p => p.POSITION_NAME)) })
                    .Where(s => string.IsNullOrWhiteSpace(q) || s.text.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return Json(new { status = true, items = Page(list, pageLimit, page) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Paging untuk select2 (site.js meminta halaman berikutnya selama hasil >= pageLimit)
        static List<Select2> Page(List<Select2> list, string pageLimit, string page)
        {
            int size = int.TryParse(pageLimit, out int l) && l > 0 ? l : 10;
            int number = int.TryParse(page, out int p) && p > 0 ? p : 1;
            return list.Skip((number - 1) * size).Take(size).ToList();
        }

        string CreatorLevelText(int level)
        {
            var names = positionMasterRepo.Search(new PositionMaster(), db, null, null)
                .Where(p => p.POSITION_LEVEL == level)
                .Select(p => p.POSITION_NAME);
            return "Level " + level + " - " + string.Join(", ", names);
        }

        /// <summary>Dropdown "Document Category" (opsional) - kategori di Document Level terpilih.</summary>
        public JsonResult GetDocumentCategories(string q, string param, string pageLimit, string page)
        {
            try
            {
                if (!int.TryParse(param, out int level))
                {
                    return Json(new { status = true, items = new List<Select2>() });
                }
                var list = DocumentMasterRepo.Instance.Search(new DocumentMaster { LEVEL = level }, db, null, null)
                    .Where(m => m.LEVEL == level)
                    .OrderBy(m => m.DOCUMENT_CODE)
                    .Select(m => new Select2 { id = m.DOCUMENT_ID.ToString(), text = m.DOCUMENT_CODE + " - " + m.DOCUMENT_NAME })
                    .Where(s => string.IsNullOrWhiteSpace(q) || s.text.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return Json(new { status = true, items = Page(list, pageLimit, page) });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Delete(WorkflowDoc data)
        {
            if (!HasFunction("WORKFLOWDOC-DELETE"))
            {
                return NotAuthorized();
            }

            DBResult result = null;
            try
            {
                result = workflowDocRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult SetActive(int id, bool isActive)
        {
            if (!HasFunction("WORKFLOWDOC-EDIT"))
            {
                return NotAuthorized();
            }

            try
            {
                return Json(workflowDocRepo.SetActive(id, isActive, GetLoginUsername(), db));
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        /// <summary>Peringatan dampak sebelum alur disimpan (EDIT), diaktifkan (ACTIVATE), dinonaktifkan (DEACTIVATE), atau dihapus (DELETE).</summary>
        [HttpPost]
        public JsonResult GetImpact(int id, string action)
        {
            if (!HasFunction(action == "DELETE" ? "WORKFLOWDOC-DELETE" : "WORKFLOWDOC-EDIT"))
            {
                return NotAuthorized();
            }

            try
            {
                WorkflowImpact impact = workflowDocRepo.Impact(id, db);
                if (impact == null)
                {
                    return Json(new { status = false, message = "Workflow not found. It may have been deleted - refresh the page." });
                }
                List<string> lines = ImpactLines(impact, action, out bool warning);
                return Json(new { status = true, lines, warning });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // warning = perubahan ini membuat dokumen baru ditolak; dialognya tampil kuning.
        static List<string> ImpactLines(WorkflowImpact w, string action, out bool warning)
        {
            warning = false;
            var lines = new List<string>();
            string creator = "creator level " + w.CREATOR_LEVEL + " (" + w.CREATOR_USERS + " active user(s))";

            if (action == "ACTIVATE")
            {
                lines.Add(w.IS_CATEGORY_WORKFLOW
                    ? "New " + w.DOCUMENT_CODE + " documents by " + creator + " will use this workflow."
                    : "New Level " + w.DOCUMENT_LEVEL + " documents by " + creator + " in categories without their own workflow will use this workflow.");
            }
            else if (!w.IS_ACTIVE)
            {
                lines.Add(action == "EDIT"
                    ? "This workflow is inactive. The changes take effect after it is activated."
                    : "This workflow is inactive, so new documents are not affected.");
            }
            else if (action == "EDIT")
            {
                lines.Add("The changes apply to new documents by " + creator + ".");
            }
            else if (!w.IS_CATEGORY_WORKFLOW)
            {
                warning = true;
                lines.Add(char.ToUpper(creator[0]) + creator.Substring(1) + " will have no default workflow for Level " + w.DOCUMENT_LEVEL
                    + ". New documents that rely on it will be rejected."
                    + (string.IsNullOrEmpty(w.DEPENDENT_CATEGORIES) ? "" : " Categories affected: " + w.DEPENDENT_CATEGORIES + "."));
            }
            else if (w.HAS_DEFAULT_WORKFLOW)
            {
                lines.Add("New " + w.DOCUMENT_CODE + " documents by " + creator + " will use the default Level " + w.DOCUMENT_LEVEL + " workflow.");
            }
            else
            {
                warning = true;
                lines.Add("New " + w.DOCUMENT_CODE + " documents by " + creator + " will be rejected, because Level " + w.DOCUMENT_LEVEL + " has no default workflow for them.");
            }

            if (w.IN_PROGRESS_DOCS > 0)
            {
                lines.Add(w.IN_PROGRESS_DOCS + " document(s) in approval keep their current approvers.");
            }
            if (w.REJECTED_DOCS > 0)
            {
                lines.Add(w.REJECTED_DOCS + " rejected document(s) will follow the workflow in force when resubmitted.");
            }
            return lines;
        }

        // Dicek di server: menyembunyikan tombol di tampilan saja tidak mencegah
        // permintaan langsung ke action-nya.
        bool HasFunction(string functionId)
        {
            string functionList = HttpContext.Session.GetString("functionList");
            return GetLoginUsername() != null && functionList != null && functionList.Contains(functionId);
        }

        JsonResult NotAuthorized()
        {
            return Json(new { status = false, message = "You are not authorized to perform this action." });
        }

        public JsonResult GetPositionName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                PositionMaster oWorkflow = new PositionMaster();
                if (q != null)
                    oWorkflow.POSITION_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<PositionMaster> dataList = positionMasterRepo.Search(oWorkflow, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.POSITION_NAME, id = data.POSITION_ID?.ToString() });
                    //list.Add(new Select2() { text = data.POSITION_NAME, id = data.POSITION_ID });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

    }
}
