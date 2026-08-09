namespace DMS.Models.DB
{
    // Single-row result of sp_Menu_Function_Stats, for the Menu & Function page's stat cards.
    // Keyless / unmapped - see DBContext.OnModelCreating.
    public class MenuFunctionStats
    {
        public int TOTAL_MENUS { get; set; }
        public int TOTAL_FUNCTIONS { get; set; }
        public int USED_BY_ROLES { get; set; }
        public int INACTIVE_COUNT { get; set; }
    }
}
