using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Dedicated read model for the redesigned User Profile page (backed by
    // sp_User_GetByKey). Kept separate from the shared User entity (used as-is by
    // UserRepo.Search/GetByKey for the admin User Management grid, and by LoginRepo
    // during authentication) so adding SIGNATURE_PATH here can't break those other
    // FromSqlRaw<User> callers, which require every mapped property to have a matching
    // column in their own SP call. Deliberately NOT mapped to a table via [Table] - see
    // DBContext.OnModelCreating (HasNoKey + ToView(null)).
    public class UserProfileDetail : BaseModel
    {
        public string? USERNAME { get; set; }
        public string? REG_NO { get; set; }
        public string? FULL_NAME { get; set; }
        public string? EMAIL { get; set; }
        public string? PHONE { get; set; }
        public string? ROLE_ID { get; set; }
        public string? ROLE_NAME { get; set; }
        public int? POSITION_ID { get; set; }
        public string? POSITION_NAME { get; set; }
        public string? DIVISION { get; set; }
        public string? DIVISION_NAME { get; set; }
        public int? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public int? SECTION_ID { get; set; }
        public string? SECTION_CODE { get; set; }
        public string? SECTION_NAME { get; set; }
        public string? DOCUMENT_CONTROL_ACCESS { get; set; }
        public string? FILE_PATH { get; set; }

        // Dedicated digital-signature image, separate from FILE_PATH (profile photo).
        public string? SIGNATURE_PATH { get; set; }

        public string? AD_USER { get; set; }

        // Present in sp_User_GetByKey's output solely so the shared User model's
        // password-presence check (gHasLocalPassword) still works the same way here.
        public string? PASSWORD { get; set; }
    }
}
