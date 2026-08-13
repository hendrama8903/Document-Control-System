using DMS.Helpers;
using DMS.Models;
using DMS.Models.DB;
using DMS.Models.PrintTrack;
using DMS.Models.Repo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DMS.Controllers
{
    // Login khusus PrintTrack (desktop app) - kredensial AD yang sama dipakai
    // login web (lihat AuthController.Validate), tapi hasilnya JWT Bearer
    // token, bukan session cookie (desktop tidak ikut siklus hidup browser
    // session). Infrastruktur validasi JWT-nya sendiri sudah ada di
    // Startup.cs (awalnya disiapkan untuk MDC/Master Data Centralize, belum
    // pernah dipakai controller manapun sampai modul ini).
    //
    // SENGAJA tidak mewarisi BaseController (beda dari AuthController) -
    // BaseController.GenerateUL(DataRow[], DataTable, StringBuilder) yang
    // ikut terwarisi bikin startup app CRASH saat dipasangkan dengan
    // [ApiController]: ApiController mencoba infer parameter binding
    // otomatis untuk SEMUA method public (termasuk yang bukan action
    // sungguhan seperti GenerateUL), gagal karena ada beberapa parameter
    // complex-type sekaligus. EncryptWithKey disalin lokal sebagai
    // gantinya (lihat BaseController.EncryptWithKey untuk versi aslinya).
    [ApiController]
    [Route("api/printtrack/auth")]
    public class PrintTrackAuthController : Controller
    {
        private const int TokenLifetimeHours = 12;

        private readonly DBContext db;
        private readonly IConfiguration configuration;
        private LoginRepo loginRepo = LoginRepo.Instance;

        public PrintTrackAuthController(DBContext db, IConfiguration configuration)
        {
            this.db = db;
            this.configuration = configuration;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] PrintTrackLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Username) || string.IsNullOrWhiteSpace(request?.Password))
            {
                return Json(new { status = false, message = "Username and password are required" });
            }

            User user = loginRepo.GetUserByUsername(new User { USERNAME = request.Username }, db);
            if (user == null)
            {
                return Json(new { status = false, message = "Invalid Username!" });
            }

            bool isPasswordValid;
            string invalidMessage = "Invalid Password!";

            if (user.AD_USER == "1")
            {
                string domain = configuration["ActiveDirectory:Domain"];
                (bool Success, string Message) adResult = ActiveDirectoryHelper.ValidateUser(domain, request.Username, request.Password);
                isPasswordValid = adResult.Success;
                if (!adResult.Success) { invalidMessage = adResult.Message; }
            }
            else
            {
                isPasswordValid = user.PASSWORD == EncryptWithKey(request.Password);
            }


            if (!isPasswordValid)
            {
                return Json(new { status = false, message = invalidMessage });
            }

            DateTime expiresAt = DateTime.UtcNow.AddHours(TokenLifetimeHours);
            string token = GenerateToken(user.USERNAME, expiresAt);

            return Json(new
            {
                status = true,
                message = "Login Successful!",
                token,
                expiresAt,
                fullName = user.FULL_NAME
            });
        }

        private string GenerateToken(string username, DateTime expiresAt)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["AppSettings:Token"]));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: expiresAt,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Salinan persis BaseController.EncryptWithKey - lihat komentar kelas
        // di atas untuk alasan tidak mewarisi BaseController.
        private static string EncryptWithKey(string password)
        {
            string key = "key-qcv-hino2023"; // must 16 digits

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);

            using (Aes aes = Aes.Create())
            {
                aes.Key = keyBytes;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aes.CreateEncryptor();
                byte[] encryptedBytes = encryptor.TransformFinalBlock(passwordBytes, 0, passwordBytes.Length);
                return Convert.ToBase64String(encryptedBytes);
            }
        }
    }
}
