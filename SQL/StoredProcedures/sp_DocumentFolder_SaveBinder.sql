SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Binder details of a Document Control folder (2026-11-15, SQL/DocumentBinder_Migration.sql).
-- @FOLDER_TYPE NULL turns the folder back into a plain folder; the code, holder and
-- location may still be kept. Refused when:
--   - the label code is already used by another folder
--   - a controlled copy binder has no holder department
--   - the type changes while the binder still holds content of the other kind
--     (documents for MASTER / plain, copies in circulation for COPY)
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentFolder_SaveBinder]
	@FOLDER_ID      INT,
	@FOLDER_TYPE    VARCHAR(10) = NULL,
	@FOLDER_CODE    VARCHAR(50) = NULL,
	@DEPARTMENT_ID  INT = NULL,
	@LOCATION       NVARCHAR(200) = NULL,
	@REMARKS        NVARCHAR(500) = NULL,
	@USERNAME       VARCHAR(50)
AS
BEGIN
	SET NOCOUNT ON;
	SET @FOLDER_TYPE = NULLIF(UPPER(LTRIM(RTRIM(@FOLDER_TYPE))), '');
	SET @FOLDER_CODE = NULLIF(UPPER(LTRIM(RTRIM(@FOLDER_CODE))), '');
	SET @LOCATION = NULLIF(LTRIM(RTRIM(@LOCATION)), '');
	SET @REMARKS = NULLIF(LTRIM(RTRIM(@REMARKS)), '');

	IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_DOCUMENT_FOLDER] WHERE FOLDER_ID = @FOLDER_ID AND ISNULL(DELETE_FLAG, 0) = 0)
		THROW 50011, 'Folder not found.', 1;
	IF @FOLDER_TYPE IS NOT NULL AND @FOLDER_TYPE NOT IN ('MASTER', 'COPY')
		THROW 50012, 'Unknown binder type.', 1;
	IF @FOLDER_TYPE IS NOT NULL AND @FOLDER_CODE IS NULL
		THROW 50013, 'Enter the binder code written on its label.', 1;
	IF @FOLDER_TYPE = 'COPY' AND @DEPARTMENT_ID IS NULL
		THROW 50014, 'Choose the department that holds this binder.', 1;
	IF @DEPARTMENT_ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_DEPARTMENT] WHERE DEPARTMENT_ID = @DEPARTMENT_ID)
		THROW 50015, 'Department not found.', 1;
	IF @FOLDER_CODE IS NOT NULL AND EXISTS (
		SELECT 1 FROM [dbo].[TB_M_DOCUMENT_FOLDER]
		WHERE FOLDER_CODE = @FOLDER_CODE AND FOLDER_ID <> @FOLDER_ID AND ISNULL(DELETE_FLAG, 0) = 0)
		THROW 50016, 'This binder code is already used by another binder.', 1;

	IF @FOLDER_TYPE = 'COPY' AND EXISTS (
		SELECT 1 FROM [dbo].[TB_R_DOCUMENT] WHERE FOLDER_ID = @FOLDER_ID AND ISNULL(DELETE_FLAG, 0) = 0)
		THROW 50017, 'This folder still has documents assigned. Move them first: a controlled copy binder holds copies, not documents.', 1;
	IF ISNULL(@FOLDER_TYPE, '') <> 'COPY' AND EXISTS (
		SELECT 1 FROM [dbo].[TB_R_DOCUMENT_EXPORT] WHERE FOLDER_ID = @FOLDER_ID AND PURPOSE = 'CONTROLLED' AND WITHDRAWN_DT IS NULL)
		THROW 50018, 'This binder still holds controlled copies in circulation. Withdraw or move them first.', 1;

	UPDATE [dbo].[TB_M_DOCUMENT_FOLDER]
	SET FOLDER_TYPE = @FOLDER_TYPE,
		FOLDER_CODE = @FOLDER_CODE,
		DEPARTMENT_ID = @DEPARTMENT_ID,
		[LOCATION] = @LOCATION,
		REMARKS = @REMARKS,
		CHANGED_BY = @USERNAME,
		CHANGED_DT = GETDATE()
	WHERE FOLDER_ID = @FOLDER_ID;
END
GO
