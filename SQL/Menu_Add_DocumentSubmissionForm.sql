-- =====================================================================
-- Modul baru: Document Submission Form (QCD/FR-QMS-00/003) -
-- DocumentSubmissionController / Views/DocumentSubmission. Menu terpisah
-- dari "Document Registration" (M00006-02, P4DMaintenance) - lihat
-- Menu_Add_DocumentSubmissionForm.sql untuk konteks lengkap.
--
-- Bagian 1: menu + function + grant (pola sama seperti
-- Menu_Add_ExternalDocumentControl.sql - menu di sidebar diatur
-- TB_M_AUTH_MENU, tombol Add/Edit/dst diatur TB_M_AUTH_FUNCTION, DUA
-- tabel ini HARUS di-grant terpisah, jangan sampai salah satu lupa.)
--
-- Bagian 2: seed TB_M_WORKFLOW_DOC_H/_D untuk approval berjenjang 3
-- tingkat (Staff -> Section Head -> Dept Head, sesuai blok tanda tangan
-- form fisik: Dibuat Oleh / Diperiksa Oleh / Disetujui Oleh), dipakai
-- oleh sp_WorkflowDoc_Create (engine approval generik yang sudah ada,
-- dipakai juga oleh Document Preparation). DOCUMENT_LEVEL = 9 dipilih
-- khusus supaya TIDAK bentrok dengan level 1-4 yang sudah dipakai
-- Document Preparation (dicek: TB_M_WORKFLOW_DOC_H yang ada cuma level
-- 1-4). sp_WorkflowDoc_Create mengunci chain lewat (DOCUMENT_LEVEL,
-- CREATOR_LEVEL) - CREATOR_LEVEL diambil otomatis dari TB_M_POSITION
-- milik user yang bikin submission, jadi disediakan 3 header (satu per
-- kemungkinan level pembuat: Staff/Section Head/Dept Head) supaya siapa
-- pun yang membuat form tetap ketemu chain-nya.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

