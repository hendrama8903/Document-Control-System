-- =====================================================================
-- Print Request Report (2026-11-19): menu M00008-04 under "Reports".
-- Approved Print Requests in the Master List layout (QTY + request date
-- per requesting division), Excel and PDF. View-only, menu access only,
-- granted like the Document Receipt Report: DMS-ADMIN, DOC-CONTROL, AUDITOR.
-- Needs sp_PrintRequestReport_Matrix (SQL/StoredProcedures).
-- Idempotent - safe to run again. Run on DMS_NEW.
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00008-04')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00008-04', 'M00008', 'Print Request Report', 'fa-print', '/PrintRequestReport/Index', 4, 'dms.admin', GETDATE(), 0);
END

INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, 'M00008-04', 'dms.admin', GETDATE()
FROM (VALUES ('DMS-ADMIN'), ('DOC-CONTROL'), ('AUDITOR')) R(ROLE_ID)
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = 'M00008-04');
GO
