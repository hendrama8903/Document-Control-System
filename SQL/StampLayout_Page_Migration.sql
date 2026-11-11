-- =====================================================================
-- Stamp Layout tahap 4 (request Hendra 2026-11-11): posisi stempel per halaman.
--
-- TB_R_DOCUMENT_STAMP_LAYOUT.PAGE_NO
--   NULL  = posisi untuk semua halaman (Portrait / Landscape), seperti sebelumnya
--   angka = posisi khusus halaman itu saja (1 = halaman pertama)
-- Posisi halaman TIDAK diwariskan ke revisi baru (jumlah/isi halaman biasanya berubah);
-- sp_DocumentMaintenance_SupersedeRevision hanya menyalin baris PAGE_NO IS NULL
-- (pasang ulang SQL/StoredProcedures/sp_DocumentMaintenance_SupersedeRevision.sql).
--
-- Unique index diganti supaya PAGE_NO ikut kunci (NULL dianggap sama oleh unique index
-- SQL Server, jadi baris "semua halaman" tetap satu per stempel dan orientasi).
-- Aman dijalankan ulang. Jalankan di DMS_NEW setelah SQL/StampLayout_Lock_Migration.sql.
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.TB_R_DOCUMENT_STAMP_LAYOUT', 'PAGE_NO') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT_STAMP_LAYOUT ADD PAGE_NO INT NULL
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_PAGE CHECK (PAGE_NO IS NULL OR PAGE_NO BETWEEN 1 AND 9999);

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TB_R_DOCUMENT_STAMP_LAYOUT' AND object_id = OBJECT_ID('dbo.TB_R_DOCUMENT_STAMP_LAYOUT'))
	DROP INDEX UX_TB_R_DOCUMENT_STAMP_LAYOUT ON dbo.TB_R_DOCUMENT_STAMP_LAYOUT;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TB_R_DOCUMENT_STAMP_LAYOUT_PAGE' AND object_id = OBJECT_ID('dbo.TB_R_DOCUMENT_STAMP_LAYOUT'))
	EXEC('CREATE UNIQUE INDEX UX_TB_R_DOCUMENT_STAMP_LAYOUT_PAGE
		ON dbo.TB_R_DOCUMENT_STAMP_LAYOUT (DOCUMENT_TRANSACTION_ID, REVISION, STAMP_TYPE, ORIENTATION, PAGE_NO)');

COMMIT TRANSACTION;
GO
