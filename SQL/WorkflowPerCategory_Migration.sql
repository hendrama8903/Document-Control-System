-- =====================================================================
-- Workflow approval per Document Category + cakupan approver per jabatan
-- (request Hendra 2026-11-06)
--
-- Sebelumnya:
--  * Alur approval hanya dipilih dari (DOCUMENT_LEVEL x CREATOR_LEVEL), jadi
--    semua jenis dokumen di level yang sama wajib berbagi alur yang sama.
--  * Cara mencari approver untuk tiap jabatan ditulis langsung di
--    sp_WorkflowDoc_Create (1,2 = section; 3 = department; 4,5 = division;
--    selain itu = TOP 1 di seluruh perusahaan) - jabatan baru (Supervisor,
--    Specialist, dst.) otomatis jatuh ke "seluruh perusahaan".
--  * Langkah yang approvernya tidak ditemukan selalu dilewati diam-diam.
--
-- Perubahan:
--  1. TB_M_POSITION.APPROVER_SCOPE   - SECTION / DEPARTMENT / DIVISION / COMPANY
--  2. TB_M_WORKFLOW_DOC_H.DOCUMENT_ID - NULL = alur default per level,
--                                       terisi = alur khusus kategori itu
--  3. TB_M_WORKFLOW_DOC_D.IS_MANDATORY - 1 = approver wajib ada; kalau tidak
--                                       ditemukan dokumen tidak bisa dibuat
--  4. Data jabatan: Supervisor & Coordinator = level 2 (setara Section Head,
--     cakupan section), Specialist = level 3 (setara Dept. Head, cakupan
--     department). Supervisor/Specialist sebelumnya ter-soft-delete (data uji).
--
-- CREATOR_LEVEL di TB_M_WORKFLOW_DOC_H = POSITION_LEVEL pembuat (bukan
-- POSITION_ID) - begitulah sp_WorkflowDoc_Create mencocokkannya sejak awal.
--
-- Idempotent - aman dijalankan ulang. Jalankan di database DMS_NEW, lalu
-- jalankan file-file SQL/StoredProcedures yang ikut berubah (lihat daftar di
-- akhir file ini).
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- 1. Cakupan approver per jabatan ------------------------------------------
IF COL_LENGTH('dbo.TB_M_POSITION', 'APPROVER_SCOPE') IS NULL
BEGIN
	ALTER TABLE dbo.TB_M_POSITION ADD APPROVER_SCOPE VARCHAR(20) NOT NULL
		CONSTRAINT DF_TB_M_POSITION_APPROVER_SCOPE DEFAULT ('COMPANY');
END
GO
IF OBJECT_ID('dbo.CK_TB_M_POSITION_APPROVER_SCOPE', 'C') IS NULL
	ALTER TABLE dbo.TB_M_POSITION ADD CONSTRAINT CK_TB_M_POSITION_APPROVER_SCOPE
		CHECK (APPROVER_SCOPE IN ('SECTION', 'DEPARTMENT', 'DIVISION', 'COMPANY'));
GO

-- Nilai awal = perilaku lama sp_WorkflowDoc_Create
UPDATE dbo.TB_M_POSITION SET APPROVER_SCOPE = 'SECTION'    WHERE POSITION_ID IN (1, 2);
UPDATE dbo.TB_M_POSITION SET APPROVER_SCOPE = 'DEPARTMENT' WHERE POSITION_ID = 3;
UPDATE dbo.TB_M_POSITION SET APPROVER_SCOPE = 'DIVISION'   WHERE POSITION_ID IN (4, 5);
UPDATE dbo.TB_M_POSITION SET APPROVER_SCOPE = 'COMPANY'    WHERE POSITION_ID IN (6, 7);

-- Supervisor & Coordinator setara Section Head; Specialist setara Dept. Head
UPDATE dbo.TB_M_POSITION
SET POSITION_LEVEL = 2, APPROVER_SCOPE = 'SECTION', DELETE_FLAG = 0,
	CHANGED_BY = 'migration', CHANGED_DT = GETDATE()
WHERE POSITION_NAME IN ('Supervisor', 'Coordinator');

UPDATE dbo.TB_M_POSITION
SET POSITION_LEVEL = 3, APPROVER_SCOPE = 'DEPARTMENT', DELETE_FLAG = 0,
	CHANGED_BY = 'migration', CHANGED_DT = GETDATE()
WHERE POSITION_NAME = 'Specialist';
GO

-- 2. Alur khusus per Document Category --------------------------------------
IF COL_LENGTH('dbo.TB_M_WORKFLOW_DOC_H', 'DOCUMENT_ID') IS NULL
	ALTER TABLE dbo.TB_M_WORKFLOW_DOC_H ADD DOCUMENT_ID INT NULL;
GO

-- 3. Langkah wajib -------------------------------------------------------------
IF COL_LENGTH('dbo.TB_M_WORKFLOW_DOC_D', 'IS_MANDATORY') IS NULL
	ALTER TABLE dbo.TB_M_WORKFLOW_DOC_D ADD IS_MANDATORY BIT NOT NULL
		CONSTRAINT DF_TB_M_WORKFLOW_DOC_D_IS_MANDATORY DEFAULT (0);
GO

-- 4. Fungsi bersama: dipakai sp_WorkflowDoc_Create (membuat approval) DAN
--    sp_WorkflowDoc_Preview (pratinjau di Document Preparation), supaya
--    pratinjau selalu sama persis dengan hasil sebenarnya.
-- ---------------------------------------------------------------------------

