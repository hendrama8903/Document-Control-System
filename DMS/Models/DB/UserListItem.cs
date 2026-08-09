using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Dedicated read model for the User Management grid (sp_User_Search with @SHOW_ALL='1').
    // Kept separate from User (shared by UserRepo's GetByKey/Insert/Update/Delete via their
    // own SPs, and by the existing paged Search() used elsewhere) so extending this one with
    // DELETE_FLAG can't break those other FromSqlRaw<User> calls.
    // Deliberately NOT mapped to a table via [Table] - see DBContext.OnModelCreating,
    // where it's configured as keyless and unmapped (ToView(null)) so EF doesn't treat
    // it as sharing TB_M_USER with User. It only exists to shape FromSqlRaw results.
    public class UserListItem : BaseModel
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
        public string? FILE_PATH { get; set; }
        public string? AD_USER { get; set; }
        public string? DELETE_FLAG { get; set; }
    }
}
