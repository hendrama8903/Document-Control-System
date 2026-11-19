-- =====================================================================
-- Reference Access (request Hendra 2026-11-18), tahap 1
--
-- Document Control memberi seorang user hak BACA dokumen milik departemen lain
-- untuk dipelajari (mis. staf GA membaca dokumen IT). Keputusan Hendra:
--   - diatur Document Control saja (menu Administration > Reference Access)
--   - view only: tidak bisa download / print, preview diberi stempel UNCONTROLLED COPY
--   - masa berlaku WAJIB (VALID_FROM s/d VALID_TO)
--
-- Aturannya masuk ke fungsi hak lihat pusat (fn_Document_CanView), jadi Search, preview,
-- file /Upload dan link lama mengikuti; begitu dicabut atau kedaluwarsa, akses hilang di
-- semua tempat tanpa data yang perlu dibersihkan.
--
-- Bukan distribusi: tidak masuk P4D/Master List, tidak ada Acknowledge, tidak ada
-- notifikasi revisi (ISO 9001 7.5.3 - salinan tidak terkendali).
--
-- Batasan bawaan:
--   - dokumen Rahasia (CLASSIFIED = 2) tidak pernah ikut
--   - hanya revisi aktif yang Approved / Published (STATUS 1 / 5); draft, obsolete dan
--     history tidak
--   - per baris: USER pembaca -> departemen pemilik, opsional satu kategori
--     (2026-11-18 lanjutan: pembaca per orang, bukan per departemen - permintaan Hendra)
--
-- Idempotent. Jalankan di DMS_NEW, lalu StoredProcedures/sp_DocumentSearch.sql.
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- Versi pertama (pembaca per departemen, VIEWER_DEPARTMENT_ID) hanya sempat terpasang di DB
-- pengembangan dan masih kosong: dibuat ulang dengan pembaca per user. Kalau ternyata sudah
-- berisi, berhenti supaya tidak ada akses yang hilang diam-diam.
IF COL_LENGTH('dbo.TB_M_REFERENCE_ACCESS', 'VIEWER_DEPARTMENT_ID') IS NOT NULL
BEGIN
	IF EXISTS (SELECT 1 FROM dbo.TB_M_REFERENCE_ACCESS)
		THROW 50001, 'TB_M_REFERENCE_ACCESS still has department-based rows; convert them to per-user rows before running this script.', 1;
	DROP TABLE dbo.TB_M_REFERENCE_ACCESS;
	IF OBJECT_ID('dbo.TB_M_REFERENCE_ACCESS_LOG', 'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_REFERENCE_ACCESS_LOG)
		DROP TABLE dbo.TB_M_REFERENCE_ACCESS_LOG;
END
GO

IF OBJECT_ID('dbo.TB_M_REFERENCE_ACCESS', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_M_REFERENCE_ACCESS (
		REFERENCE_ACCESS_ID   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_M_REFERENCE_ACCESS PRIMARY KEY,
		VIEWER_USERNAME       VARCHAR(50)   NOT NULL,   -- user yang boleh membaca (TB_M_USER.USERNAME)
		OWNER_DEPARTMENT_ID   INT           NOT NULL,   -- departemen pemilik dokumen (TB_R_DOCUMENT.DEPARTMENT)
		DOCUMENT_ID           INT           NULL,       -- kategori (TB_M_DOCUMENT); NULL = semua kategori
		VALID_FROM            DATE          NOT NULL,
		VALID_TO              DATE          NOT NULL,
		REASON                NVARCHAR(500) NOT NULL,
		[STATUS]              TINYINT       NOT NULL CONSTRAINT DF_TB_M_REFERENCE_ACCESS_STATUS DEFAULT (1),  -- 1 aktif, 0 dicabut
		REVOKE_REASON         NVARCHAR(500) NULL,
		REVOKED_BY            VARCHAR(50)   NULL,
		REVOKED_DT            DATETIME      NULL,
		CREATED_BY            VARCHAR(50)   NOT NULL,
		CREATED_DT            DATETIME      NOT NULL CONSTRAINT DF_TB_M_REFERENCE_ACCESS_CREATED DEFAULT (GETDATE()),
		CHANGED_BY            VARCHAR(50)   NULL,
		CHANGED_DT            DATETIME      NULL,
		CONSTRAINT CK_TB_M_REFERENCE_ACCESS_DATES CHECK (VALID_TO >= VALID_FROM),
		CONSTRAINT CK_TB_M_REFERENCE_ACCESS_STATUS CHECK ([STATUS] IN (0, 1))
	);
	-- dicari per user + departemen pemilik dari fn_Document_AccessReason
	CREATE INDEX IX_TB_M_REFERENCE_ACCESS_VIEWER ON dbo.TB_M_REFERENCE_ACCESS (VIEWER_USERNAME, OWNER_DEPARTMENT_ID, [STATUS])
		INCLUDE (DOCUMENT_ID, VALID_FROM, VALID_TO);
END
GO

-- Riwayat pemberian / perubahan / pencabutan, untuk audit.
IF OBJECT_ID('dbo.TB_M_REFERENCE_ACCESS_LOG', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_M_REFERENCE_ACCESS_LOG (
		LOG_ID                BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_M_REFERENCE_ACCESS_LOG PRIMARY KEY,
		REFERENCE_ACCESS_ID   INT           NOT NULL,
		[ACTION]              VARCHAR(10)   NOT NULL,   -- CREATE / EDIT / REVOKE
		DETAIL                NVARCHAR(1000) NULL,      -- isi setelah perubahan, dalam kalimat
		ACTION_BY             VARCHAR(50)   NOT NULL,
		ACTION_DT             DATETIME      NOT NULL CONSTRAINT DF_TB_M_REFERENCE_ACCESS_LOG_DT DEFAULT (GETDATE()),
		CONSTRAINT CK_TB_M_REFERENCE_ACCESS_LOG_ACTION CHECK ([ACTION] IN ('CREATE', 'EDIT', 'REVOKE'))
	);
	CREATE INDEX IX_TB_M_REFERENCE_ACCESS_LOG_ID ON dbo.TB_M_REFERENCE_ACCESS_LOG (REFERENCE_ACCESS_ID, ACTION_DT DESC);
END
GO

-- Menu: Administration > Reference Access (setelah Access Rights Review)
IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00003-09')
BEGIN
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00003-09', 'M00003', 'Reference Access', 'fa-book', '/ReferenceAccess/Index', 5, 'dms.admin', GETDATE(), 0);
END

INSERT INTO dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, 'M00003-09', 'dms.admin', GETDATE()
FROM (VALUES ('DOC-CONTROL'), ('DMS-ADMIN')) R(ROLE_ID)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_MENU A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = 'M00003-09');

-- Fungsi
INSERT INTO dbo.TB_M_FUNCTION (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT, DELETE_FLAG)
SELECT F.FUNCTION_ID, 'M00003-09', F.FUNCTION_NAME, F.FUNCTION_DESC, 'dms.admin', GETDATE(), 0
FROM (VALUES
	('REFERENCEACCESS-ADD',    'Add',    'Reference Access Add Function'),
	('REFERENCEACCESS-EDIT',   'Edit',   'Reference Access Edit Function'),
	('REFERENCEACCESS-REVOKE', 'Revoke', 'Reference Access Revoke Function')
) F(FUNCTION_ID, FUNCTION_NAME, FUNCTION_DESC)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION X WHERE X.FUNCTION_ID = F.FUNCTION_ID);

INSERT INTO dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, F.FUNCTION_ID, 'dms.admin', GETDATE()
FROM (VALUES ('DOC-CONTROL'), ('DMS-ADMIN')) R(ROLE_ID)
CROSS JOIN (VALUES ('REFERENCEACCESS-ADD'), ('REFERENCEACCESS-EDIT'), ('REFERENCEACCESS-REVOKE')) F(FUNCTION_ID)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = R.ROLE_ID AND A.FUNCTION_ID = F.FUNCTION_ID);
GO

