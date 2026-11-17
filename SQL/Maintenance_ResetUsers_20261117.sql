-- =====================================================================
-- Maintenance: remove every user except dms.admin (request Hendra 2026-11-17).
-- Users are entered again later with the 2026 organisation codes
-- (SQL/MasterOrganization2026_Migration.sql). Run after
-- SQL/Maintenance_ResetTransactionData_20261117.sql (no transaction points at a user).
--
--   - TB_M_USER, TB_M_USER_POS, TB_M_DOCUMENT_FOLDER_PERSONAL: rows of other users deleted.
--     Signature / avatar files on disk are not touched.
--   - QPD and its section 00 were only kept for those users' positions: deleted now.
--   - Document Control access moves from QPD to its successor QPA (Quality Planning &
--     Audit System - section 03 Quality Management System), so QMS users entered
--     under QPA get the Document Control menus again.
--
-- Backups: *_BACKUP_20261117U. One transaction. Run on database DMS_NEW.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.TB_M_USER_BACKUP_20261117U', 'U') IS NOT NULL
BEGIN
    RAISERROR('Already run (TB_M_USER_BACKUP_20261117U exists).', 16, 1);
    SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

SELECT * INTO dbo.TB_M_USER_BACKUP_20261117U                   FROM dbo.TB_M_USER;
SELECT * INTO dbo.TB_M_USER_POS_BACKUP_20261117U               FROM dbo.TB_M_USER_POS;
SELECT * INTO dbo.TB_M_DOCUMENT_FOLDER_PERSONAL_BACKUP_20261117U FROM dbo.TB_M_DOCUMENT_FOLDER_PERSONAL;
SELECT * INTO dbo.TB_M_DEPARTMENT_BACKUP_20261117U             FROM dbo.TB_M_DEPARTMENT WHERE DEPARTMENT_CODE IN ('QPD', 'QPA');
SELECT * INTO dbo.TB_M_SECTION_BACKUP_20261117U                FROM dbo.TB_M_SECTION WHERE DEPARTMENT_CODE = 'QPD';

DELETE FROM dbo.TB_M_DOCUMENT_FOLDER_PERSONAL WHERE USERNAME <> 'dms.admin';
DELETE FROM dbo.TB_M_USER_POS WHERE USERNAME <> 'dms.admin';
DELETE FROM dbo.TB_M_USER WHERE USERNAME <> 'dms.admin';

UPDATE dbo.TB_M_DEPARTMENT SET DOCUMENT_CONTROL_ACCESS = 1, CHANGED_BY = 'dms.admin', CHANGED_DT = GETDATE()
WHERE DEPARTMENT_CODE = 'QPA';

DELETE s FROM dbo.TB_M_SECTION s
WHERE s.DEPARTMENT_CODE = 'QPD'
  AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS p WHERE p.SECTION_ID = s.SECTION_ID);
DELETE d FROM dbo.TB_M_DEPARTMENT d
WHERE d.DEPARTMENT_CODE = 'QPD'
  AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS p WHERE p.DEPARTMENT_ID = d.DEPARTMENT_ID)
  AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_SECTION s WHERE s.DEPARTMENT_CODE = d.DEPARTMENT_CODE);

COMMIT TRANSACTION;

SELECT (SELECT COUNT(*) FROM dbo.TB_M_USER) USERS, (SELECT COUNT(*) FROM dbo.TB_M_USER_POS) POSITIONS,
       (SELECT COUNT(*) FROM dbo.TB_M_DEPARTMENT) DEPARTMENTS, (SELECT COUNT(*) FROM dbo.TB_M_SECTION) SECTIONS,
       (SELECT STRING_AGG(DEPARTMENT_CODE, ',') FROM dbo.TB_M_DEPARTMENT WHERE DOCUMENT_CONTROL_ACCESS = 1) DOC_CONTROL;
GO
SET NOEXEC OFF;
GO
