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
    public class DocumentMasterController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        public DocumentMasterController(DBContext db, IWebHostEnvironment environment)
        {
            this.db = db;
            Environment = environment;
        }

        private DocumentMasterRepo documentMasterRepo = DocumentMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;

        public IActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/DocumentMaster/Index"))
                {
                    return StatusCode(403);
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-DELETE");
            ViewData["Delete-FilePath"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-DELETE-FILEPATH");
            ViewData["Download-FilePath"] = HttpContext.Session.GetString("functionList").Contains("DOCUMENTMASTER-DOWNLOAD-FILEPATH");

            ViewData["Title"] = "Document Category";

            return View();
        }

        public JsonResult GetByKey(DocumentMaster data)
        {
            try
            {
                DocumentMaster result = documentMasterRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Search(DocumentMaster data, bool initialMode)
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
                    var listData = documentMasterRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = documentMasterRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = listData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public async Task<JsonResult> AddEditAsync(string screenMode, DocumentMaster data)
        {
            DBResult result = null;
            string folderName = "/Upload/";
            string webRootPath = Environment.WebRootPath;

            try
            {
                if (Request.Form.Files.Count > 0)
                {
                    IFormFile file = Request.Form.Files[0];
                    MSystem mSystem = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "DOCUMENT_MASTER" }, db);

                    string extension = Path.GetExtension(file.FileName);
                    string path = folderName + mSystem.SYSTEM_VALUE.Trim();
                    string pathSave = webRootPath + folderName + mSystem.SYSTEM_VALUE.Trim();
                    string documentname = data.DOCUMENT_NAME;
                    documentname = documentname.Replace(" ", "_").Replace("/", "_");
                    string fileName = documentname + extension;
                    string finalPath = pathSave + fileName;


                    if (!Directory.Exists(pathSave))
                    {
                        Directory.CreateDirectory(pathSave);
                    }

                    if (data.FILE_PATH != null)
                    {
                        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                        //Delete File
                        if (System.IO.File.Exists(pathCheck))
                        {
                            System.IO.File.Delete(pathCheck);
                        }
                    }

                    data.FILE_PATH = path + fileName;
                    //Save File to Local Storage
                    using (var stream = new FileStream(finalPath, FileMode.Create))
                    {
                        file.CopyTo(stream);
                    }
                }

                if (screenMode == "ADD")
                {
                    result = documentMasterRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    result = documentMasterRepo.Update(data, GetLoginUsername(), db);
                }
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(DocumentMaster data, string path)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                result = documentMasterRepo.Delete(data, GetLoginUsername(), db);

                if (result.status)
                {
                    if (data.FILE_PATH != null)
                    {
                        string pathCheck = webRootPath + data.FILE_PATH.Trim();
                        //Delete File
                        if (System.IO.File.Exists(pathCheck))
                        {
                            System.IO.File.Delete(pathCheck);
                        }
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult RemoveAttachment(DocumentMaster data)
        {
            DBResult result = null;
            string webRootPath = Environment.WebRootPath;

            try
            {
                if (data.FILE_PATH != null)
                {
                    string pathCheck = webRootPath + data.FILE_PATH.Trim();
                    //Delete File
                    if (System.IO.File.Exists(pathCheck))
                    {
                        System.IO.File.Delete(pathCheck);
                    }

                    result = documentMasterRepo.RemoveAttachment(data, GetLoginUsername(), db);
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public IActionResult DownloadAttachment(string path)
        {
            string webRootPath = Environment.WebRootPath;
            string fullPath = webRootPath + path;
            string[] split = path.Split("/");
            string fileName = split[4];

            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

            return File(bytes, "application/force-download", fileName);
        }

        public JsonResult GetDocumentCode(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaster oDocumentMaster = new DocumentMaster();
                if (q != null)
                    oDocumentMaster.DOCUMENT_CODE = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentMaster> dataList = documentMasterRepo.Search(oDocumentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_CODE).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_CODE, id = data.DOCUMENT_CODE });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetDocumentName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                DocumentMaster oDocumentMaster = new DocumentMaster();
                if (q != null)
                    oDocumentMaster.DOCUMENT_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<DocumentMaster> dataList = documentMasterRepo.Search(oDocumentMaster, db, pageInt, int.Parse(pageLimit))
                    .GroupBy(x => x.DOCUMENT_NAME).Select(x => x.First()).ToList();

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.DOCUMENT_NAME, id = data.DOCUMENT_NAME });
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
