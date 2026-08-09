-- =====================================================================
-- Document Submission Form (QCD/FR-QMS-00/003)
-- =====================================================================
-- Modul baru dan terpisah dari P4DMaintenance/TB_R_CTRL_DOCUMENT -
-- meniru form fisik "FORM PENGAJUAN PENERBITAN / PERUBAHAN / PEMUSNAHAN
-- DOKUMEN": satu form (header) berisi banyak baris dokumen (detail) yang
-- diajukan sekaligus, disetujui berjenjang (Staff -> Section Head ->
-- Dept Head, lihat Menu_Add_DocumentSubmissionForm.sql), lalu di-print
-- dan dibawa hardcopy ke QMS.
--
-- TB_R_DOC_SUBMISSION_FORM_H  = header (Divisi/Dept/Section/Tanggal, status)
-- TB_R_DOC_SUBMISSION_FORM_D  = daftar dokumen di dalam satu form
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW.
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_R_DOC_SUBMISSION_FORM_H')
BEGIN
	CREATE TABLE [dbo].[TB_R_DOC_SUBMISSION_FORM_H] (
		[SUBMISSION_ID]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[SUBMISSION_NO]     VARCHAR(50)   NULL,        -- diisi saat Submit, format FPD/yyyy/MM/0001
		[DIVISION]          VARCHAR(50)   NULL,
		[DEPARTMENT_ID]     INT           NULL,
		[SECTION_CODE]      VARCHAR(5)    NULL,        -- ikut konvensi TB_R_DOCUMENT (SECTION_CODE, bukan SECTION_ID)
		[SUBMISSION_DATE]   DATETIME      NULL,
		[DOC_CATEGORY]      VARCHAR(200)  NULL,        -- CSV kode TB_M_SYSTEM 'DOC_SUBMISSION_CATEGORY'
		[STATUS]            CHAR(1)       NOT NULL DEFAULT '0',  -- 0 Draft,1 Waiting Approval,2 Approved,3 Rejected,4 Cancelled
		[APPROVAL_ID]       INT           NULL,        -- link ke TB_R_APPROVAL_H (soft, sama seperti TB_R_DOCUMENT.APPROVAL_ID)
		[DOCUMENT_CREATOR]  VARCHAR(255)  NULL,         -- username, dipakai sp_WorkflowDoc_Create
		[SUBMITTED_BY]      VARCHAR(50)   NULL,
		[SUBMITTED_DT]      DATETIME      NULL,
		[REMARK]            VARCHAR(MAX)  NULL,
		[DELETE_FLAG]       INT           NOT NULL DEFAULT 0,
		[CREATED_BY]        VARCHAR(50)   NULL,
		[CREATED_DT]        DATETIME      NULL,
		[CHANGED_BY]        VARCHAR(50)   NULL,
		[CHANGED_DT]        DATETIME      NULL
	);

	CREATE INDEX [IX_DSF_H_STATUS_DEPT] ON [dbo].[TB_R_DOC_SUBMISSION_FORM_H] ([STATUS], [DEPARTMENT_ID]);
	CREATE INDEX [IX_DSF_H_APPROVAL] ON [dbo].[TB_R_DOC_SUBMISSION_FORM_H] ([APPROVAL_ID]);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_R_DOC_SUBMISSION_FORM_D')