-- ---------------------------------------------------------------------
-- Bagian 1: Menu + Function + Grant
-- ---------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00006-09')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00006-09', 'M00006', 'Document Submission Form', 'fa-clipboard', '/DocumentSubmission/Index', 6, 'dms.admin', GETDATE(), 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'DOCSUBMISSION-ADD')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('DOCSUBMISSION-ADD', 'M00006-09', 'Add', 'Document Submission Form Add Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'DOCSUBMISSION-EDIT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('DOCSUBMISSION-EDIT', 'M00006-09', 'Edit', 'Document Submission Form Edit Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'DOCSUBMISSION-DELETE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('DOCSUBMISSION-DELETE', 'M00006-09', 'Delete', 'Document Submission Form Delete Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'DOCSUBMISSION-SUBMIT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('DOCSUBMISSION-SUBMIT', 'M00006-09', 'Submit for Approval', 'Document Submission Form Submit Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'DOCSUBMISSION-PRINT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('DOCSUBMISSION-PRINT', 'M00006-09', 'Print Form', 'Document Submission Form Print Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'DOCSUBMISSION-APPROVE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('DOCSUBMISSION-APPROVE', 'M00006-09', 'Approve/Reject', 'Document Submission Form Approve Function', 'dms.admin', GETDATE());
END

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT 'DMS-ADMIN', f.FUNCTION_ID, 'dms.admin', GETDATE()
FROM (VALUES
	('DOCSUBMISSION-ADD'),
	('DOCSUBMISSION-EDIT'),
	('DOCSUBMISSION-DELETE'),
	('DOCSUBMISSION-SUBMIT'),
	('DOCSUBMISSION-PRINT'),
	('DOCSUBMISSION-APPROVE')
) AS f(FUNCTION_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] a
	WHERE a.ROLE_ID = 'DMS-ADMIN' AND a.FUNCTION_ID = f.FUNCTION_ID
);

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] WHERE ROLE_ID = 'DMS-ADMIN' AND MENU_ID = 'M00006-09')
BEGIN
	INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'M00006-09', 'dms.admin', GETDATE());
END
GO

-- ---------------------------------------------------------------------
-- Bagian 2: Seed 3-tier approval chain (DOCUMENT_LEVEL = 9)
-- ---------------------------------------------------------------------

DECLARE @StaffLevel INT = (SELECT POSITION_LEVEL FROM TB_M_POSITION WHERE POSITION_ID = 1);
DECLARE @SectionHeadLevel INT = (SELECT POSITION_LEVEL FROM TB_M_POSITION WHERE POSITION_ID = 2);
DECLARE @DeptHeadLevel INT = (SELECT POSITION_LEVEL FROM TB_M_POSITION WHERE POSITION_ID = 3);
DECLARE @WorkflowDocId INT;

-- Chain untuk pembuat = Staff: Dibuat(Staff, auto-approve) -> Diperiksa(Section Head) -> Disetujui(Dept Head)
IF NOT EXISTS (SELECT 1 FROM TB_M_WORKFLOW_DOC_H WHERE DOCUMENT_LEVEL = 9 AND CREATOR_LEVEL = @StaffLevel)
BEGIN
	INSERT INTO TB_M_WORKFLOW_DOC_H (DOCUMENT_LEVEL, CREATOR_LEVEL, CREATED_BY, CREATED_DT)
	VALUES (9, @StaffLevel, 'dms.admin', GETDATE());
	SET @WorkflowDocId = SCOPE_IDENTITY();

	INSERT INTO TB_M_WORKFLOW_DOC_D (WORKFLOW_DOC_ID, WORKFLOW_SEQ, POSITION_ID, LABEL, CREATED_BY, CREATED_DT) VALUES
		(@WorkflowDocId, 1, 1, 'Dibuat Oleh', 'dms.admin', GETDATE()),
		(@WorkflowDocId, 2, 2, 'Diperiksa Oleh', 'dms.admin', GETDATE()),
		(@WorkflowDocId, 3, 3, 'Disetujui Oleh', 'dms.admin', GETDATE());
END

-- Chain untuk pembuat = Section Head: Dibuat/Diperiksa(Section Head, auto-approve) -> Disetujui(Dept Head)
IF NOT EXISTS (SELECT 1 FROM TB_M_WORKFLOW_DOC_H WHERE DOCUMENT_LEVEL = 9 AND CREATOR_LEVEL = @SectionHeadLevel)
BEGIN
	INSERT INTO TB_M_WORKFLOW_DOC_H (DOCUMENT_LEVEL, CREATOR_LEVEL, CREATED_BY, CREATED_DT)
	VALUES (9, @SectionHeadLevel, 'dms.admin', GETDATE());
	SET @WorkflowDocId = SCOPE_IDENTITY();

	INSERT INTO TB_M_WORKFLOW_DOC_D (WORKFLOW_DOC_ID, WORKFLOW_SEQ, POSITION_ID, LABEL, CREATED_BY, CREATED_DT) VALUES
		(@WorkflowDocId, 1, 2, 'Dibuat Oleh / Diperiksa Oleh', 'dms.admin', GETDATE()),
		(@WorkflowDocId, 2, 3, 'Disetujui Oleh', 'dms.admin', GETDATE());
END

-- Chain untuk pembuat = Dept Head: Dibuat/Disetujui(Dept Head, auto-approve seluruhnya)
IF NOT EXISTS (SELECT 1 FROM TB_M_WORKFLOW_DOC_H WHERE DOCUMENT_LEVEL = 9 AND CREATOR_LEVEL = @DeptHeadLevel)
BEGIN
	INSERT INTO TB_M_WORKFLOW_DOC_H (DOCUMENT_LEVEL, CREATOR_LEVEL, CREATED_BY, CREATED_DT)
	VALUES (9, @DeptHeadLevel, 'dms.admin', GETDATE());
	SET @WorkflowDocId = SCOPE_IDENTITY();

	INSERT INTO TB_M_WORKFLOW_DOC_D (WORKFLOW_DOC_ID, WORKFLOW_SEQ, POSITION_ID, LABEL, CREATED_BY, CREATED_DT) VALUES
		(@WorkflowDocId, 1, 3, 'Dibuat Oleh / Disetujui Oleh', 'dms.admin', GETDATE());
END
GO
