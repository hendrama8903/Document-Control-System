-- =====================================================================
-- DIBATALKAN (2026-08-11) - JANGAN JALANKAN LAGI TANPA KONFIRMASI.
-- Copy Request awalnya dibuat pakai system type SENDIRI (8 kategori persis
-- checkbox form fisik), tapi Hendra putuskan reuse DOC_SUBMISSION_CATEGORY
-- (5 kategori, sama dengan Document Submission) supaya konsisten. Seed di
-- bawah ini sudah DIHAPUS lagi dari TB_M_SYSTEM. Dibiarkan di repo cuma
-- untuk riwayat, lihat [[dms-copyrequest-approval-simplified]].
--
-- Copy Request: kategori dokumen ("Jenis Dokumen") pakai system type
-- SENDIRI, bukan reuse DOC_SUBMISSION_CATEGORY - form fisik QCD/FR-QMS-00/028
-- punya 8 checkbox persis (Prosedur/EIS/SOE/OPL/SOP/IK/ACUAN/Lain Lain),
-- beda total dari 5 kategori gabungan punya Document Submission. Kode di
-- sini dipetakan 1:1 ke cell checkbox template Excel
-- (lihat CopyRequestController.CategoryCheckboxCellMap) - JANGAN ubah
-- SYSTEM_CODE tanpa ikut update cell map itu.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '1')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '1', 'Prosedur', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '2')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '2', 'EIS', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '3')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '3', 'SOE', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '4')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '4', 'OPL', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '5')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '5', 'SOP', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '6')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '6', 'IK', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '7')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '7', 'ACUAN', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_DOC_CATEGORY' AND SYSTEM_CODE = '8')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_DOC_CATEGORY', '8', 'Lain Lain', 1, 'dms.admin', GETDATE());
GO
