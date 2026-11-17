SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Delete a Division / Department / Section that was never used (request Hendra 2026-11-16,
-- SQL/MasterDataRemove_Migration.sql). Soft delete: DELETE_FLAG = 2 (Deleted), so the row is
-- kept for the audit trail but no longer listed, selectable, or activated again. Used data is
-- refused with where it is used; it can only be deactivated.
CREATE OR ALTER PROCEDURE [dbo].[sp_MasterData_Remove]
	@ENTITY			VARCHAR(20),
	@ID				INT,
	@LOGIN_USER		VARCHAR(255),
	@RETURN_MSG		VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	DECLARE @PROCESS_ID BIGINT,
			@LOCATION VARCHAR(255) = 'sp_MasterData_Remove',
			@LABEL VARCHAR(20),
			@CODE VARCHAR(255),
			@USED VARCHAR(500);

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Master Data', 'Remove', @LOCATION, @LOGIN_USER;

	IF @ENTITY = 'DIVISION'
		SELECT @LABEL = 'Division', @CODE = DIVISION_CODE FROM dbo.TB_M_DIVISION
		WHERE DIVISION_ID = @ID AND ISNULL(DELETE_FLAG, 0) <> 2;
	ELSE IF @ENTITY = 'DEPARTMENT'
		SELECT @LABEL = 'Department', @CODE = DEPARTMENT_CODE FROM dbo.TB_M_DEPARTMENT
		WHERE DEPARTMENT_ID = @ID AND ISNULL(DELETE_FLAG, 0) <> 2;
	ELSE IF @ENTITY = 'SECTION'
		SELECT @LABEL = 'Section', @CODE = DEPARTMENT_CODE + ' / ' + SECTION_CODE FROM dbo.TB_M_SECTION
		WHERE SECTION_ID = @ID AND ISNULL(DELETE_FLAG, 0) <> 2;
	ELSE
	BEGIN
		SET @RETURN_MSG = 'ERROR: Unknown master data type.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER;
		RETURN 0;
	END

	IF @CODE IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data not found. It may have been deleted - refresh the page.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER;
		RETURN 0;
	END

	SET @USED = dbo.fn_MasterData_UsedBy(@ENTITY, @ID);
	IF @USED <> ''
	BEGIN
		SET @RETURN_MSG = 'ERROR: ' + @LABEL + ' ' + @CODE + ' is already used by ' + @USED
			+ ', so it cannot be deleted. Deactivate it instead.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER;
		RETURN 0;
	END

	IF @ENTITY = 'DIVISION'
		UPDATE dbo.TB_M_DIVISION SET DELETE_FLAG = 2, CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER WHERE DIVISION_ID = @ID;
	ELSE IF @ENTITY = 'DEPARTMENT'
		UPDATE dbo.TB_M_DEPARTMENT SET DELETE_FLAG = 2, CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER WHERE DEPARTMENT_ID = @ID;
	ELSE
		UPDATE dbo.TB_M_SECTION SET DELETE_FLAG = 2, CHANGED_DT = GETDATE(), CHANGED_BY = @LOGIN_USER WHERE SECTION_ID = @ID;

	SET @RETURN_MSG = 'Successfully deleted';
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER;
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() + ': ' + ERROR_MESSAGE() + ', at line = ' + CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER;
	RETURN 0;
END CATCH
GO
