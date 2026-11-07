-- =====================================================================
-- Status aktif/nonaktif alur approval (Approval Workflow Setup, Tahap 1).
-- Request Hendra 2026-11-07.
--
--  * TB_M_WORKFLOW_DOC_H.IS_ACTIVE: alur nonaktif tidak dipakai dokumen baru,
--    tetapi datanya tetap ada dan bisa diaktifkan lagi. Semua alur yang sudah
--    ada menjadi aktif, jadi perilaku tidak berubah setelah migrasi.
--  * Satu kombinasi (level, level pembuat, kategori) hanya boleh punya satu alur
--    aktif - dijaga indeks unik, jadi aman walau ada simpan bersamaan.
--  * fn_WorkflowDoc_FindHeader hanya memilih alur aktif. Dipakai
--    sp_WorkflowDoc_Create dan sp_WorkflowDoc_Preview.
--
-- Urutan deploy: skrip ini -> SP -> aplikasi baru.
-- Aman dijalankan ulang. Jalankan di database DMS_NEW.
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.TB_M_WORKFLOW_DOC_H', 'IS_ACTIVE') IS NULL
	ALTER TABLE dbo.TB_M_WORKFLOW_DOC_H
		ADD IS_ACTIVE BIT NOT NULL CONSTRAINT DF_TB_M_WORKFLOW_DOC_H_IS_ACTIVE DEFAULT (1);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TB_M_WORKFLOW_DOC_H_ACTIVE' AND object_id = OBJECT_ID('dbo.TB_M_WORKFLOW_DOC_H'))
	CREATE UNIQUE INDEX UX_TB_M_WORKFLOW_DOC_H_ACTIVE
		ON dbo.TB_M_WORKFLOW_DOC_H (DOCUMENT_LEVEL, CREATOR_LEVEL, DOCUMENT_ID)
		WHERE IS_ACTIVE = 1;
GO

-- Header alur yang berlaku: alur khusus kategori dulu, kalau tidak ada pakai
-- alur default level. Hanya alur aktif.
CREATE OR ALTER FUNCTION dbo.fn_WorkflowDoc_FindHeader (@DOCUMENT_LEVEL INT, @DOCUMENT_ID INT, @CREATOR_LEVEL INT)
RETURNS TABLE
AS
RETURN
	SELECT TOP 1
		H.WORKFLOW_DOC_ID,
		CAST(CASE WHEN H.DOCUMENT_ID IS NULL THEN 0 ELSE 1 END AS BIT) AS IS_CATEGORY_WORKFLOW
	FROM dbo.TB_M_WORKFLOW_DOC_H H
	WHERE H.CREATOR_LEVEL = @CREATOR_LEVEL
	AND H.IS_ACTIVE = 1
	AND (
		(@DOCUMENT_ID IS NOT NULL AND H.DOCUMENT_ID = @DOCUMENT_ID)
		OR (H.DOCUMENT_ID IS NULL AND H.DOCUMENT_LEVEL = @DOCUMENT_LEVEL)
	)
	ORDER BY CASE WHEN H.DOCUMENT_ID IS NULL THEN 1 ELSE 0 END, H.WORKFLOW_DOC_ID;
GO