-- =====================================================================
-- Alasan user boleh melihat dokumen (0 = tidak boleh). Isi butir 1-4 sama persis dengan
-- fn_Document_CanView sebelumnya (RoleRedesign_2026_Migration.sql); butir 5 baru.
--   1 Document Control           2 pembuat / approver / divisi "Mengetahui"
--   3 didistribusikan             4 satu divisi (bukan Rahasia)
--   5 Reference Access (view only, bukan Rahasia, revisi aktif Approved/Published)
-- Yang pertama cocok yang dipakai, jadi 5 hanya muncul kalau user tidak punya hak lain;
-- itulah yang membuat download/print ditolak (lihat DocumentAccessService.IsReferenceOnly).
-- =====================================================================
CREATE OR ALTER FUNCTION [dbo].[fn_Document_AccessReason] (@USERNAME VARCHAR(50), @DOCUMENT_TRANSACTION_ID INT)
RETURNS TINYINT
AS
BEGIN
	IF @USERNAME IS NULL OR @DOCUMENT_TRANSACTION_ID IS NULL RETURN 0;
	IF dbo.fn_User_IsDocumentControl(@USERNAME) = 1 RETURN 1;

	DECLARE @DIVISION VARCHAR(50), @CLASSIFIED VARCHAR(10), @STATUS VARCHAR(10), @DELETED INT,
	        @CREATOR VARCHAR(255), @CREATED_BY VARCHAR(255), @APPROVAL_ID INT, @FOUND BIT = 0,
	        @DEPARTMENT INT, @CATEGORY INT, @IS_HISTORY BIT = 0;

	SELECT @DIVISION = DIVISION, @CLASSIFIED = CLASSIFIED, @STATUS = [STATUS], @DELETED = ISNULL(DELETE_FLAG, 0),
	       @CREATOR = DOCUMENT_CREATOR, @CREATED_BY = CREATED_BY, @APPROVAL_ID = APPROVAL_ID, @FOUND = 1,
	       @DEPARTMENT = DEPARTMENT, @CATEGORY = DOCUMENT_ID
	FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID;

	IF @FOUND = 0
		SELECT TOP 1 @DIVISION = DIVISION, @CLASSIFIED = CLASSIFIED, @STATUS = [STATUS], @DELETED = 0,
		       @CREATOR = DOCUMENT_CREATOR, @CREATED_BY = CREATED_BY, @APPROVAL_ID = APPROVAL_ID, @FOUND = 1,
		       @IS_HISTORY = 1
		FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
		ORDER BY DOCUMENT_HISTORY_ID DESC;

	IF @FOUND = 0 RETURN 0;

	-- 2. pembuat, approver, divisi "Mengetahui"
	IF @CREATOR = @USERNAME OR @CREATED_BY = @USERNAME RETURN 2;
	IF EXISTS (SELECT 1 FROM dbo.TB_R_APPROVAL_D WHERE APPROVAL_ID = @APPROVAL_ID AND APPROVER = @USERNAME) RETURN 2;
	IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_RELATED_DIVISION R
	           WHERE R.DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
	             AND R.DIVISION_CODE IN (SELECT DIVISION FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME)) RETURN 2;

	-- 3. didistribusikan ke department / divisi user
	IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_DISTRIBUTION DD
	           WHERE DD.DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND DD.STATUS = 1
	             AND (DD.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME)
	                  OR DD.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM dbo.TB_M_DEPARTMENT
	                                          WHERE DIVISION IN (SELECT DIVISION FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME)))) RETURN 3;

	-- Rahasia berhenti di sini
	IF @CLASSIFIED = '2' RETURN 0;

	-- 4. satu divisi (Document Preparation)
	IF @DIVISION IN (SELECT DIVISION FROM dbo.TB_M_USER_POS WHERE USERNAME = @USERNAME) RETURN 4;

	-- 5. Reference Access (2026-11-18): revisi aktif yang sah, user diberi hak baca atas
	--    departemen pemilik (dan kategorinya), di dalam masa berlaku. Per orang, bukan per
	--    departemen: pindah departemen tidak membawa atau menghilangkan akses ini.
	IF @IS_HISTORY = 0 AND @DELETED = 0 AND @STATUS IN ('1', '5')
	   AND EXISTS (SELECT 1 FROM dbo.TB_M_REFERENCE_ACCESS RA
	               WHERE RA.VIEWER_USERNAME = @USERNAME AND RA.OWNER_DEPARTMENT_ID = @DEPARTMENT AND RA.[STATUS] = 1
	                 AND (RA.DOCUMENT_ID IS NULL OR RA.DOCUMENT_ID = @CATEGORY)
	                 AND CAST(GETDATE() AS DATE) BETWEEN RA.VALID_FROM AND RA.VALID_TO)
		RETURN 5;

	RETURN 0;
END
GO

-- Hak lihat: semua pemanggil lama (sp_DocumentFile_CanView, sp_DocumentSearch, ...) tetap
-- memakai fungsi ini; isinya kini dari fn_Document_AccessReason.
CREATE OR ALTER FUNCTION [dbo].[fn_Document_CanView] (@USERNAME VARCHAR(50), @DOCUMENT_TRANSACTION_ID INT)
RETURNS BIT
AS
BEGIN
	RETURN CASE WHEN dbo.fn_Document_AccessReason(@USERNAME, @DOCUMENT_TRANSACTION_ID) > 0 THEN 1 ELSE 0 END;
END
GO
