-- =====================================================================
-- Hak lihat dokumen (request Hendra 2026-11-10).
--
-- Satu aturan untuk pencarian (sp_DocumentSearch), ViewAttachment / Download, dan akses
-- langsung ke file di /Upload (middleware DocumentFileGuard). Seorang user boleh melihat
-- dokumen internal kalau salah satu terpenuhi:
--   1. Role DMS-ADMIN, atau department di divisinya punya DOCUMENT_CONTROL_ACCESS (QMS)
--   2. Pembuat / approver dokumen itu, atau divisinya termasuk divisi "Mengetahui"
--   3. Didistribusikan (STATUS = 1) ke department-nya atau department di divisinya
--   4. Satu divisi dengan dokumen (sama seperti Document Preparation)
-- (Butir 5 lama - punya menu Document Control Dashboard / Masterlist - dihapus 2026-11-14:
--  menu daftar induk tidak lagi memberi hak membuka isi dokumen.)
-- Dokumen Rahasia (CLASSIFIED = 2) hanya lewat butir 1-3.
-- Dokumen eksternal: role DMS-ADMIN atau yang punya menu External Document.
--
-- Revisi lama yang sudah digantikan dibaca dari TB_R_DOCUMENT_HISTORY.
-- Aman dijalankan ulang. Jalankan di database DMS_NEW.
-- =====================================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER FUNCTION [dbo].[fn_User_HasMenu] (@USERNAME VARCHAR(50), @MENU_URL VARCHAR(255))
RETURNS BIT
AS
BEGIN
	RETURN CASE WHEN EXISTS (
		SELECT 1
		FROM dbo.TB_M_USER U
		JOIN dbo.TB_M_AUTH_MENU A ON A.ROLE_ID = U.ROLE_ID
		JOIN dbo.TB_M_MENU M ON M.MENU_ID = A.MENU_ID AND ISNULL(M.DELETE_FLAG, 0) = 0
		WHERE U.USERNAME = @USERNAME AND LTRIM(RTRIM(M.MENU_URL)) = @MENU_URL
	) THEN 1 ELSE 0 END;
END
GO

-- DMS-ADMIN atau Document Control (QMS) - sama seperti sp_P4DMaintenance_Search.
CREATE OR ALTER FUNCTION [dbo].[fn_User_IsDocumentControl] (@USERNAME VARCHAR(50))
RETURNS BIT
AS
BEGIN
	-- 2026-11-17: Document Control (QMS secretariat) is a ROLE, not the department flag
	-- TB_M_DEPARTMENT.DOCUMENT_CONTROL_ACCESS matched per division (that gave the whole
	-- division, e.g. every QAD user, access to all documents incl. confidential ones).
	IF EXISTS (SELECT 1 FROM dbo.TB_M_USER WHERE USERNAME = @USERNAME AND ROLE_ID IN ('DMS-ADMIN', 'DOC-CONTROL')) RETURN 1;
	RETURN 0;
END
GO

CREATE OR ALTER FUNCTION [dbo].[fn_Document_CanView] (@USERNAME VARCHAR(50), @DOCUMENT_TRANSACTION_ID INT)
RETURNS BIT
AS
BEGIN
	IF @USERNAME IS NULL OR @DOCUMENT_TRANSACTION_ID IS NULL RETURN 0;
	IF dbo.fn_User_IsDocumentControl(@USERNAME) = 1 RETURN 1;

	DECLARE @DIVISION VARCHAR(50), @CLASSIFIED VARCHAR(10), @STATUS VARCHAR(10), @DELETED INT,
	        @CREATOR VARCHAR(255), @CREATED_BY VARCHAR(255), @APPROVAL_ID INT, @FOUND BIT = 0;

	SELECT @DIVISION = DIVISION, @CLASSIFIED = CLASSIFIED, @STATUS = [STATUS], @DELETED = ISNULL(DELETE_FLAG, 0),
	       @CREATOR = DOCUMENT_CREATOR, @CREATED_BY = CREATED_BY, @APPROVAL_ID = APPROVAL_ID, @FOUND = 1
	FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID;

	IF @FOUND = 0
		SELECT TOP 1 @DIVISION = DIVISION, @CLASSIFIED = CLASSIFIED, @STATUS = [STATUS], @DELETED = 0,
		       @CREATOR = DOCUMENT_CREATOR, @CREATED_BY = CREATED_BY, @APPROVAL_ID = APPROVAL_ID, @FOUND = 1
		FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
		ORDER BY DOCUMENT_HISTORY_ID DESC;

	IF @FOUND = 0 RETURN 0;

	-- 2. pembuat, approver, divisi "Mengetahui"
	IF @CREATOR = @USERNAME OR @CREATED_BY = @USERNAME RETURN 1;
	IF EXISTS (SELECT 1 FROM dbo.TB_R_APPROVAL_D WHERE APPROVAL_ID = @APPROVAL_ID AND APPROVER = @USERNAME) RETURN 1;
	IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_RELATED_DIVISION R
	           WHERE R.DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
	             AND R.DIVISION_CODE IN (SELECT DIVISION FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME)) RETURN 1;

	-- 3. didistribusikan ke department / divisi user
	IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_DISTRIBUTION DD
	           WHERE DD.DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND DD.STATUS = 1
	             AND (DD.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME)
	                  OR DD.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM dbo.TB_M_DEPARTMENT
	                                          WHERE DIVISION IN (SELECT DIVISION FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME)))) RETURN 1;

	-- Rahasia berhenti di sini
	IF @CLASSIFIED = '2' RETURN 0;

	-- 4. satu divisi (Document Preparation)
	IF @DIVISION IN (SELECT DIVISION FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME) RETURN 1;

	-- 2026-11-14: butir 5 lama (punya menu Document Control Dashboard / Masterlist = boleh lihat
	-- semua dokumen sah) dihapus. Menu itu hanya menampilkan register; isi dokumen tetap
	-- mengikuti butir 1-4. Kasus: user FAD dengan menu Masterlist bisa membuka dokumen ITD
	-- yang tidak didistribusikan ke FAD lewat pencarian isi dokumen.
	RETURN 0;
