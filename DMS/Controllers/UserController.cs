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
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

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

        // Update FULL_NAME/SIGNATURE_PATH tidak menyentuh dokumen manapun secara
        // langsung - PDF pengesahan yang sudah kepajang di cache DOCUMENT_TEMP tetap
        // pakai data lama sampai cache-nya dibuang. Daripada melacak dokumen mana saja
        // yang melibatkan user ini sebagai pembuat/approver, cukup buang seluruh cache
        // pengesahan begitu ada perubahan profile; preview berikutnya otomatis generate
        // ulang dengan data terbaru (biayanya cuma delay sekali di preview pertama).
        private void ClearPengesahanPdfCache()
        {
            try
            {
                string cacheDir = Path.Combine(Environment.WebRootPath, "Upload", "ATTACHMENT", "DOCUMENT_TEMP");
                if (Directory.Exists(cacheDir))
                {
                    foreach (string file in Directory.GetFiles(cacheDir, "TEMP_*"))
                    {
                        System.IO.File.Delete(file);
                    }
                }
            }
            catch
            {
                // Best-effort - kegagalan hapus cache tidak boleh menggagalkan update profile.
            }
        }

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
                    Response.StatusCode = 403;
                    return View("Error403");
                }
            }

            // add authorization function
            ViewData["Add"] = HttpContext.Session.GetString("functionList").Contains("USER-ADD");
            ViewData["Edit"] = HttpContext.Session.GetString("functionList").Contains("USER-EDIT");
            ViewData["Delete"] = HttpContext.Session.GetString("functionList").Contains("USER-DELETE");
            ViewData["Position"] = HttpContext.Session.GetString("functionList").Contains("USER-POSITION");
            ViewData["Restore"] = HttpContext.Session.GetString("functionList").Contains("USER-RESTORE");

            ViewData["Title"] = "User Management";

            return View();
        }

        public ActionResult Search(User data, bool initialMode, bool showDeleted = false)
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
                    var userData = userRepo.Search(data, db, pageNumber, pageSize, showDeleted);
                    var dataCount = userRepo.Search(data, db, null, null, showDeleted).Count;
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

        public ActionResult SearchAll(User data)
        {
            try
            {
                var draw = Request.Form["draw"].FirstOrDefault();
                var start = Request.Form["start"].FirstOrDefault();
                var length = Request.Form["length"].FirstOrDefault();
                int pageSize = length != null ? Convert.ToInt32(length) : 0;
                int skip = start != null ? Convert.ToInt32(start) : 0;
                int pageNumber = skip / pageSize + 1;

                var userData = userRepo.SearchAll(data, db, pageNumber, pageSize);
                var dataCount = userRepo.SearchAll(data, db, null, null).Count;
                var jsonData = new { draw = draw, recordsFiltered = dataCount, recordsTotal = dataCount, data = userData };
                return Ok(jsonData);
            }
            catch (Exception ex)
            {
                return Json("Error : " + ex.Message);
            }
        }

        public IActionResult DownloadExcel()
        {
            IList<UserListItem> listData = userRepo.SearchAll(new User(), db, null, null);

            IWorkbook workbook = new XSSFWorkbook();
            ISheet sheet = workbook.CreateSheet("Users");

            string[] headers = { "No", "Username", "Employee ID", "Full Name", "Email", "Phone", "Department", "Role", "Login Type", "Status" };

            IRow headerRow = sheet.CreateRow(0);
            for (int col = 0; col < headers.Length; col++)
            {
                headerRow.CreateCell(col).SetCellValue(headers[col]);
            }

            int rowIndex = 1;
            int no = 1;
            foreach (UserListItem item in listData)
            {
                IRow row = sheet.CreateRow(rowIndex);
                row.CreateCell(0).SetCellValue(no);
                row.CreateCell(1).SetCellValue(item.USERNAME);
                row.CreateCell(2).SetCellValue(item.REG_NO);
                row.CreateCell(3).SetCellValue(item.FULL_NAME);
                row.CreateCell(4).SetCellValue(item.EMAIL);
                row.CreateCell(5).SetCellValue(item.PHONE);
                row.CreateCell(6).SetCellValue(item.DEPARTMENT_CODE != null ? item.DEPARTMENT_CODE + " - " + item.DEPARTMENT_NAME : "");
                row.CreateCell(7).SetCellValue(item.ROLE_NAME);
                row.CreateCell(8).SetCellValue(item.AD_USER == "1" ? "AD" : "Local");
                row.CreateCell(9).SetCellValue(item.DELETE_FLAG == "1" ? "Inactive" : "Active");

                rowIndex++;
                no++;
            }

            for (int col = 0; col < headers.Length; col++)
            {
                sheet.AutoSizeColumn(col);
            }

            byte[] fileBytes;
            using (var memoryStream = new MemoryStream())
            {
                workbook.Write(memoryStream);
                fileBytes = memoryStream.ToArray();
            }

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "USERS-" + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".xlsx");
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
                        if (result.status)
                        {
                            ClearPengesahanPdfCache();

                            // Foto profil di header (_AdminLayout) dibaca dari session
                            // FILE_PATH yang diisi saat login - kalau user sedang edit
                            // profilnya sendiri (bukan admin edit user lain), session itu
                            // harus ikut di-refresh di sini juga, supaya foto baru
                            // langsung kelihatan di header tanpa perlu logout/login ulang.
                            if (data.USERNAME == GetLoginUsername())
                            {
                                HttpContext.Session.SetString("FILE_PATH", data.FILE_PATH ?? "");
                            }
                        }
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

        public JsonResult Restore(User data)
        {
            try
            {
                DBResult result = userRepo.Restore(data, GetLoginUsername(), db);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Richer version of GetByKey (adds SIGNATURE_PATH) for the redesigned Profile
        // page - GetByKey above stays untouched for the admin User Management grid.
        public JsonResult GetProfileDetail(User data)
        {
            try
            {
                UserProfileDetail result = userRepo.GetProfileDetail(data, db);
                return Json(new { status = true, data = result });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        // Independent of AddEditAsync's own (avatar) file handling - reads the upload by
        // form field name rather than position, so it can't collide with an avatar file
        // sent in the same or a different request.
        public async Task<JsonResult> UploadSignatureAsync(UserProfileDetail data)
        {
            string folderName = "/Upload/";
            string webRootPath = Environment.WebRootPath;

            try
            {
                if (Request.Form.Files.Count == 0)
                {
                    return Json(new { status = false, message = "No signature file uploaded." });
                }

                IFormFile file = Request.Form.Files[0];
                string extension = Path.GetExtension(file.FileName).ToLower();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    return Json(new { status = false, message = "Only JPG and PNG files are allowed." });
                }
                if (file.Length > 2 * 1024 * 1024)
                {
                    return Json(new { status = false, message = "Signature file must be 2 MB or smaller." });
                }

                // Look up the current signature path server-side (rather than trusting a
                // client-supplied value) so the old file gets cleaned up on replacement.
                UserProfileDetail existing = userRepo.GetProfileDetail(new User { USERNAME = data.USERNAME }, db);

                MSystem mSystem = mSystemRepo.GetByKey(new MSystem { SYSTEM_TYPE = "UPLOAD_FOLDER", SYSTEM_CODE = "USER" }, db);
                string path = folderName + mSystem.SYSTEM_VALUE.Trim();
                string pathSave = webRootPath + folderName + mSystem.SYSTEM_VALUE.Trim();
                string fileName = "Signature-" + DateTime.Now.ToFileTime() + extension;
                string finalPath = pathSave + fileName;

                if (!Directory.Exists(pathSave))
                {
                    Directory.CreateDirectory(pathSave);
                }

                if (existing?.SIGNATURE_PATH != null)
                {
                    string pathCheck = webRootPath + existing.SIGNATURE_PATH.Trim();
                    if (System.IO.File.Exists(pathCheck))
                    {
                        System.IO.File.Delete(pathCheck);
                    }
                }

                using (var stream = new FileStream(finalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                data.SIGNATURE_PATH = path + fileName;
                DBResult result = userRepo.UpdateSignature(data, GetLoginUsername(), db);
                if (result.status)
                {
                    ClearPengesahanPdfCache();
                }
                return Json(new { status = result.status, message = result.message, filePath = data.SIGNATURE_PATH });
            }
            catch (Exception ex)
            {
                return Json(new { status = false, message = ex.Message });
            }
        }

        public JsonResult DeleteSignature(UserProfileDetail data)
        {
            string webRootPath = Environment.WebRootPath;
            try
            {
                if (data.SIGNATURE_PATH != null)
                {
                    string pathCheck = webRootPath + data.SIGNATURE_PATH.Trim();
                    if (System.IO.File.Exists(pathCheck))
                    {
                        System.IO.File.Delete(pathCheck);
                    }
                }

                data.SIGNATURE_PATH = null;
                DBResult result = userRepo.UpdateSignature(data, GetLoginUsername(), db);
                if (result.status)
                {
                    ClearPengesahanPdfCache();
                }
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
