-- =====================================================================
-- Add: "Download PDF" in Document Control (request Hendra 2026-11-11).
-- QMS downloads the approved document PDF with a stamp chosen by purpose
-- (ISO 9001 7.5.3):
--   MASTER        - archive copy, MASTER stamp
--   CONTROLLED    - CONTROLLED COPY with a copy number per document revision and a recipient
--   UNCONTROLLED  - reference / audit / external, UNCONTROLLED COPY + "Downloaded by"
-- A superseded revision always gets OBSOLETE, whatever the purpose.
--
-- 1. TB_R_DOCUMENT_EXPORT: every Download PDF (the controlled copy register).
--    WITHDRAWN_* columns: a controlled copy taken back (Copy Register).
-- 2. Stored procedures, run after this file:
--    SQL/StoredProcedures/sp_DocumentExport_Insert.sql
--    SQL/StoredProcedures/sp_DocumentExport_CopyRegister.sql
--    SQL/StoredProcedures/sp_DocumentExport_Withdraw.sql
-- 3. Function DOCUMENTCONTROLDASHBOARD-EXPORTPDF on menu Document Control (M00006-04),
--    granted to DMS-ADMIN. Give it to other QMS roles from the Role menu.
--    Users see the new menu item after logging in again.
--
-- Idempotent - safe to run again.
-- Run on database DMS_NEW
-- =====================================================================

-- Required for the filtered unique index (sqlcmd starts with QUOTED_IDENTIFIER OFF).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.TB_R_DOCUMENT_EXPORT', 'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[TB_R_DOCUMENT_EXPORT] (
		EXPORT_ID                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_DOCUMENT_EXPORT PRIMARY KEY,
		DOCUMENT_TRANSACTION_ID  INT NOT NULL,
		DOCUMENT_CODE            VARCHAR(255) NOT NULL,
		REVISION                 INT NULL,
		PURPOSE                  VARCHAR(20) NOT NULL,
		COPY_NO                  INT NULL,
		RECIPIENT                NVARCHAR(200) NULL,
		NOTES                    NVARCHAR(500) NULL,
		CREATED_BY               VARCHAR(50) NOT NULL,
		CREATED_DT               DATETIME NOT NULL,
		CONSTRAINT CK_TB_R_DOCUMENT_EXPORT_PURPOSE CHECK (PURPOSE IN ('MASTER', 'CONTROLLED', 'UNCONTROLLED'))
	);

END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TB_R_DOCUMENT_EXPORT_DOC' AND object_id = OBJECT_ID('dbo.TB_R_DOCUMENT_EXPORT'))
	CREATE INDEX IX_TB_R_DOCUMENT_EXPORT_DOC
		ON [dbo].[TB_R_DOCUMENT_EXPORT] (DOCUMENT_CODE, REVISION, PURPOSE);

-- One copy number per document revision.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TB_R_DOCUMENT_EXPORT_COPY' AND object_id = OBJECT_ID('dbo.TB_R_DOCUMENT_EXPORT'))
	CREATE UNIQUE INDEX UX_TB_R_DOCUMENT_EXPORT_COPY
		ON [dbo].[TB_R_DOCUMENT_EXPORT] (DOCUMENT_CODE, REVISION, COPY_NO)
		WHERE COPY_NO IS NOT NULL;
GO

-- Withdrawal of a controlled copy (Copy Register, 2026-11-11): NULL = in circulation.
IF COL_LENGTH('dbo.TB_R_DOCUMENT_EXPORT', 'WITHDRAWN_DT') IS NULL
	ALTER TABLE [dbo].[TB_R_DOCUMENT_EXPORT] ADD
		WITHDRAWN_BY   VARCHAR(50) NULL,
		WITHDRAWN_DT   DATETIME NULL,
		WITHDRAW_NOTE  NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'DOCUMENTCONTROLDASHBOARD-EXPORTPDF')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('DOCUMENTCONTROLDASHBOARD-EXPORTPDF', 'M00006-04', 'Download PDF',
		'Download the document PDF as Master, Controlled Copy (numbered) or Uncontrolled Copy', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE ROLE_ID = 'DMS-ADMIN' AND FUNCTION_ID = 'DOCUMENTCONTROLDASHBOARD-EXPORTPDF')
BEGIN
	INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'DOCUMENTCONTROLDASHBOARD-EXPORTPDF', 'dms.admin', GETDATE());
END
GO
