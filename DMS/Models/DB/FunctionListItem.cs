using DMS.Common.Models;

namespace DMS.Models.DB
{
    // Dedicated read model for the Menu & Function right panel (sp_Function_GetByMenu).
    // Kept separate from Function (shared by FunctionRepo's GetByKey/Insert/Update/Delete/Search
    // via their own SPs) so extending this one with DELETE_FLAG/USED_BY_ROLES can't break
    // those other FromSqlRaw<Function> calls.
    // Deliberately NOT mapped to a table via [Table] - see DBContext.OnModelCreating,
    // where it's configured as keyless and unmapped (ToView(null)) so EF doesn't treat
    // it as sharing TB_M_FUNCTION with Function. It only exists to shape FromSqlRaw results.
    public class FunctionListItem : BaseModel
    {
        public string? FUNCTION_ID { get; set; }
        public string? FUNCTION_NAME { get; set; }
        public string? FUNCTION_DESC { get; set; }
        public string? MENU_ID { get; set; }
        public string? MENU_NAME { get; set; }
        public int? DELETE_FLAG { get; set; }

        // Distinct roles with this FUNCTION_ID in TB_M_AUTH_FUNCTION.
        public int? USED_BY_ROLES { get; set; }
    }
}
