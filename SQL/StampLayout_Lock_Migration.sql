-- =====================================================================
-- Stamp Layout tahap 3 (request Hendra 2026-11-11): kunci, log, warisan revisi.
--
-- * TB_R_DOCUMENT_STAMP_LAYOUT.SOURCE: dari mana posisi dokumen berasal
--     CATEGORY = salinan layout kategori/global saat Accept (dibekukan)
--     CUSTOM   = diatur khusus untuk dokumen ini
--   Sejak tahap 3 setiap Accept menyimpan posisi ke dokumen, jadi perubahan layout master
--   tidak lagi menggeser stempel dokumen yang sudah di-Accept.
-- * TB_R_DOCUMENT_STAMP_LAYOUT_LOG: riwayat ACCEPT / CHANGE (wajib alasan) / INHERIT
--   (disalin dari revisi sebelumnya), dengan posisi lengkap dalam JSON.
-- * sp_DocumentMaintenance_SupersedeRevision menyalin posisi revisi lama ke revisi baru
--   (pasang ulang SQL/StoredProcedures/sp_DocumentMaintenance_SupersedeRevision.sql).
--
-- Tabel transaksi: ikut dikosongkan di skrip Maintenance_ResetTransactionData berikutnya.
-- Aman dijalankan ulang. Jalankan di DMS_NEW setelah SQL/StampLayout_Document_Migration.sql.
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.TB_R_DOCUMENT_STAMP_LAYOUT', 'SOURCE') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT_STAMP_LAYOUT ADD SOURCE VARCHAR(20) NOT NULL
		CONSTRAINT DF_TB_R_DOCUMENT_STAMP_LAYOUT_SOURCE DEFAULT ('CUSTOM')
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_SOURCE CHECK (SOURCE IN ('CATEGORY', 'CUSTOM'));

IF OBJECT_ID('dbo.TB_R_DOCUMENT_STAMP_LAYOUT_LOG', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_DOCUMENT_STAMP_LAYOUT_LOG (
		LOG_ID                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_DOCUMENT_STAMP_LAYOUT_LOG PRIMARY KEY,
		DOCUMENT_TRANSACTION_ID INT NOT NULL,
		REVISION                INT NOT NULL,
		ACTION                  VARCHAR(20) NOT NULL,
		SOURCE                  VARCHAR(20) NOT NULL,
		POSITIONS               NVARCHAR(MAX) NULL,
		REASON                  NVARCHAR(500) NULL,
		CREATED_BY              VARCHAR(50) NULL,
		CREATED_DT              DATETIME NOT NULL CONSTRAINT DF_TB_R_DOCUMENT_STAMP_LAYOUT_LOG_DT DEFAULT (GETDATE()),
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_LOG_ACTION CHECK (ACTION IN ('ACCEPT', 'CHANGE', 'INHERIT')),
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_LOG_SOURCE CHECK (SOURCE IN ('CATEGORY', 'CUSTOM'))
	);
	CREATE INDEX IX_TB_R_DOCUMENT_STAMP_LAYOUT_LOG_DOC ON dbo.TB_R_DOCUMENT_STAMP_LAYOUT_LOG (DOCUMENT_TRANSACTION_ID, LOG_ID);
END;

COMMIT TRANSACTION;
GO
