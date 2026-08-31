-- Assigns (or clears, when @FOLDER_ID is NULL) one document's folder.
-- Called once per document from DocumentControlDashboardController's
-- MoveDocumentsToFolder loop - same "single-row SP, loop in C#" pattern
-- as PositionMasterController.DeleteMultiple.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentControlDashboard_AssignFolder]
	@DOCUMENT_TRANSACTION_ID	INT,
	@FOLDER_ID					INT,
	@LOGIN_USER					VARCHAR(255),
	@RETURN_MSG					VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_DocumentControlDashboard_AssignFolder';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Control Dashboard', 'Assign Folder', @LOCATION, @LOGIN_USER

	IF @FOLDER_ID IS NOT NULL AND NOT EXISTS (
		SELECT 1 FROM [dbo].[TB_M_DOCUMENT_FOLDER] WHERE FOLDER_ID = @FOLDER_ID AND ISNULL(DELETE_FLAG, 0) = 0
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Folder not found';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	UPDATE [dbo].[TB_R_DOCUMENT]
	SET FOLDER_ID	= @FOLDER_ID,
			CHANGED_DT	= GETDATE(),
			CHANGED_BY	= @LOGIN_USER
	WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID

	SET @RETURN_MSG = 'Successfully Save Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
