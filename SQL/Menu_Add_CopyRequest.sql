-- =====================================================================
-- Modul baru: Controlled Copy Request (QCD/FR-QMS-00/028) -
-- CopyRequestController / Views/CopyRequest. Menu terpisah dari
-- "Document Submission Form" (M00006-09/M00008, QCD/FR-QMS-00/003 -
-- REGISTRASI dokumen baru/revisi) - modul ini untuk MEMINTA salinan
-- dokumen yang SUDAH terdaftar & approved, bukan registrasi baru.
--
-- Menggantikan tombol "Request Document" lama di UserDashboard (satu klik,
-- tanpa form/approval) - request user 2026-08-11.
--
-- Bagian 1: menu + function + grant (pola sama seperti
-- Menu_Add_DocumentSubmissionForm.sql).
--
-- Approval-nya SATU LANGKAH oleh QMS (fungsi COPYREQUEST-APPROVE),
-- BUKAN chain internal berjenjang seperti Document Submission - revisi
-- keputusan Hendra 2026-08-11 (awalnya sempat dibuat pakai
-- TB_M_WORKFLOW_DOC_H/D 3-tingkat, lihat
-- SQL/CopyRequest_SimplifyApproval.sql untuk migrasi pembatalannya).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

-- ---------------------------------------------------------------------
-- Bagian 1: Menu + Function + Grant
-- ---------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00006-08')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00006-08', 'M00006', 'Controlled Copy Request', 'fa-files-o', '/CopyRequest/Index', 6, 'dms.admin', GETDATE(), 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-ADD')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('COPYREQUEST-ADD', 'M00006-08', 'Add', 'Controlled Copy Request Add Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-EDIT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('COPYREQUEST-EDIT', 'M00006-08', 'Edit', 'Controlled Copy Request Edit Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-DELETE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('COPYREQUEST-DELETE', 'M00006-08', 'Delete', 'Controlled Copy Request Delete Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-SUBMIT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('COPYREQUEST-SUBMIT', 'M00006-08', 'Submit for Approval', 'Controlled Copy Request Submit Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-PRINT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('COPYREQUEST-PRINT', 'M00006-08', 'Print Form', 'Controlled Copy Request Print Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-APPROVE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('COPYREQUEST-APPROVE', 'M00006-08', 'Approve/Reject', 'Controlled Copy Request Approve Function', 'dms.admin', GETDATE());
END

-- Digrant ke role set yang sama persis dengan Document Submission
-- (M00006-09/DOCSUBMISSION-*) - dicek langsung ke DB, modul QMS semacam ini
-- memang dibuka untuk semua level (Staff sampai Direktur) untuk BIKIN dan
-- SUBMIT request. COPYREQUEST-APPROVE SENGAJA TIDAK ikut di sini - itu
-- khusus tim QMS, digrant terpisah di bawah (default DMS-ADMIN saja),
-- selebihnya Hendra assign manual ke role tim QMS lewat halaman Role
-- Authorization.
INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT r.ROLE_ID, f.FUNCTION_ID, 'dms.admin', GETDATE()
FROM (VALUES
	('DeptHead'), ('DMS-ADMIN'), ('DMS-ADMIN-DEPT'), ('GENERAL-DEPTHEAD'),
	('GENERAL-DIVHEAD'), ('GENERAL-EO'), ('GENERAL-SECHEAD'), ('GENERAL-STAFF'),
	('PIC'), ('SecHead'), ('Staff')
) AS r(ROLE_ID)
CROSS JOIN (VALUES
	('COPYREQUEST-ADD'),
	('COPYREQUEST-EDIT'),
	('COPYREQUEST-DELETE'),
	('COPYREQUEST-SUBMIT'),
	('COPYREQUEST-PRINT')
) AS f(FUNCTION_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] a
	WHERE a.ROLE_ID = r.ROLE_ID AND a.FUNCTION_ID = f.FUNCTION_ID
);

-- COPYREQUEST-APPROVE: DMS-ADMIN (safety net) + tim QMS sesungguhnya, yaitu
-- role QPD (DeptHead/SecHead/Staff - 1:1 dengan department QPD, satu-satunya
-- department ber-DOCUMENT_CONTROL_ACCESS=1, lihat [[dms-copyrequest-approval-simplified]])
-- - model tim: siapa saja di QPD boleh approve/reject, tidak berjenjang.
-- Ditentukan Hendra 2026-08-11. Cabut dulu grant ke role lain yang sempat
-- kebawa dari versi awal script ini (semua role, sebelum direvisi jadi
-- satu-langkah QMS).
DELETE FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-APPROVE' AND ROLE_ID NOT IN ('DMS-ADMIN', 'DeptHead', 'SecHead', 'Staff');

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT r.ROLE_ID, 'COPYREQUEST-APPROVE', 'dms.admin', GETDATE()
FROM (VALUES ('DMS-ADMIN'), ('DeptHead'), ('SecHead'), ('Staff')) AS r(ROLE_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] a
	WHERE a.ROLE_ID = r.ROLE_ID AND a.FUNCTION_ID = 'COPYREQUEST-APPROVE'
);

INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT r.ROLE_ID, 'M00006-08', 'dms.admin', GETDATE()
FROM (VALUES
	('DeptHead'), ('DMS-ADMIN'), ('DMS-ADMIN-DEPT'), ('GENERAL-DEPTHEAD'),
	('GENERAL-DIVHEAD'), ('GENERAL-EO'), ('GENERAL-SECHEAD'), ('GENERAL-STAFF'),
	('PIC'), ('SecHead'), ('Staff')
) AS r(ROLE_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] a
	WHERE a.ROLE_ID = r.ROLE_ID AND a.MENU_ID = 'M00006-08'
);
GO

-- ---------------------------------------------------------------------
-- Bagian 2: Seed status/copy-type system values
-- ---------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_STATUS' AND SYSTEM_CODE = '0')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_STATUS', '0', 'Draft', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_STATUS' AND SYSTEM_CODE = '1')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_STATUS', '1', 'Waiting Approval', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_STATUS' AND SYSTEM_CODE = '2')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_STATUS', '2', 'Approved', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_STATUS' AND SYSTEM_CODE = '3')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_STATUS', '3', 'Rejected', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'COPY_REQUEST_STATUS' AND SYSTEM_CODE = '4')
	INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('COPY_REQUEST_STATUS', '4', 'Cancelled', 1, 'dms.admin', GETDATE());
GO
