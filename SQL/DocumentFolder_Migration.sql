-- =====================================================================
-- Document Folder tree (2026-08-28, request Hendra): virtual folder
-- hierarchy for DocumentControlDashboard - manually created/managed by
-- QMS, documents assigned to a folder later from the Dashboard (not
-- auto-derived from Category/Department, not set during Legacy Import).
--
-- Mirrors TB_M_MENU's self-referencing pattern (MENU_ID/PARENT_ID).
-- Jalankan sekali di database DMS_NEW.
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_M_DOCUMENT_FOLDER')
BEGIN
	CREATE TABLE [dbo].[TB_M_DOCUMENT_FOLDER] (
		FOLDER_ID		INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		PARENT_ID		INT NULL,
		FOLDER_NAME		VARCHAR(255) NOT NULL,
		DELETE_FLAG		INT NULL DEFAULT 0,
		CREATED_BY		VARCHAR(50) NULL,
		CREATED_DT		DATETIME NULL,
		CHANGED_BY		VARCHAR(50) NULL,
		CHANGED_DT		DATETIME NULL,
		CONSTRAINT FK_DOCUMENT_FOLDER_PARENT FOREIGN KEY (PARENT_ID) REFERENCES [dbo].[TB_M_DOCUMENT_FOLDER](FOLDER_ID)
	)
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TB_R_DOCUMENT') AND name = 'FOLDER_ID')
BEGIN
	ALTER TABLE [dbo].[TB_R_DOCUMENT] ADD FOLDER_ID INT NULL
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DOCUMENT_FOLDER_ID')
BEGIN
	ALTER TABLE [dbo].[TB_R_DOCUMENT]
	ADD CONSTRAINT FK_DOCUMENT_FOLDER_ID FOREIGN KEY (FOLDER_ID) REFERENCES [dbo].[TB_M_DOCUMENT_FOLDER](FOLDER_ID)
END
GO
