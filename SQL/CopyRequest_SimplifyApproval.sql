-- =====================================================================
-- Copy Request: ganti approval dari chain internal 3-tingkat (Staff ->
-- Section Head -> Dept Head, lewat TB_M_WORKFLOW_DOC_H/D generik) jadi
-- satu langkah approve/reject langsung oleh tim QMS - keputusan Hendra
-- 2026-08-11: proses request salinan dokumen tidak perlu approval
-- internal berjenjang, cukup QMS yang approve.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

-- ---------------------------------------------------------------------
-- Bagian 1: kolom header - buang APPROVAL_ID (tidak dipakai lagi),
-- tambah kolom hasil approval satu-langkah.
-- ---------------------------------------------------------------------

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TB_R_COPY_REQUEST_H') AND name = 'APPROVAL_ID')
BEGIN
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] DROP COLUMN [APPROVAL_ID];
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TB_R_COPY_REQUEST_H') AND name = 'APPROVED_BY')
BEGIN
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] ADD [APPROVED_BY] VARCHAR(255) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TB_R_COPY_REQUEST_H') AND name = 'APPROVED_DT')
BEGIN
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] ADD [APPROVED_DT] DATETIME NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.TB_R_COPY_REQUEST_H') AND name = 'APPROVAL_REMARK')
BEGIN
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] ADD [APPROVAL_REMARK] VARCHAR(MAX) NULL;
END
GO

-- ---------------------------------------------------------------------
-- Bagian 2: bersihkan data uji coba yang sempat dibuat lewat alur chain
-- lama (1 baris, REQUEST_ID=1, masih "Waiting Approval" nyangkut di
-- workflow lama) supaya tidak nyangkut status APPROVAL_ID yang sudah
-- tidak ada kolomnya. Dikembalikan ke Draft supaya bisa di-submit ulang
-- lewat alur baru.
-- ---------------------------------------------------------------------

UPDATE [dbo].[TB_R_COPY_REQUEST_H]
SET STATUS = '0', REQUEST_NO = NULL, SUBMITTED_BY = NULL, SUBMITTED_DT = NULL
WHERE STATUS = '1';

DELETE AH_D FROM [dbo].[TB_R_APPROVAL_D] AH_D
INNER JOIN [dbo].[TB_R_APPROVAL_H] AH_H ON AH_H.APPROVAL_ID = AH_D.APPROVAL_ID
WHERE AH_H.MENU_ID = 'M00006-08';

DELETE FROM [dbo].[TB_R_APPROVAL_H] WHERE MENU_ID = 'M00006-08';

-- ---------------------------------------------------------------------
-- Bagian 3: buang seed workflow chain DOCUMENT_LEVEL=10 (dibuat khusus
-- untuk Copy Request di Menu_Add_CopyRequest.sql, tidak dipakai modul
-- lain manapun - lihat komentar di file itu) - sudah tidak dipakai.
-- ---------------------------------------------------------------------

DELETE WD_D FROM [dbo].[TB_M_WORKFLOW_DOC_D] WD_D
INNER JOIN [dbo].[TB_M_WORKFLOW_DOC_H] WD_H ON WD_H.WORKFLOW_DOC_ID = WD_D.WORKFLOW_DOC_ID
WHERE WD_H.DOCUMENT_LEVEL = 10;

DELETE FROM [dbo].[TB_M_WORKFLOW_DOC_H] WHERE DOCUMENT_LEVEL = 10;
GO
