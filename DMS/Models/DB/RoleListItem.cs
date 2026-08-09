using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Dedicated read model for the Role Authorization grid (sp_Role_Search).
    // Kept separate from Role (shared by RoleRepo's GetByKey/Insert/Update/Delete
    // via their own SPs) so extending this one with USERS_COUNT/PERMISSIONS_COUNT
    // can't break those other FromSqlRaw<Role> calls.
    // Deliberately NOT mapped to a table via [Table] - see DBContext.OnModelCreating,
    // where it's configured as keyless and unmapped (ToView(null)) so EF doesn't treat
    // it as sharing TB_M_ROLE with Role. It only exists to shape FromSqlRaw results.
    public class RoleListItem : BaseModel
    {
        public string? ROLE_ID { get; set; }
        public string? ROLE_NAME { get; set; }
        public string? ROLE_DESC { get; set; }

        // Count of active (non-soft-deleted) TB_M_USER rows with this ROLE_ID.
        public int? USERS_COUNT { get; set; }

        // Count of TB_M_AUTH_MENU + TB_M_AUTH_FUNCTION rows assigned to this role.
        public int? PERMISSIONS_COUNT { get; set; }
    }
}
