-- =====================================================================
-- Modul baru: Controlled Copy Request (QCD/FR-QMS-00/028) - permintaan
-- salinan dokumen yang SUDAH terdaftar & approved di P4D, BUKAN registrasi
-- dokumen baru (itu domain DocumentSubmission/QCD-FR-QMS-00-003). Meniru
-- pola header+detail TB_R_DOC_SUBMISSION_FORM_H/D persis, lihat file itu
-- untuk arsitektur referensi.
--
-- Menggantikan fitur "Request Document" lama di UserDashboard (satu klik,
-- tanpa form/approval) - request user 2026-08-11: proses QMS asli butuh
-- form + approval berjenjang Staff -> Section Head -> Dept Head dulu.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_R_COPY_REQUEST_H')
BEGIN
	CREATE TABLE [dbo].[TB_R_COPY_REQUEST_H] (
		[REQUEST_ID]      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[REQUEST_NO]      VARCHAR(50)  NULL,
		[DIVISION]        VARCHAR(50)  NULL,
		[DEPARTMENT_ID]   INT          NULL,
		[SECTION_CODE]    VARCHAR(5)   NULL,
		[REQUEST_DATE]    DATETIME     NULL,
		[DOC_CATEGORY]    VARCHAR(200) NULL,
		[STATUS]          CHAR(1)      NOT NULL DEFAULT ('0'),
		[APPROVAL_ID]     INT          NULL,
		[REQUESTED_BY]    VARCHAR(255) NULL,
		[SUBMITTED_BY]    VARCHAR(50)  NULL,
		[SUBMITTED_DT]    DATETIME     NULL,
		[REMARK]          VARCHAR(MAX) NULL,
		[DELETE_FLAG]     INT          NOT NULL DEFAULT ((0)),
		[CREATED_BY]      VARCHAR(50)  NULL,
		[CREATED_DT]      DATETIME     NULL,
		[CHANGED_BY]      VARCHAR(50)  NULL,
		[CHANGED_DT]      DATETIME     NULL
	);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_R_COPY_REQUEST_D')
BEGIN
	CREATE TABLE [dbo].[TB_R_COPY_REQUEST_D] (
		[REQUEST_DETAIL_ID]     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		[REQUEST_ID]            INT          NOT NULL,
		[LINE_NO]               INT          NULL,
		[DOCUMENT_TRANSACTION_ID] INT        NULL,
		[DOCUMENT_CODE]         VARCHAR(100) NULL,
		[DOCUMENT_NAME]         VARCHAR(255) NULL,
		[REVISION_NO]           INT          NULL,
		[COPY_TYPE]             VARCHAR(2)   NULL,
		[COPY_QTY]              INT          NULL,
		[REASON]                VARCHAR(MAX) NULL,
		[COUNTERMEASURE]        VARCHAR(MAX) NULL,
		[REMARKS]               VARCHAR(MAX) NULL,
		[DELETE_FLAG]           INT          NOT NULL DEFAULT ((0)),
		[CREATED_BY]            VARCHAR(50)  NULL,
		[CREATED_DT]            DATETIME     NULL,
		[CHANGED_BY]            VARCHAR(50)  NULL,
		[CHANGED_DT]            DATETIME     NULL,
		CONSTRAINT [FK_CPR_D_REQUEST] FOREIGN KEY ([REQUEST_ID])
			REFERENCES [dbo].[TB_R_COPY_REQUEST_H] ([REQUEST_ID])
			ON DELETE CASCADE
	);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CPR_D_REQUEST_ID')
BEGIN
	CREATE INDEX [IX_CPR_D_REQUEST_ID] ON [dbo].[TB_R_COPY_REQUEST_D] ([REQUEST_ID]);
END
GO
