SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- File a controlled copy in a controlled copy binder, or take it out (@FOLDER_ID NULL)
-- (2026-11-15, SQL/DocumentBinder_Migration.sql). Only a copy still in circulation.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentExport_AssignBinder]
	@EXPORT_ID  INT,
	@FOLDER_ID  INT = NULL
AS
BEGIN
	SET NOCOUNT ON;

	IF @FOLDER_ID IS NOT NULL AND NOT EXISTS (
		SELECT 1 FROM [dbo].[TB_M_DOCUMENT_FOLDER]
		WHERE FOLDER_ID = @FOLDER_ID AND FOLDER_TYPE = 'COPY' AND ISNULL(DELETE_FLAG, 0) = 0)
		THROW 50021, 'Choose a controlled copy binder.', 1;

	UPDATE [dbo].[TB_R_DOCUMENT_EXPORT]
	SET FOLDER_ID = @FOLDER_ID
	WHERE EXPORT_ID = @EXPORT_ID AND PURPOSE = 'CONTROLLED' AND WITHDRAWN_DT IS NULL;

	IF @@ROWCOUNT = 0
		THROW 50022, 'This copy was not found or is already withdrawn.', 1;
END
GO
