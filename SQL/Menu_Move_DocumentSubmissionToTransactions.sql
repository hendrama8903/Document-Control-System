-- =====================================================================
-- "Document Submission Form" (M00006-09) was originally seeded under
-- Transactions (M00006) by Menu_Add_DocumentSubmissionForm.sql, but at
-- some point got moved to Reports (M00008) via the Menu Maintenance UI
-- (no migration script recorded that move - confirmed by checking
-- TB_M_MENU directly, 2026-08-12). Functionally it's a transaction
-- (create/submit/approve a document submission), not a report, so move
-- it back under Transactions, appended after the existing items there.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

UPDATE [dbo].[TB_M_MENU]
SET PARENT_ID = 'M00006',
    MENU_SEQ = 7,
    CHANGED_BY = 'dms.admin',
    CHANGED_DT = GETDATE()
WHERE MENU_ID = 'M00006-09'
AND (PARENT_ID <> 'M00006' OR MENU_SEQ <> 7);
GO
