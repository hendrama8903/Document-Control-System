using DMS.Models.DB;
using Microsoft.EntityFrameworkCore;
//Recommit DINA

namespace DMS.Models
{
    public partial class DBContext : DbContext
    {
        public DBContext(DbContextOptions<DBContext> options) : base(options) { }

        public virtual DbSet<User>? User { get; set; }
        public virtual DbSet<UserListItem>? UserListItem { get; set; }
        public virtual DbSet<RoleListItem>? RoleListItem { get; set; }
        public virtual DbSet<MenuListItem>? MenuListItem { get; set; }
        public virtual DbSet<FunctionListItem>? FunctionListItem { get; set; }
        public virtual DbSet<MenuFunctionStats>? MenuFunctionStats { get; set; }
        public virtual DbSet<UserProfileDetail>? UserProfileDetail { get; set; }
        public virtual DbSet<UserPosition>? UserPosition { get; set; }
        public virtual DbSet<Role>? Role { get; set; }
        public virtual DbSet<Menu>? Menu { get; set; }
        public virtual DbSet<Function>? Function { get; set; }
        public virtual DbSet<AuthMenu>? AuthMenu { get; set; }
        public virtual DbSet<AuthFunction>? AuthFunction { get; set; }
        public virtual DbSet<MSystem>? MSystem { get; set; }
        public virtual DbSet<LogHeader>? LogHeader { get; set; }
        public virtual DbSet<LogDetail>? LogDetail { get; set; }
        public virtual DbSet<DocumentMaster>? DocumentMaster { get; set; }
        public virtual DbSet<DepartmentMaster>? DepartmentMaster { get; set; }
        public virtual DbSet<DivisionMaster>? DivisionMaster { get; set; }
        public virtual DbSet<SectionMaster>? SectionMaster { get; set; }
        public virtual DbSet<Select2SectionCode>? Select2SectionCode { get; set; }
        public virtual DbSet<Select2SectionName>? Select2SectionName { get; set; }
        public virtual DbSet<Select2SectionCodeAndName>? Select2SectionCodeAndName { get; set; }
        public virtual DbSet<WorkflowDoc>? WorkflowDoc { get; set; }
        public virtual DbSet<DocumentMaintenance>? DocumentMaintenance { get; set; }
        public virtual DbSet<DocumentRelatedDivision>? DocumentRelatedDivision { get; set; }
        public virtual DbSet<DocumentApproval>? DocumentApproval { get; set; }
        public virtual DbSet<DocumentDistribution>? DocumentDistribution { get; set; }
        public virtual DbSet<DocumentControlMaintenance>? P4DMaintenance { get; set; }
        public virtual DbSet<DocumentControlMaintenance>? UserDashboard { get; set; }
        public virtual DbSet<UserDashboardDocument>? UserDashboardSearch { get; set; }
        public virtual DbSet<DocumentControlMaintenance>? DocumentControlDashboard { get; set; }
        public virtual DbSet<DocumentControlDashboardDocument>? DocumentControlDashboardSearch { get; set; }
        public virtual DbSet<DocumentFolder>? DocumentFolder { get; set; }
        public virtual DbSet<DocumentFolderPersonal>? DocumentFolderPersonal { get; set; }
        public virtual DbSet<ApprovalHeader>? ApprovalHeader { get; set; }
        public virtual DbSet<ApprovalDetail>? ApprovalDetail { get; set; }
        public virtual DbSet<PositionMaster>? PositionMaster { get; set; }
        public virtual DbSet<WorkflowDocArr>? WorkflowDocArr { get; set; }
        public virtual DbSet<DocumentLog>? DocumentLog { get; set; }
        public virtual DbSet<Notification>? Notification { get; set; }
        public virtual DbSet<ExcelTemplateMaster>? ExcelTemplateMaster { get; set; }
        public virtual DbSet<DocumentHistory>? DocumentHistory { get; set; }
        public virtual DbSet<DocumentReport>? DocumentReport { get; set; }
        public virtual DbSet<DocumentMasterlist>? DocumentMasterlist { get; set; }
        public virtual DbSet<Select2DivisionUser>? Select2DivisionUser { get; set; }
        public virtual DbSet<ExternalDocument>? ExternalDocument { get; set; }
        public virtual DbSet<DocumentSubmission>? DocumentSubmission { get; set; }
        public virtual DbSet<DocumentSubmissionDetail>? DocumentSubmissionDetail { get; set; }
        public virtual DbSet<CopyRequest>? CopyRequest { get; set; }
        public virtual DbSet<CopyRequestDetail>? CopyRequestDetail { get; set; }
        public virtual DbSet<CopyRequestDetailForPrint>? CopyRequestDetailForPrint { get; set; }
        public virtual DbSet<CopyRequestPrintQueueItem>? CopyRequestPrintQueueItem { get; set; }
        public virtual DbSet<CopyRequestPrintLogItem>? CopyRequestPrintLogItem { get; set; }
        public virtual DbSet<CopyRequestPrintLog>? CopyRequestPrintLog { get; set; }
        public virtual DbSet<MSequence>? MSequence { get; set; }
        public virtual DbSet<DocumentReceiptReport>? DocumentReceiptReport { get; set; }
        public virtual DbSet<NotificationSetting>? NotificationSetting { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Read-only FromSqlRaw projection over TB_R_CTRL_DOCUMENT (see UserDashboardDocument.cs) -
            // keyless so EF doesn't require a linking relationship with DocumentControlMaintenance,
            // which also maps to this table for its own (different) FromSqlRaw queries.
            builder.Entity<UserDashboardDocument>().HasNoKey().ToView(null);
            builder.Entity<DocumentControlDashboardDocument>().HasNoKey().ToView(null);
            builder.Entity<UserProfileDetail>().HasNoKey().ToView(null);
            builder.Entity<RoleListItem>().HasNoKey().ToView(null);
            builder.Entity<MenuListItem>().HasNoKey().ToView(null);
            builder.Entity<FunctionListItem>().HasNoKey().ToView(null);
            builder.Entity<MenuFunctionStats>().HasNoKey().ToView(null);
            builder.Entity<UserListItem>().HasNoKey().ToView(null);
            builder.Entity<NotificationSetting>().HasNoKey().ToView(null);
            builder.Entity<CopyRequestDetailForPrint>().HasNoKey().ToView(null);
            builder.Entity<CopyRequestPrintQueueItem>().HasNoKey().ToView(null);
            builder.Entity<CopyRequestPrintLogItem>().HasNoKey().ToView(null);

            builder.Entity<MSystem>().HasKey(table => new
            {
                table.SYSTEM_TYPE,
                table.SYSTEM_CODE
            });

            builder.Entity<MSequence>().HasKey(table => new
            {
                table.SEQ_TYPE,
                table.SEQ_CODE
            });

            builder.Entity<AuthMenu>().HasKey(table => new
            {
                table.ROLE_ID,
                table.MENU_ID
            });

            builder.Entity<AuthFunction>().HasKey(table => new
            {
                table.ROLE_ID,
                table.FUNCTION_ID
            });

            builder.Entity<LogDetail>().HasKey(table => new
            {
                table.PROCESS_ID,
                table.SEQ_NO
            });

            builder.Entity<Select2SectionName>().HasNoKey();
            builder.Entity<Select2SectionCode>().HasNoKey();
            builder.Entity<Select2SectionCodeAndName>().HasNoKey();
            builder.Entity<WorkflowDoc>().HasNoKey();
            builder.Entity<WorkflowDocArr>().HasNoKey();
            builder.Entity<DocumentReport>().HasNoKey();
            builder.Entity<DocumentMasterlist>().HasNoKey();
            builder.Entity<Select2DivisionUser>().HasNoKey();

        }
    }
}
