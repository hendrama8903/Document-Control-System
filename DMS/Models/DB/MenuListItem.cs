using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Dedicated read model for the Menu & Function tree panel (sp_Menu_Tree).
    // Kept separate from Menu (shared by MenuRepo's GetByKey/Insert/Update/Delete/Search
    // via their own SPs) so extending this one with DELETE_FLAG/FUNCTION_COUNT/USED_BY_ROLES
    // can't break those other FromSqlRaw<Menu> calls.
    // Deliberately NOT mapped to a table via [Table] - see DBContext.OnModelCreating,
    // where it's configured as keyless and unmapped (ToView(null)) so EF doesn't treat
    // it as sharing TB_M_MENU with Menu. It only exists to shape FromSqlRaw results.
    public class MenuListItem : BaseModel
    {
        public string? MENU_ID { get; set; }
        public string? PARENT_ID { get; set; }
        public string? PARENT_NAME { get; set; }
        public string? MENU_NAME { get; set; }
        public string? MENU_ICON { get; set; }
        public string? MENU_URL { get; set; }
        public int? MENU_SEQ { get; set; }
        public int? DELETE_FLAG { get; set; }

        // Own function count for a child menu, summed across children for a top-level menu.
        public int? FUNCTION_COUNT { get; set; }

        // Distinct roles with this MENU_ID in TB_M_AUTH_MENU.
        public int? USED_BY_ROLES { get; set; }
    }
}