-- Jabatan & organisasi pembuat dokumen
CREATE OR ALTER FUNCTION dbo.fn_WorkflowDoc_Creator (@DOCUMENT_CREATOR VARCHAR(50))
RETURNS TABLE
AS
RETURN
	SELECT TOP 1
		B.USERNAME,
		A.POSITION_ID,
		A.POSITION_NAME,
		A.POSITION_LEVEL,
		B.DIVISION,
		B.DEPARTMENT_ID,
		B.SECTION_ID
	FROM dbo.TB_M_POSITION A
	JOIN dbo.TB_M_USER_POS B ON B.POSITION_ID = A.POSITION_ID
	WHERE B.USERNAME = @DOCUMENT_CREATOR
	ORDER BY A.POSITION_LEVEL DESC, A.POSITION_ID;
GO

-- Header alur yang berlaku: alur khusus kategori dulu, kalau tidak ada pakai
-- alur default level.
CREATE OR ALTER FUNCTION dbo.fn_WorkflowDoc_FindHeader (@DOCUMENT_LEVEL INT, @DOCUMENT_ID INT, @CREATOR_LEVEL INT)
RETURNS TABLE
AS
RETURN
	SELECT TOP 1
		H.WORKFLOW_DOC_ID,
		CAST(CASE WHEN H.DOCUMENT_ID IS NULL THEN 0 ELSE 1 END AS BIT) AS IS_CATEGORY_WORKFLOW
	FROM dbo.TB_M_WORKFLOW_DOC_H H
	WHERE H.CREATOR_LEVEL = @CREATOR_LEVEL
	AND (
		(@DOCUMENT_ID IS NOT NULL AND H.DOCUMENT_ID = @DOCUMENT_ID)
		OR (H.DOCUMENT_ID IS NULL AND H.DOCUMENT_LEVEL = @DOCUMENT_LEVEL)
	)
	ORDER BY CASE WHEN H.DOCUMENT_ID IS NULL THEN 1 ELSE 0 END, H.WORKFLOW_DOC_ID;
GO

-- Langkah-langkah alur beserta approver hasil pencarian sesuai cakupan
-- jabatan. APPROVER NULL = tidak ada user dengan jabatan itu di cakupannya.
CREATE OR ALTER FUNCTION dbo.fn_WorkflowDoc_Steps (@WORKFLOW_DOC_ID INT, @DOCUMENT_CREATOR VARCHAR(50))
RETURNS TABLE
AS
RETURN
	SELECT
		D.WORKFLOW_SEQ,
		D.POSITION_ID,
		P.POSITION_NAME,
		P.APPROVER_SCOPE,
		D.LABEL,
		D.IS_MANDATORY,
		A.USERNAME AS APPROVER
	FROM dbo.TB_M_WORKFLOW_DOC_D D
	LEFT JOIN dbo.TB_M_POSITION P ON P.POSITION_ID = D.POSITION_ID
	OUTER APPLY dbo.fn_WorkflowDoc_Creator(@DOCUMENT_CREATOR) C
	OUTER APPLY (
		SELECT TOP 1 X.USERNAME
		FROM (
			-- Jabatan langkah ini = jabatan pembuat: pembuat sendiri
			SELECT C.USERNAME, 0 AS ORD
			WHERE D.POSITION_ID = C.POSITION_ID
			UNION ALL
			SELECT UP.USERNAME, 1 AS ORD
			FROM dbo.TB_M_USER_POS UP
			WHERE UP.POSITION_ID = D.POSITION_ID
			AND (
				P.APPROVER_SCOPE = 'COMPANY'
				OR (P.APPROVER_SCOPE = 'DIVISION'   AND UP.DIVISION = C.DIVISION)
				OR (P.APPROVER_SCOPE = 'DEPARTMENT' AND UP.DIVISION = C.DIVISION AND UP.DEPARTMENT_ID = C.DEPARTMENT_ID)
				OR (P.APPROVER_SCOPE = 'SECTION'    AND UP.DIVISION = C.DIVISION AND UP.DEPARTMENT_ID = C.DEPARTMENT_ID AND UP.SECTION_ID = C.SECTION_ID)
			)
		) X
		ORDER BY X.ORD, X.USERNAME
	) A
	WHERE D.WORKFLOW_DOC_ID = @WORKFLOW_DOC_ID;
GO

-- Stored procedure yang ikut berubah (jalankan setelah file ini):
--   SQL/StoredProcedures/sp_WorkflowDoc_Create.sql
--   SQL/StoredProcedures/sp_WorkflowDoc_Preview.sql            (baru)
--   SQL/StoredProcedures/sp_WorkflowDoc_CategoryPositions.sql  (baru)
--   SQL/StoredProcedures/sp_Workflow_DocSearch.sql
--   SQL/StoredProcedures/sp_Workflow_DocGetByCode.sql
--   SQL/StoredProcedures/sp_Workflow_DocInsertHeader.sql
--   SQL/StoredProcedures/sp_Workflow_DocInsertDetail.sql
--   SQL/StoredProcedures/sp_PositionMaster_Search.sql
--   SQL/StoredProcedures/sp_PositionMaster_GetByKey.sql
--   SQL/StoredProcedures/sp_PositionMaster_Insert.sql
--   SQL/StoredProcedures/sp_PositionMaster_Update.sql
--   SQL/StoredProcedures/sp_DocumentMaintenance_Insert.sql
--   SQL/StoredProcedures/sp_DocumentMaintenance_Update.sql
