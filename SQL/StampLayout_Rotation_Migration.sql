-- =====================================================================
-- Stamp Layout tahap 5 (request Hendra 2026-11-12): stempel bisa diputar.
--
-- ROTATION = derajat searah jarum jam (-180 s.d. 180), diputar pada titik tengah stempel
-- (termasuk catatan di bawah CONTROLLED COPY). 0 = tegak seperti sebelumnya, jadi data lama
-- tidak berubah. Ditambahkan ke layout master (TB_M_STAMP_LAYOUT) dan ke posisi dokumen
-- (TB_R_DOCUMENT_STAMP_LAYOUT). sp_DocumentMaintenance_SupersedeRevision ikut menyalin
-- ROTATION (pasang ulang SQL/StoredProcedures/sp_DocumentMaintenance_SupersedeRevision.sql).
-- Aman dijalankan ulang. Jalankan di DMS_NEW setelah SQL/StampLayout_Page_Migration.sql.
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.TB_M_STAMP_LAYOUT', 'ROTATION') IS NULL
	ALTER TABLE dbo.TB_M_STAMP_LAYOUT ADD ROTATION DECIMAL(6,2) NOT NULL
		CONSTRAINT DF_TB_M_STAMP_LAYOUT_ROTATION DEFAULT (0)
		CONSTRAINT CK_TB_M_STAMP_LAYOUT_ROTATION CHECK (ROTATION BETWEEN -180 AND 180);

IF COL_LENGTH('dbo.TB_R_DOCUMENT_STAMP_LAYOUT', 'ROTATION') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT_STAMP_LAYOUT ADD ROTATION DECIMAL(6,2) NOT NULL
		CONSTRAINT DF_TB_R_DOCUMENT_STAMP_LAYOUT_ROTATION DEFAULT (0)
		CONSTRAINT CK_TB_R_DOCUMENT_STAMP_LAYOUT_ROTATION CHECK (ROTATION BETWEEN -180 AND 180);

COMMIT TRANSACTION;
GO