END
GO

CREATE OR ALTER FUNCTION [dbo].[fn_ExternalDocument_CanView] (@USERNAME VARCHAR(50))
RETURNS BIT
AS
BEGIN
	IF EXISTS (SELECT 1 FROM dbo.TB_M_USER WHERE USERNAME = @USERNAME AND ROLE_ID = 'DMS-ADMIN') RETURN 1;
	RETURN dbo.fn_User_HasMenu(@USERNAME, '/ExternalDocument/Index');
END
GO

-- Hak lihat sebuah file upload, dicari dari path-nya. Dipakai endpoint yang menerima path
-- dari browser dan middleware /Upload. KIND: DOCUMENT, EXTERNAL, UNKNOWN.
--   @FILE_PATH  path relatif, mis. /Upload/ATTACHMENT/DOCUMENT_TRANSACTION/x.xlsx
--   @BASE_NAME  nama file tanpa ekstensi, untuk PDF cache DOCUMENT_TEMP/TEMP_<nama>.pdf
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentFile_CanView]
	@USERNAME   VARCHAR(50),
	@FILE_PATH  VARCHAR(500) = NULL,
	@BASE_NAME  VARCHAR(255) = NULL
AS
BEGIN
	SET NOCOUNT ON;
	DECLARE @IDS TABLE (ID INT);

	IF @FILE_PATH IS NOT NULL
	BEGIN
		INSERT INTO @IDS SELECT DOCUMENT_TRANSACTION_ID FROM dbo.TB_R_DOCUMENT WHERE FILE_PATH = @FILE_PATH;
		INSERT INTO @IDS SELECT DOCUMENT_TRANSACTION_ID FROM dbo.TB_R_DOCUMENT_HISTORY WHERE FILE_PATH = @FILE_PATH;
	END
	ELSE IF @BASE_NAME IS NOT NULL
	BEGIN
		-- nama file dokumen dibuat sistem (judul + waktu), jadi unik per upload
		INSERT INTO @IDS SELECT DOCUMENT_TRANSACTION_ID FROM dbo.TB_R_DOCUMENT
			WHERE FILE_PATH LIKE '%/' + REPLACE(REPLACE(@BASE_NAME, '[', '[[]'), '_', '[_]') + '.%';
		INSERT INTO @IDS SELECT DOCUMENT_TRANSACTION_ID FROM dbo.TB_R_DOCUMENT_HISTORY
			WHERE FILE_PATH LIKE '%/' + REPLACE(REPLACE(@BASE_NAME, '[', '[[]'), '_', '[_]') + '.%';
	END

	IF EXISTS (SELECT 1 FROM @IDS)
	BEGIN
		SELECT 'DOCUMENT' AS KIND,
		       CAST(CASE WHEN EXISTS (SELECT 1 FROM @IDS WHERE dbo.fn_Document_CanView(@USERNAME, ID) = 1) THEN 1 ELSE 0 END AS BIT) AS CAN_VIEW;
		RETURN;
	END

	IF @FILE_PATH IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.TB_M_EXTERNAL_DOCUMENT WHERE FILE_PATH = @FILE_PATH)
	BEGIN
		SELECT 'EXTERNAL' AS KIND, dbo.fn_ExternalDocument_CanView(@USERNAME) AS CAN_VIEW;
		RETURN;
	END

	SELECT 'UNKNOWN' AS KIND, dbo.fn_User_IsDocumentControl(@USERNAME) AS CAN_VIEW;
END
GO
