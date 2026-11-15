-- =====================================================================
-- Binder (bantex) management in Document Control (request Hendra 2026-11-15).
--
-- A Document Control folder can now be a physical binder:
--   FOLDER_TYPE NULL     - plain folder, only groups documents (as before)
--   FOLDER_TYPE 'MASTER' - master binder kept by Document Control: holds the documents
--                          assigned to it (TB_R_DOCUMENT.FOLDER_ID)
--   FOLDER_TYPE 'COPY'   - controlled copy binder kept by a department: holds the numbered
--                          controlled copies filed in it (TB_R_DOCUMENT_EXPORT.FOLDER_ID)
-- with a label code, the holder department and the physical location. Who holds a binder
-- (one per division or one per department) is data, not a rule in code.
--
-- 1. Columns on TB_M_DOCUMENT_FOLDER and TB_R_DOCUMENT_EXPORT (this file).
-- 2. Stored procedures, run after this file:
--    SQL/StoredProcedures/sp_DocumentFolder_Tree.sql
--    SQL/StoredProcedures/sp_DocumentFolder_Delete.sql
--    SQL/StoredProcedures/sp_DocumentFolder_SaveBinder.sql
--    SQL/StoredProcedures/sp_DocumentBinder_Contents.sql
--    SQL/StoredProcedures/sp_DocumentExport_AssignBinder.sql
--    SQL/StoredProcedures/sp_DocumentExport_Insert.sql
--    SQL/StoredProcedures/sp_DocumentExport_CopyRegister.sql
--    SQL/StoredProcedures/sp_DocumentControlDashboard_AssignFolder.sql
--    SQL/StoredProcedures/sp_DocumentControlDashboard_Search.sql
--    SQL/StoredProcedures/sp_DocumentControlDashboard_ActionItems.sql
--    SQL/StoredProcedures/sp_DocumentMaintenance_SupersedeRevision.sql
--
-- Idempotent - safe to run again. Run on database DMS_NEW.
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.TB_M_DOCUMENT_FOLDER', 'FOLDER_TYPE') IS NULL
	ALTER TABLE [dbo].[TB_M_DOCUMENT_FOLDER] ADD
		FOLDER_TYPE    VARCHAR(10) NULL,
		FOLDER_CODE    VARCHAR(50) NULL,
		DEPARTMENT_ID  INT NULL,
		LOCATION       NVARCHAR(200) NULL,
		REMARKS        NVARCHAR(500) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TB_M_DOCUMENT_FOLDER_TYPE')
	ALTER TABLE [dbo].[TB_M_DOCUMENT_FOLDER] ADD CONSTRAINT CK_TB_M_DOCUMENT_FOLDER_TYPE
		CHECK (FOLDER_TYPE IS NULL OR FOLDER_TYPE IN ('MASTER', 'COPY'));
GO

-- The binder a controlled copy is filed in (NULL = not filed in a binder).
IF COL_LENGTH('dbo.TB_R_DOCUMENT_EXPORT', 'FOLDER_ID') IS NULL
	ALTER TABLE [dbo].[TB_R_DOCUMENT_EXPORT] ADD FOLDER_ID INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TB_R_DOCUMENT_EXPORT_FOLDER' AND object_id = OBJECT_ID('dbo.TB_R_DOCUMENT_EXPORT'))
	CREATE INDEX IX_TB_R_DOCUMENT_EXPORT_FOLDER
		ON [dbo].[TB_R_DOCUMENT_EXPORT] (FOLDER_ID)
		WHERE FOLDER_ID IS NOT NULL;
GO
