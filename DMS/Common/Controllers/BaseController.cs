using DMS.Models.DB;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace DMS.Common.Controllers
{
    public class BaseController : Controller
    {
        public string GenerateMenu(List<Menu> menus)
        {
            DataSet ds = new DataSet();
            ds = ToDataSet(menus);
            DataTable table = ds.Tables[0];
            DataRow[] parentMenus = table.Select("PARENT_ID IS NULL");
            var sb = new StringBuilder();
            string menuString = GenerateUL(parentMenus, table, sb);

            return menuString;
        }

        public string GenerateUL(DataRow[] menu, DataTable table, StringBuilder sb)
        {
            if (menu.Length > 0)
            {
                foreach (DataRow dr in menu)
                {
                    string menuText = dr["MENU_NAME"].ToString();
                    string url = dr["MENU_URL"].ToString();
                    string icon = dr["MENU_ICON"].ToString();
                    string parentId = dr["PARENT_ID"].ToString();
                    string pid = dr["MENU_ID"].ToString();
                    DataRow[] subMenu = table.Select(String.Format("PARENT_ID = '{0}'", pid));

                    // Menu yang punya child selalu dirender sebagai grup expand saja,
                    // supaya tidak dobel muncul kalau MENU_URL-nya kelupaan diisi "#".
                    if (url != "#" && subMenu.Length == 0)
                    {
                        string line = String.Format(@"<li id=""sidebarMenu{0}"" data-parentId=""{3}""><a href=""{0}""><i class=""fa {2}""></i> <span>{1}</span></a></li>", url, menuText, icon, parentId);
                        sb.Append(line);
                    }
                    if (subMenu.Length > 0 && !pid.Equals(parentId))
                    {
                        string line = String.Format(@"<li id=""parentId{3}"" class=""treeview""><a href=""#""><i class=""fa {0}""></i> <span>{1}</span><span class=""pull-right-container""><i class=""fa fa-angle-left pull-right""></i></span></a><ul class=""treeview-menu"">", icon, menuText, parentId, pid);
                        var subMenuBuilder = new StringBuilder();
                        sb.AppendLine(line);
                        sb.Append(GenerateUL(subMenu, table, subMenuBuilder));
                        sb.Append("</ul></li>");
                    }
                }
            }
            return sb.ToString();
        }

        public DataSet ToDataSet<T>(List<T> items)
        {
            DataTable dataTable = new DataTable(typeof(T).Name);
            //Get all the properties
            PropertyInfo[] Props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo prop in Props)
            {
                //Setting column names as Property names
                dataTable.Columns.Add(prop.Name);
            }
            foreach (T item in items)
            {
                var values = new object[Props.Length];
                for (int i = 0; i < Props.Length; i++)
                {
                    values[i] = Props[i].GetValue(item, null);
                }
                dataTable.Rows.Add(values);
            }
            DataSet ds = new DataSet();
            ds.Tables.Add(dataTable);
            return ds;
        }

        public void CreateSession(User user, string menuString, string menuURL, string functionList)
        {
            HttpContext.Session.SetString("USERNAME", (user.USERNAME == null) ? "" : user.USERNAME);
            HttpContext.Session.SetString("EMAIL", (user.EMAIL == null) ? "" : user.EMAIL);
            HttpContext.Session.SetString("PHONE", (user.PHONE == null) ? "" : user.PHONE);
            HttpContext.Session.SetString("ROLE_ID", (user.ROLE_ID == null) ? "" : user.ROLE_ID);
            HttpContext.Session.SetInt32("POSITION_ID", (int)((user.POSITION_ID == null) ? 0 : user.POSITION_ID));
            HttpContext.Session.SetString("POSITION_NAME", (user.POSITION_NAME == null) ? "" : user.POSITION_NAME);
            HttpContext.Session.SetString("DIVISION", (user.DIVISION == null) ? "" : user.DIVISION);
            HttpContext.Session.SetString("DIVISION_NAME", (user.DIVISION_NAME == null) ? "" : user.DIVISION_NAME);
            HttpContext.Session.SetInt32("DEPARTMENT_ID", (int)((user.DEPARTMENT_ID == null) ? 0 : user.DEPARTMENT_ID));
            HttpContext.Session.SetString("DEPARTMENT_CODE", (user.DEPARTMENT_CODE == null) ? "" : user.DEPARTMENT_CODE);
            HttpContext.Session.SetString("DEPARTMENT_NAME", (user.DEPARTMENT_NAME == null) ? "" : user.DEPARTMENT_NAME);
            HttpContext.Session.SetInt32("SECTION_ID", (int)((user.SECTION_ID == null) ? 0 : user.SECTION_ID));
            HttpContext.Session.SetString("SECTION_CODE", (user.SECTION_CODE == null) ? "" : user.SECTION_CODE);
            HttpContext.Session.SetString("SECTION_NAME", (user.SECTION_NAME == null) ? "" : user.SECTION_NAME);
            HttpContext.Session.SetString("DOCUMENT_CONTROL_ACCESS", (user.DOCUMENT_CONTROL_ACCESS == null) ? "" : user.DOCUMENT_CONTROL_ACCESS);
            HttpContext.Session.SetString("FILE_PATH", (user.FILE_PATH == null) ? "" : user.FILE_PATH);
            HttpContext.Session.SetString("menuString", menuString);
            HttpContext.Session.SetString("menuURL", menuURL);
            HttpContext.Session.SetString("functionList", functionList);
        }

        public void ClearSession()
        {
            HttpContext.Session.Clear();
        }

        public string GetLoginUsername()
        {
            return HttpContext.Session.GetString("USERNAME");
        }

        //Cek apakah user yang login berhak akses menu tertentu (mis. "/Reassign/Index"),
        //berdasarkan CSV menuURL yang disimpan saat login (lihat CreateSession).
        //Dipakai untuk guard action Index (redirect/403) maupun endpoint AJAX (json status=false),
        //supaya endpoint AJAX tidak bisa dipanggil langsung oleh user yang tidak punya menunya.
        public bool HasMenuAccess(string menuUrl)
        {
            if (HttpContext.Session.GetString("USERNAME") == null)
            {
                return false;
            }

            string menuURLList = HttpContext.Session.GetString("menuURL");
            return menuURLList != null && menuURLList.Contains(menuUrl);
        }

        public string GetLoginDepartmentCode()
        {
            return HttpContext.Session.GetString("DEPARTMENT_CODE");
        }

        public string GetLoginDivision()
        {
            return HttpContext.Session.GetString("DIVISION");
        }

        public int? GetLoginDepartmentId()
        {
            return HttpContext.Session.GetInt32("DEPARTMENT_ID");
        }

        public bool? GetDocumentAccessControl()
        {
            return HttpContext.Session.GetString("DOCUMENT_CONTROL_ACCESS") == "1";
        }

        protected ICellStyle CellStyleLeft(XSSFWorkbook oHSSFWorkbook)
        {
            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Left;
            return oICellStyle;
        }

        protected ICellStyle CellStyleRight(XSSFWorkbook oHSSFWorkbook)
        {
            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Right;
            return oICellStyle;
        }

        protected ICellStyle CellStyleCenter(XSSFWorkbook oHSSFWorkbook)
        {
            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Top;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Center;
            return oICellStyle;
        }

        protected ICellStyle QuestionParentStyle(XSSFWorkbook oHSSFWorkbook)
        {
            var font = oHSSFWorkbook.CreateFont();
            font.IsBold = true;

            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Center;
            oICellStyle.FillForegroundColor = IndexedColors.LightYellow.Index;
            oICellStyle.FillPattern = FillPattern.SolidForeground;
            oICellStyle.SetFont(font);

            return oICellStyle;
        }

        protected ICellStyle KatashikiTitleStyle(XSSFWorkbook oHSSFWorkbook)
        {
            var font = oHSSFWorkbook.CreateFont();
            font.FontHeightInPoints = 16;
            font.FontName = "Arial";

            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Center;
            oICellStyle.SetFont(font);

            return oICellStyle;
        }

        protected ICellStyle KatashikiCodeStyle(XSSFWorkbook oHSSFWorkbook)
        {
            var font = oHSSFWorkbook.CreateFont();
            font.FontHeightInPoints = 11;
            font.FontName = "Arial";

            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Center;
            oICellStyle.FillForegroundColor = IndexedColors.Lavender.Index;
            oICellStyle.FillPattern = FillPattern.SolidForeground;
            oICellStyle.SetFont(font);

            return oICellStyle;
        }

        protected ICellStyle NoCodeStyle(XSSFWorkbook oHSSFWorkbook)
        {
            var font = oHSSFWorkbook.CreateFont();
            font.FontHeightInPoints = 11;
            font.FontName = "Arial";
            font.IsBold = true;

            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Center;
            oICellStyle.BorderTop = BorderStyle.Thin;
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderLeft = BorderStyle.Thin;
            oICellStyle.BorderRight = BorderStyle.Thin;
            oICellStyle.Alignment = HorizontalAlignment.Center;
            oICellStyle.SetFont(font);

            return oICellStyle;
        }

        protected ICellStyle VerticalBorderStyle(XSSFWorkbook oHSSFWorkbook)
        {
            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.BorderBottom = BorderStyle.Thin;
            oICellStyle.BorderTop = BorderStyle.Thin;
            return oICellStyle;
        }

        protected ICellStyle HeaderTitle(XSSFWorkbook oHSSFWorkbook)
        {
            var font = oHSSFWorkbook.CreateFont();
            font.FontHeightInPoints = 18;
            font.FontName = "Calibri";
            font.IsBold = true;

            ICellStyle oICellStyle = oHSSFWorkbook.CreateCellStyle();
            oICellStyle.VerticalAlignment = VerticalAlignment.Top;
            oICellStyle.BorderLeft = BorderStyle.Medium;
            oICellStyle.BorderRight = BorderStyle.Medium;
            oICellStyle.BorderBottom = BorderStyle.Medium;
            oICellStyle.BorderTop = BorderStyle.Medium;
            oICellStyle.Alignment = HorizontalAlignment.Center;
            oICellStyle.SetFont(font);

            return oICellStyle;
        }

        public string EncryptWithKey(string password)
        {
            //must 16 digits
            string key = "key-qcv-hino2023";

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] encryptedBytes, decryptedBytes;

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.Mode = CipherMode.ECB; // Use ECB mode for simplicity
                aes.Padding = PaddingMode.PKCS7;

                // Encrypt the password
                ICryptoTransform encryptor = aes.CreateEncryptor();
                encryptedBytes = encryptor.TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);

                // Decrypt the password
                ICryptoTransform decryptor = aes.CreateDecryptor();
                decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
            }

            string encryptedPassword = Convert.ToBase64String(encryptedBytes);
            string decryptedPassword = Encoding.UTF8.GetString(decryptedBytes);

            return encryptedPassword;
        }

        public string DecryptWithKey(string encryptedString)
        {
            //must 16 digits
            string keyString = "key-qcv-hino2023";
            string decryptedPassword;

            byte[] key = Encoding.UTF8.GetBytes(keyString);

            // Convert the encrypted string to bytes
            byte[] encryptedBytes = Convert.FromBase64String(encryptedString);

            // Create the AES decryptor
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.ECB; // Use ECB mode for simplicity
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform decryptor = aes.CreateDecryptor();
                byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

                decryptedPassword = Encoding.UTF8.GetString(decryptedBytes);
            }

            return decryptedPassword;
        }
    }
}