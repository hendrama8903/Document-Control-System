-- =====================================================================
-- Role review (request Hendra 2026-11-17): general roles do not get master data or
-- settings. USER already has none. APPROVER still had Master Data > Document Category
-- (view + Download Template); templates are downloaded from Document Preparation >
-- Template, so the menu and its function are removed from APPROVER, and the empty
-- Master Data parent with them. Applies at the user's next login.
-- Run on database DMS_NEW. Idempotent.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

IF OBJECT_ID('dbo.TB_M_AUTH_MENU_BACKUP_20261117R', 'U') IS NULL
	SELECT * INTO dbo.TB_M_AUTH_MENU_BACKUP_20261117R FROM dbo.TB_M_AUTH_MENU;
IF OBJECT_ID('dbo.TB_M_AUTH_FUNCTION_BACKUP_20261117R', 'U') IS NULL
	SELECT * INTO dbo.TB_M_AUTH_FUNCTION_BACKUP_20261117R FROM dbo.TB_M_AUTH_FUNCTION;

DELETE FROM dbo.TB_M_AUTH_FUNCTION
WHERE ROLE_ID IN ('APPROVER', 'USER')
  AND FUNCTION_ID IN (SELECT FUNCTION_ID FROM dbo.TB_M_FUNCTION WHERE MENU_ID IN (SELECT MENU_ID FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00002' OR PARENT_ID = 'M00002'));

DELETE FROM dbo.TB_M_AUTH_MENU
WHERE ROLE_ID IN ('APPROVER', 'USER')
  AND MENU_ID IN (SELECT MENU_ID FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00002' OR PARENT_ID = 'M00002');

COMMIT;

SELECT A.ROLE_ID, M.MENU_ID, M.MENU_NAME
FROM dbo.TB_M_AUTH_MENU A JOIN dbo.TB_M_MENU M ON M.MENU_ID = A.MENU_ID
WHERE A.ROLE_ID IN ('APPROVER', 'USER') ORDER BY A.ROLE_ID, M.MENU_ID;
