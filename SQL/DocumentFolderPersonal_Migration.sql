-- =====================================================================
-- Personal document folders for My Documents / UserDashboard (2026-08-29,
-- request Hendra: "My Document itu punya dashboard user" - fully separate per
-- user from the global Document Control folder tree, confirmed via
-- clarification: a document can sit in a different personal folder per user,
-- independent of its one official Document Control folder assignment.
--
-- Mirrors TB_M_DOCUMENT_FOLDER / TB_R_DOCUMENT.FOLDER_ID (Document Control's
-- global folder feature) but scoped by USERNAME, and since one document can't
-- hold different FOLDER_ID values for different users on a single column, the
-- assignment lives in its own many-rows-per-document table instead of a column
-- on TB_R_DOCUMENT.
--
-- Jalankan sekali di database DMS_NEW.
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_M_DOCUMENT_FOLDER_PERSONAL')
BEGIN
	CREATE TABLE [dbo].[TB_M_DOCUMENT_FOLDER_PERSONAL] (
		FOLDER_ID		INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		PARENT_ID		INT NULL,
		FOLDER_NAME		VARCHAR(255) NOT NULL,
		USERNAME		VARCHAR(255) NOT NULL,
		DELETE_FLAG		INT NULL DEFAULT 0,
		CREATED_BY		VARCHAR(50) NULL,
		CREATED_DT		DATETIME NULL,
		CHANGED_BY		VARCHAR(50) NULL,
		CHANGED_DT		DATETIME NULL,
		CONSTRAINT FK_DOCUMENT_FOLDER_PERSONAL_PARENT FOREIGN KEY (PARENT_ID) REFERENCES [dbo].[TB_M_DOCUMENT_FOLDER_PERSONAL](FOLDER_ID)
	)
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_R_DOCUMENT_FOLDER_PERSONAL')
BEGIN
	CREATE TABLE [dbo].[TB_R_DOCUMENT_FOLDER_PERSONAL] (
		ID						INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		DOCUMENT_TRANSACTION_ID	INT NOT NULL,
		USERNAME				VARCHAR(255) NOT NULL,
		FOLDER_ID				INT NOT NULL,
		CREATED_BY				VARCHAR(50) NULL,
		CREATED_DT				DATETIME NULL,
		CHANGED_BY				VARCHAR(50) NULL,
		CHANGED_DT				DATETIME NULL,
		CONSTRAINT UQ_DOCUMENT_FOLDER_PERSONAL_DOC_USER UNIQUE (DOCUMENT_TRANSACTION_ID, USERNAME),
		CONSTRAINT FK_DOCUMENT_FOLDER_PERSONAL_DOC FOREIGN KEY (DOCUMENT_TRANSACTION_ID) REFERENCES [dbo].[TB_R_DOCUMENT](DOCUMENT_TRANSACTION_ID),
		CONSTRAINT FK_DOCUMENT_FOLDER_PERSONAL_FOLDER FOREIGN KEY (FOLDER_ID) REFERENCES [dbo].[TB_M_DOCUMENT_FOLDER_PERSONAL](FOLDER_ID)
	)
END
GO
