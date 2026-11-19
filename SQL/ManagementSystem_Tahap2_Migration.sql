-- =====================================================================
-- Management System tahap 2 (2026-11-19): Document Control per management system.
-- TB_M_MGMT_SYSTEM_USER (USER_ROLE = CONTROLLER) names the Document Control of a system
-- (QMS -> QA, K3L -> SHE). Rule (fn_User_IsDocumentControlFor):
--   * DMS-ADMIN: every system.
--   * listed as CONTROLLER of the system: yes.
--   * role DOC-CONTROL, listed for NO system, and the system lists no controller: yes
--     (what it was before tahap 2, so nothing changes until the lists are filled).
-- fn_User_IsDocumentControl (no system) = Document Control of at least one system; lists that
-- show "everything" to Document Control narrow it to their systems. Idempotent. DMS_NEW.
-- =====================================================================
CREATE OR ALTER FUNCTION [dbo].[fn_User_IsDocumentControlFor] (@USERNAME VARCHAR(50), @MGMT_SYSTEM VARCHAR(10))
RETURNS BIT
AS
BEGIN
	IF @USERNAME IS NULL RETURN 0;
	SET @MGMT_SYSTEM = ISNULL(NULLIF(@MGMT_SYSTEM, ''), 'QMS');
	IF EXISTS (SELECT 1 FROM dbo.TB_M_USER WHERE USERNAME = @USERNAME AND ROLE_ID = 'DMS-ADMIN') RETURN 1;
	IF EXISTS (SELECT 1 FROM dbo.TB_M_MGMT_SYSTEM_USER WHERE MGMT_SYSTEM_CODE = @MGMT_SYSTEM AND USER_ROLE = 'CONTROLLER' AND USERNAME = @USERNAME) RETURN 1;
	IF EXISTS (SELECT 1 FROM dbo.TB_M_USER WHERE USERNAME = @USERNAME AND ROLE_ID = 'DOC-CONTROL')
	   AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_MGMT_SYSTEM_USER WHERE USER_ROLE = 'CONTROLLER' AND USERNAME = @USERNAME)
	   AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_MGMT_SYSTEM_USER WHERE USER_ROLE = 'CONTROLLER' AND MGMT_SYSTEM_CODE = @MGMT_SYSTEM)
		RETURN 1;
	RETURN 0;
END
GO

CREATE OR ALTER FUNCTION [dbo].[fn_User_IsDocumentControl] (@USERNAME VARCHAR(50))
RETURNS BIT
AS
BEGIN
	-- 2026-11-17: a ROLE (DMS-ADMIN / DOC-CONTROL), not the department flag.
	-- 2026-11-19: or listed as CONTROLLER of a management system. Per document use
	-- fn_User_IsDocumentControlFor - this one only says "Document Control of something".
	IF EXISTS (SELECT 1 FROM dbo.TB_M_USER WHERE USERNAME = @USERNAME AND ROLE_ID IN ('DMS-ADMIN', 'DOC-CONTROL')) RETURN 1;
	IF EXISTS (SELECT 1 FROM dbo.TB_M_MGMT_SYSTEM_USER WHERE USER_ROLE = 'CONTROLLER' AND USERNAME = @USERNAME) RETURN 1;
	RETURN 0;
END
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

	DECLARE @DIVISION VARCHAR(50), @CLASSIFIED VARCHAR(10), @STATUS VARCHAR(10), @DELETED INT,
	        @CREATOR VARCHAR(255), @CREATED_BY VARCHAR(255), @APPROVAL_ID INT, @FOUND BIT = 0,
	        @DEPARTMENT INT, @CATEGORY INT, @IS_HISTORY BIT = 0, @SYSTEM VARCHAR(10);

	SELECT @DIVISION = DIVISION, @CLASSIFIED = CLASSIFIED, @STATUS = [STATUS], @DELETED = ISNULL(DELETE_FLAG, 0),
	       @CREATOR = DOCUMENT_CREATOR, @CREATED_BY = CREATED_BY, @APPROVAL_ID = APPROVAL_ID, @FOUND = 1,
	       @DEPARTMENT = DEPARTMENT, @CATEGORY = DOCUMENT_ID, @SYSTEM = MGMT_SYSTEM
	FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID;

	IF @FOUND = 0
		SELECT TOP 1 @DIVISION = DIVISION, @CLASSIFIED = CLASSIFIED, @STATUS = [STATUS], @DELETED = 0,
		       @CREATOR = DOCUMENT_CREATOR, @CREATED_BY = CREATED_BY, @APPROVAL_ID = APPROVAL_ID, @FOUND = 1,
		       @IS_HISTORY = 1, @SYSTEM = MGMT_SYSTEM
		FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
		ORDER BY DOCUMENT_HISTORY_ID DESC;

	IF @FOUND = 0 RETURN 0;

	-- 1. Document Control of the document's management system (2026-11-19, IMS tahap 2)
	IF dbo.fn_User_IsDocumentControlFor(@USERNAME, @SYSTEM) = 1 RETURN 1;

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
