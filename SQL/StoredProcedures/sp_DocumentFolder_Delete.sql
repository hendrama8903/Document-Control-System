-- Soft delete. Blocked if the folder still has a non-deleted child folder,
-- or any document still assigned to it - same "FK-in-use" guard style as
-- sp_PositionMaster_Delete (which blocks deleting a Position still assigned
-- to a user).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentFolder_Delete]
	@FOLDER_ID		INT,
	@LOGIN_USER		VARCHAR(255),
	@RETURN_MSG		VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_DocumentFolder_Delete';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Control Dashboard', 'Folder Delete', @LOCATION, @LOGIN_USER

	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_DOCUMENT_FOLDER] WHERE PARENT_ID = @FOLDER_ID AND ISNULL(DELETE_FLAG, 0) = 0)
	BEGIN
		SET @RETURN_MSG = 'ERROR: This folder still has subfolders - delete or move them first';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_R_DOCUMENT] WHERE FOLDER_ID = @FOLDER_ID AND ISNULL(DELETE_FLAG, 0) = 0)
	BEGIN
		SET @RETURN_MSG = 'ERROR: This folder still has documents assigned - move them first';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	UPDATE [dbo].[TB_M_DOCUMENT_FOLDER]
	SET DELETE_FLAG	= 1,
			CHANGED_DT	= GETDATE(),
			CHANGED_BY	= @LOGIN_USER
	WHERE FOLDER_ID = @FOLDER_ID

	SET @RETURN_MSG = 'Successfully Delete Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
