-- =====================================================================
-- Remove test / obsolete Document Categories (request Hendra 2026-11-17).
--   13 TEST  "TEST ERNA"        (inactive, test data)
--   14 TEST  "TEST ERNA 5"      (inactive, test data)
--   16 ZZTES "ZZTEST Category"  (inactive, test data)
--    9 CS    "Checksheet"       (deleted duplicate; CS in use is DOCUMENT_ID 19)
-- None is referenced by TB_R_DOCUMENT, TB_R_DOCUMENT_HISTORY, TB_M_WORKFLOW_DOC_H,
-- TB_M_EXCEL_TEMPLATE, TB_M_STAMP_LAYOUT or TB_M_SEQUENCE (checked before deleting;
-- the script stops if that changed). Backup: TB_M_DOCUMENT_BACKUP_20261117C.
-- Run on database DMS_NEW.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @IDS TABLE (DOCUMENT_ID INT PRIMARY KEY);
INSERT INTO @IDS VALUES (9), (13), (14), (16);

IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @IDS))
   OR EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @IDS))
   OR EXISTS (SELECT 1 FROM dbo.TB_M_WORKFLOW_DOC_H WHERE DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @IDS))
   OR EXISTS (SELECT 1 FROM dbo.TB_M_STAMP_LAYOUT WHERE DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @IDS))
BEGIN
	RAISERROR('A category to remove is in use - nothing deleted.', 16, 1);
	RETURN;
END

BEGIN TRAN;

IF OBJECT_ID('dbo.TB_M_DOCUMENT_BACKUP_20261117C', 'U') IS NULL
	SELECT * INTO dbo.TB_M_DOCUMENT_BACKUP_20261117C FROM dbo.TB_M_DOCUMENT;

DELETE FROM dbo.TB_M_EXCEL_TEMPLATE WHERE DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @IDS);
DELETE FROM dbo.TB_M_DOCUMENT WHERE DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @IDS);
PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' categories deleted';

COMMIT;
