-- =====================================================================
-- Stamp Layout (request Hendra 2026-11-11, tahap 1).
--
-- Posisi dan ukuran stempel MASTER / CONTROLLED COPY / UNCONTROLLED COPY / OBSOLETE
-- di halaman PDF, diatur di Master Data > Stamp Layout:
--   DOCUMENT_ID NULL = default global
--   DOCUMENT_ID diisi = default khusus Document Category (TB_M_DOCUMENT); kalau
--                       kategori tidak punya baris, dipakai default global.
-- Masing-masing untuk halaman Portrait (P) dan Landscape (L); sistem memilih per halaman.
--
-- POS_X / POS_Y = pojok kiri-atas stempel dalam persen lebar / tinggi halaman.
-- WIDTH_PCT     = lebar stempel dalam persen lebar halaman (tinggi mengikuti gambar).
-- Catatan di bawah CONTROLLED/UNCONTROLLED (nomor salinan dsb.) ikut di bawah stempel.
--
-- Default global = posisi lama (MASTER kiri-atas, lainnya tengah-bawah, lebar 180 pt di A4),
-- jadi tampilan dokumen tidak berubah sampai ada yang mengubah layout.
--
-- Aman dijalankan ulang. Jalankan di database DMS_NEW.
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.TB_M_STAMP_LAYOUT', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_M_STAMP_LAYOUT (
		LAYOUT_ID   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_M_STAMP_LAYOUT PRIMARY KEY,
		DOCUMENT_ID INT NULL,
		STAMP_TYPE  VARCHAR(20) NOT NULL,
		ORIENTATION CHAR(1) NOT NULL,
		POS_X       DECIMAL(6,3) NOT NULL,
		POS_Y       DECIMAL(6,3) NOT NULL,
		WIDTH_PCT   DECIMAL(6,3) NOT NULL,
		CREATED_BY  VARCHAR(50) NULL,
		CREATED_DT  DATETIME NULL,
		CHANGED_BY  VARCHAR(50) NULL,
		CHANGED_DT  DATETIME NULL,
		CONSTRAINT FK_TB_M_STAMP_LAYOUT_DOCUMENT FOREIGN KEY (DOCUMENT_ID) REFERENCES dbo.TB_M_DOCUMENT (DOCUMENT_ID),
		CONSTRAINT CK_TB_M_STAMP_LAYOUT_TYPE CHECK (STAMP_TYPE IN ('MASTER', 'CONTROLLED', 'UNCONTROLLED', 'OBSOLETE')),
		CONSTRAINT CK_TB_M_STAMP_LAYOUT_ORIENTATION CHECK (ORIENTATION IN ('P', 'L')),
		CONSTRAINT CK_TB_M_STAMP_LAYOUT_RANGE CHECK (POS_X BETWEEN 0 AND 100 AND POS_Y BETWEEN 0 AND 100 AND WIDTH_PCT BETWEEN 1 AND 100)
	);
	-- One row per scope + stamp + orientation (SQL Server treats NULL scopes as equal here).
	CREATE UNIQUE INDEX UX_TB_M_STAMP_LAYOUT ON dbo.TB_M_STAMP_LAYOUT (DOCUMENT_ID, STAMP_TYPE, ORIENTATION);
END;

-- Default global (A4 595 x 842 pt, stempel 180 pt, tinggi dari rasio gambar).
MERGE dbo.TB_M_STAMP_LAYOUT AS t
USING (VALUES
	('MASTER',       'P',  3.360,  2.376, 30.238),
	('MASTER',       'L',  2.376,  3.360, 21.380),
	('CONTROLLED',   'P', 34.881, 92.070, 30.238),
	('CONTROLLED',   'L', 39.310, 88.800, 21.380),
	('UNCONTROLLED', 'P', 34.881, 92.070, 30.238),
	('UNCONTROLLED', 'L', 39.310, 88.800, 21.380),
	('OBSOLETE',     'P', 34.881, 93.190, 30.238),
	('OBSOLETE',     'L', 39.310, 90.370, 21.380)
) AS s (STAMP_TYPE, ORIENTATION, POS_X, POS_Y, WIDTH_PCT)
ON t.DOCUMENT_ID IS NULL AND t.STAMP_TYPE = s.STAMP_TYPE AND t.ORIENTATION = s.ORIENTATION
WHEN NOT MATCHED THEN
	INSERT (DOCUMENT_ID, STAMP_TYPE, ORIENTATION, POS_X, POS_Y, WIDTH_PCT, CREATED_BY, CREATED_DT)
	VALUES (NULL, s.STAMP_TYPE, s.ORIENTATION, s.POS_X, s.POS_Y, s.WIDTH_PCT, 'dms.admin', GETDATE());

-- Menu Master Data > Stamp Layout + fungsi Edit (DMS-ADMIN; role lain lewat Role Authorization).
IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00002-11')
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00002-11', 'M00002', 'Stamp Layout', 'fa-certificate', '/StampLayout/Index', 9, 'dms.admin', GETDATE(), 0);

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_MENU WHERE ROLE_ID = 'DMS-ADMIN' AND MENU_ID = 'M00002-11')
	INSERT INTO dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'M00002-11', 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION WHERE FUNCTION_ID = 'STAMPLAYOUT-EDIT')
	INSERT INTO dbo.TB_M_FUNCTION (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('STAMPLAYOUT-EDIT', 'M00002-11', 'Edit', 'Stamp Layout Change Stamp Position Function', 'dms.admin', GETDATE(), 0);

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_FUNCTION WHERE ROLE_ID = 'DMS-ADMIN' AND FUNCTION_ID = 'STAMPLAYOUT-EDIT')
	INSERT INTO dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'STAMPLAYOUT-EDIT', 'dms.admin', GETDATE());

COMMIT TRANSACTION;
GO
