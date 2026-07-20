using System.Data;
using System.Text;
using System.Globalization;
using DMS.Common.Controllers;
using DMS.Common.Models;
using DMS.Hubs;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.DB.Commons;
using DMS.Models.Repo;
using Hangfire;
using MDC.Models;
using MDC.Models.Repo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace DMS.Controllers
{
    public class UserController : BaseController
    {
        DBContext db;
        private IWebHostEnvironment Environment;
        private readonly IBackgroundJobClient backgroundJobClient;
        private EmailService EmailService;
        private IHubContext<NotificationsHub> _hubContext;

        public UserController(DBContext db, IWebHostEnvironment environment, IOptions<EmailConfiguration> options,
            IBackgroundJobClient backgroundJobClient, IHubContext<NotificationsHub> hubContext)
        {
            this.db = db;
            Environment = environment;
            EmailService = new EmailService(options, environment);
            this.backgroundJobClient = backgroundJobClient;
            _hubContext = hubContext;
        }

        private UserRepo userRepo = UserRepo.Instance;
        private RoleRepo roleRepo = RoleRepo.Instance;
        private LoginRepo loginRepo = LoginRepo.Instance;
        private PositionMasterRepo positionRepo = PositionMasterRepo.Instance;
        private MSystemRepo mSystemRepo = MSystemRepo.Instance;

        public ActionResult Index()
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return Redirect("/Auth/Login");
            }
            else
            {
                string menuURLList = HttpContext.Session.GetString("menuURL");

                if (!menuURLList.Contains("/User/Index"))
                {
                    return StatusCode(403);
                }
            }

            ViewData["Title"] = "User Management";

            return View();
        }

        public ActionResult Search(User data, bool initialMode)
        {
            try
            {
                if (data.USERNAME != null)
                {
                    data.USERNAME = '*' + data.USERNAME + '*';
                }
                if (data.FULL_NAME != null)
                {
                    data.FULL_NAME = '*' + data.FULL_NAME + '*';
                }

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
                    var userData = userRepo.Search(data, db, pageNumber, pageSize);
                    var dataCount = userRepo.Search(data, db, null, null).Count;
                    recordsTotal = dataCount;
                    var jsonData = new { draw = draw, recordsFiltered = recordsTotal, recordsTotal = recordsTotal, data = userData };
                    return Ok(jsonData);
                }
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public JsonResult GetByKey(User data)
        {
            try
            {
                User result = userRepo.GetByKey(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public async Task<JsonResult> AddEditAsync(string screenMode, User data)
        {
            DBResult result = null;
            string folderName = "/Upload/";
            string webRootPath = Environment.WebRootPath;

            try
            {
                if (Request.Form.Files.Count > 0)
                {
                    IFormFile file = Request.Form.Files[0];
                    MSystem mSystem = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "USER" }, db);

                    string extension = Path.GetExtension(file.FileName);
                    string path = folderName + mSystem.SYSTEM_VALUE.Trim();
                    string pathSave = webRootPath + folderName + mSystem.SYSTEM_VALUE.Trim();
                    //string fileName = data.VIN_NO + '-' + DateTime.Now.ToFileTime() + extension;
                    string fileName = "User" + '-' + DateTime.Now.ToFileTime() + extension;
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
                    //save jpg to local storage
                    using (var stream = new FileStream(finalPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                }

                if (screenMode == "ADD")
                {
                    if (data.AD_USER != "1")
                    {
                        if (string.IsNullOrEmpty(data.PASSWORD) || string.IsNullOrEmpty(data.CONFIRM_PASSWORD))
                        {
                            return Json(new { status = false, message = "Password and Confirm Password must both be filled." });
                        }

                        //Encrypt to MD5 with key
                        data.PASSWORD = EncryptWithKey(data.PASSWORD);
                        data.CONFIRM_PASSWORD = EncryptWithKey(data.CONFIRM_PASSWORD);
                    }

                    result = userRepo.Insert(data, GetLoginUsername(), db);
                }
                else
                {
                    if (data.AD_USER == "1" || (data.PASSWORD == null && data.OLD_PASSWORD == null && data.CONFIRM_PASSWORD == null))
                    {
                        result = userRepo.Update(data, GetLoginUsername(), db);
                    }
                    else if (data.PASSWORD == null || data.CONFIRM_PASSWORD == null)
                    {
                        return Json(new { status = false, message = "New Password and Confirm Password must both be filled to change the password." });
                    }
                    else
                    {
                        //Encrypt to MD5 with key
                        //Old Password bisa null kalau user ini belum pernah punya password lokal
                        //(mis. baru dipindah dari AD_USER) - sp_User_UpdatePassword yang menentukan
                        //apakah Old Password wajib diverifikasi atau tidak.
                        if (data.OLD_PASSWORD != null)
                        {
                            data.OLD_PASSWORD = EncryptWithKey(data.OLD_PASSWORD);
                        }
                        data.PASSWORD = EncryptWithKey(data.PASSWORD);
                        data.CONFIRM_PASSWORD = EncryptWithKey(data.CONFIRM_PASSWORD);

                        result = userRepo.UpdatePassword(data, GetLoginUsername(), db);
                    }
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult Delete(User data)
        {
            try
            {
                if (data.USERNAME == GetLoginUsername())
                {
                    return Json(new { status = false, message = "You cannot delete your own account." });
                }

                DBResult result = userRepo.Delete(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult DeleteAttachment(User data)
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
                    result = userRepo.RemoveAttachment(data, GetLoginUsername(), db);
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
            string[] split = path.Split('/');
            string fileName = split[4];
            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);

            return File(bytes, "application/force-download", fileName);
        }

        public JsonResult GetUserName(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                User oUser = new User();
                if (q != null)
                    oUser.FULL_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<User> dataList = userRepo.Search(oUser, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.FULL_NAME, id = data.USERNAME});
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult GetUserNameByDepartmentId(string q, string pageLimit, string page, string param)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                User oUser = new User();

                if (param != "")
                    oUser.DEPARTMENT_ID = int.Parse(param);

                if (q != null)
                    oUser.FULL_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<User> dataList = userRepo.Search(oUser, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.FULL_NAME, id = data.USERNAME });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public ActionResult Profile()
        {
            ViewData["Title"] = "User Profile";
            ViewData["Username"] = HttpContext.Session.GetInt32("USERNAME").ToString();
            ViewData["Url"] = HttpContext.Request.Path.Value;
            return View("Profile");
        }

        //User Position
        public JsonResult GetPositionNameAndPositionID(string q, string pageLimit, string page)
        {
            try
            {
                AjaxResult ajaxResult = new AjaxResult();
                RepoResult repoResult = new RepoResult();

                PositionMaster oPositionMaster = new PositionMaster();
                if (q != null)
                    oPositionMaster.POSITION_NAME = '*' + q + '*';

                int result, pageInt;

                if (int.TryParse(page, out result))
                {
                    pageInt = int.Parse(page);
                }
                else
                {
                    pageInt = 1;
                }

                IList<PositionMaster> dataList = positionRepo.Search(oPositionMaster, db, pageInt, int.Parse(pageLimit));

                var list = new List<Select2>();
                foreach (var data in dataList)
                {
                    list.Add(new Select2() { text = data.POSITION_NAME, id = data.POSITION_ID.ToString() });
                }
                return Json(new { status = true, items = list });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult PositionGetByKey(UserPosition data)
        {
            try
            {
                UserPosition result = userRepo.PositionGetByKey(data, db);
                if (result != null)
                {
                    if (result.POSITION_ID == 5) //if Position EO
                    {
                        IList<UserPosition> listResult = userRepo.SearchPosition(data, db, null, null);

                        return Json(new { status = true, data = listResult, mode = "multiple" });
                    }
                    else
                    {
                        return Json(new { status = true, data = result, mode = "normal" });
                    }
                }
                else
                {
                    return Json(new { status = true, data = result, mode = "normal" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }


        public JsonResult AddEditPosition(string screenMode, UserPosition data, List<string> arrDivision)
        {
            DBResult result = null;
            try
            {
                if (data.POSITION_ID == 5)
                {
                    if (arrDivision.Count > 0)
                    {
                        result = userRepo.DeletePosition(data, GetLoginUsername(), db);

                        var currentData = userRepo.SearchPosition(data, db, null, null);

                        for (int i = 0; i < arrDivision.Count; i++)
                        {
                            data.DIVISION = arrDivision[i];
                            result = userRepo.InsertUpdatePosition(data, GetLoginUsername(), db);
                        }
                    }
                    else
                    {
                        return Json(new { status = false, message = "Division Cannot be null" });
                    }
                }
                else
                {
                    result = userRepo.InsertUpdatePosition(data, GetLoginUsername(), db);
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

    }
}
