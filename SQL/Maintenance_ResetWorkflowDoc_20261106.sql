-- =====================================================================
-- Maintenance: kosongkan setting alur approval dokumen Level 1-4 supaya
-- bisa diinput ulang lewat menu Approval Workflow Setup dengan metode baru
-- (alur per Document Category, Creator Level, langkah Mandatory) -
-- request Hendra 2026-11-06.
--
-- Alur DOCUMENT_LEVEL = 9 SENGAJA TIDAK dihapus: dipakai Document
-- Submission Form (sp_DocumentSubmission_Submit memanggil sp_WorkflowDoc_Create
-- dengan @DOCUMENT_LEVEL = 9), dan level 9 tidak bisa dibuat ulang lewat menu
-- (dropdown Document Level hanya berisi LEVEL_CODE 1-4).
--
-- Backup alur lama: BAK_TB_M_WORKFLOW_DOC_H_20261106 / _D_20261106 (dibuat oleh
-- SQL/WorkflowPerCategory_Migration.sql sebelum migrasi) - dibuat di sini juga
-- kalau belum ada.
--
-- Selama alur belum diinput ulang, dokumen Level 1-4 tidak bisa dibuat
-- ("Workflow for Document level ... not found").
-- Jalankan di database DMS_NEW
-- =====================================================================
SET NOCOUNT ON;

IF OBJECT_ID('dbo.BAK_TB_M_WORKFLOW_DOC_H_20261106') IS NULL
	SELECT * INTO dbo.BAK_TB_M_WORKFLOW_DOC_H_20261106 FROM dbo.TB_M_WORKFLOW_DOC_H;
IF OBJECT_ID('dbo.BAK_TB_M_WORKFLOW_DOC_D_20261106') IS NULL
	SELECT * INTO dbo.BAK_TB_M_WORKFLOW_DOC_D_20261106 FROM dbo.TB_M_WORKFLOW_DOC_D;

BEGIN TRANSACTION;

DELETE D
FROM dbo.TB_M_WORKFLOW_DOC_D D
JOIN dbo.TB_M_WORKFLOW_DOC_H H ON H.WORKFLOW_DOC_ID = D.WORKFLOW_DOC_ID
WHERE H.DOCUMENT_LEVEL IN (1, 2, 3, 4);

DELETE FROM dbo.TB_M_WORKFLOW_DOC_H
WHERE DOCUMENT_LEVEL IN (1, 2, 3, 4);

COMMIT TRANSACTION;

SELECT H.DOCUMENT_LEVEL, H.CREATOR_LEVEL, H.DOCUMENT_ID, COUNT(D.WORKFLOW_DOC_D_ID) AS STEPS
FROM dbo.TB_M_WORKFLOW_DOC_H H
LEFT JOIN dbo.TB_M_WORKFLOW_DOC_D D ON D.WORKFLOW_DOC_ID = H.WORKFLOW_DOC_ID
GROUP BY H.DOCUMENT_LEVEL, H.CREATOR_LEVEL, H.DOCUMENT_ID
ORDER BY 1, 2;