BEGIN
	CREATE TABLE [dbo].[TB_R_DOC_SUBMISSION_FORM_D] (
		[SUBMISSION_DETAIL_ID]   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[SUBMISSION_ID]          INT           NOT NULL,
		[LINE_NO]                INT           NULL,
		[DOCUMENT_NO]            VARCHAR(100)  NULL,   -- kosong kalau SUBMISSION_TYPE = Baru
		[DOCUMENT_NAME]          VARCHAR(255)  NULL,
		[CLASSIFICATION]         INT           NULL,   -- reuse TB_M_SYSTEM 'CLASSIFIED' (1=Umum,2=Rahasia)
		[SUBMISSION_TYPE]        VARCHAR(2)    NULL,   -- TB_M_SYSTEM 'DOC_SUBMISSION_TYPE' (01 Baru/02 Revisi/03 Pemusnahan)
		[REVISION_NO]            INT           NULL,   -- hanya diisi kalau SUBMISSION_TYPE = 02 ("Revisi Ke-")
		[ITEM_CHANGED]           VARCHAR(MAX)  NULL,
		[REASON]                 VARCHAR(MAX)  NULL,
		[EFFECTIVE_DATE]         DATETIME      NULL,
		[COPY_TYPE]              VARCHAR(2)    NULL,   -- TB_M_SYSTEM 'DOC_SUBMISSION_COPY_TYPE' (1 Hitam-Putih/2 Warna)
		[COPY_QTY]               INT           NULL,
		[DISTRIBUTION_DEPT_IDS]  VARCHAR(500)  NULL,   -- CSV TB_M_DEPARTMENT.DEPARTMENT_ID - referensi cetak saja, tidak memicu distribusi otomatis
		[DELETE_FLAG]            INT           NOT NULL DEFAULT 0,
		[CREATED_BY]             VARCHAR(50)   NULL,
		[CREATED_DT]             DATETIME      NULL,
		[CHANGED_BY]             VARCHAR(50)   NULL,
		[CHANGED_DT]             DATETIME      NULL,
		CONSTRAINT [FK_DSF_D_SUBMISSION] FOREIGN KEY ([SUBMISSION_ID])
			REFERENCES [dbo].[TB_R_DOC_SUBMISSION_FORM_H] ([SUBMISSION_ID])
	);

	CREATE INDEX [IX_DSF_D_SUBMISSION] ON [dbo].[TB_R_DOC_SUBMISSION_FORM_D] ([SUBMISSION_ID], [DELETE_FLAG]);
END
GO

-- =====================================================================
-- TB_M_SYSTEM seed - kode master baru khusus modul ini
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM TB_M_SYSTEM WHERE SYSTEM_TYPE = 'DOC_SUBMISSION_STATUS' AND SYSTEM_CODE = '0')
INSERT INTO TB_M_SYSTEM (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT) VALUES
	('DOC_SUBMISSION_STATUS', '0', 'Draft', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_STATUS', '1', 'Waiting Approval', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_STATUS', '2', 'Approved', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_STATUS', '3', 'Rejected', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_STATUS', '4', 'Cancelled', 1, 'dms.admin', GETDATE());
GO

-- 01/02/03 sengaja TIDAK ditambahkan ke SYSTEM_TYPE 'DOCUMENT_TYPE' yang sudah
-- ada (dipakai dropdown Document Preparation) - "Pemusnahan" adalah konsep
-- baru yang cuma berlaku di modul ini.
IF NOT EXISTS (SELECT 1 FROM TB_M_SYSTEM WHERE SYSTEM_TYPE = 'DOC_SUBMISSION_TYPE' AND SYSTEM_CODE = '01')
INSERT INTO TB_M_SYSTEM (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT) VALUES
	('DOC_SUBMISSION_TYPE', '01', 'Baru', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_TYPE', '02', 'Revisi', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_TYPE', '03', 'Pemusnahan', 1, 'dms.admin', GETDATE());
GO

IF NOT EXISTS (SELECT 1 FROM TB_M_SYSTEM WHERE SYSTEM_TYPE = 'DOC_SUBMISSION_COPY_TYPE' AND SYSTEM_CODE = '1')
INSERT INTO TB_M_SYSTEM (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT) VALUES
	('DOC_SUBMISSION_COPY_TYPE', '1', 'Hitam Putih (HP)', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_COPY_TYPE', '2', 'Warna (W)', 1, 'dms.admin', GETDATE());
GO

-- Kategori dokumen (checkbox "Jenis Dokumen" di header form fisik)
IF NOT EXISTS (SELECT 1 FROM TB_M_SYSTEM WHERE SYSTEM_TYPE = 'DOC_SUBMISSION_CATEGORY' AND SYSTEM_CODE = '1')
INSERT INTO TB_M_SYSTEM (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT) VALUES
	('DOC_SUBMISSION_CATEGORY', '1', 'Pedoman Perusahaan', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_CATEGORY', '2', 'SOP / EIS / IK / OPL / ACUAN', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_CATEGORY', '3', 'Prosedur', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_CATEGORY', '4', 'Checksheet / Form', 1, 'dms.admin', GETDATE()),
	('DOC_SUBMISSION_CATEGORY', '5', 'Lain Lain', 1, 'dms.admin', GETDATE());
GO
