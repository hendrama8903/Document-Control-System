-- =====================================================================
-- Stamp Layout tahap 2 (request Hendra 2026-11-11): posisi stempel khusus satu dokumen,
-- dipilih QMS saat Accept di Document Registration (P4D).
--
-- TB_R_DOCUMENT_STAMP_LAYOUT menimpa layout kategori / global (TB_M_STAMP_LAYOUT) untuk
-- satu revisi dokumen. Tanpa baris = dokumen memakai layout kategorinya.
-- Kolom sama dengan TB_M_STAMP_LAYOUT (persen halaman, per STAMP_TYPE dan ORIENTATION).
--
-- Fungsi P4D-STAMPLAYOUT (menu Document Registration): tanpa fungsi ini Accept tetap bisa,
-- tetapi tanpa pilihan posisi (dokumen memakai layout kategori).
--
-- Tabel transaksi: ikut dikosongkan di skrip Maintenance_ResetTransactionData berikutnya.
-- Aman dijalankan ulang. Jalankan di database DMS_NEW setelah SQL/StampLayout_Migration.sql.
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.TB_R_DOCUMENT_STAMP_LAYOUT', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_DOCUMENT_STAMP_LAYOUT (
		LAYOUT_ID               INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_DOCUMENT_STAMP_LAYOUT PRIMARY KEY,
		DOCUMENT_TRANSACTION_ID INT NOT NULL,
		REVISION                INT NOT NULL,
		STAMP_TYPE              VARCHAR(20) NOT NULL,
		ORIENTATION             CHAR(1) NOT NULL,
		POS_X                   DECIMAL(6,3) NOT NULL,
		POS_Y                   DECIMAL(6,3) NOT NULL,
		WIDTH_PCT               DECIMAL(6,3) NOT NULL,
		CREATED_BY              VARCHAR(50) NULL,
		CREATED_DT              DATETIME NULL,
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_TYPE CHECK (STAMP_TYPE IN ('MASTER', 'CONTROLLED', 'UNCONTROLLED', 'OBSOLETE')),
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_ORIENTATION CHECK (ORIENTATION IN ('P', 'L')),
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_RANGE CHECK (POS_X BETWEEN 0 AND 100 AND POS_Y BETWEEN 0 AND 100 AND WIDTH_PCT BETWEEN 1 AND 100)
	);
	CREATE UNIQUE INDEX UX_TB_R_DOCUMENT_STAMP_LAYOUT
		ON dbo.TB_R_DOCUMENT_STAMP_LAYOUT (DOCUMENT_TRANSACTION_ID, REVISION, STAMP_TYPE, ORIENTATION);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION WHERE FUNCTION_ID = 'P4D-STAMPLAYOUT')
	INSERT INTO dbo.TB_M_FUNCTION (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('P4D-STAMPLAYOUT', 'M00006-02', 'Adjust Stamp Position', 'Document Registration Adjust Stamp Position On Accept Function', 'dms.admin', GETDATE(), 0);

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_FUNCTION WHERE ROLE_ID = 'DMS-ADMIN' AND FUNCTION_ID = 'P4D-STAMPLAYOUT')
	INSERT INTO dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'P4D-STAMPLAYOUT', 'dms.admin', GETDATE());

COMMIT TRANSACTION;
GO
