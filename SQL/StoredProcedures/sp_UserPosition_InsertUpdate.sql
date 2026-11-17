SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Saves a user's position. Which organisation fields a position needs follows its
-- APPROVER_SCOPE in Position Master (2026-11-17; before, only position ids 1-7 were
-- known and any other position, e.g. Supervisor, was silently not saved):
--   COMPANY    no division / department / section (Direktur, Presiden Direktur)
--   DIVISION   division                            (Div. Head, Executive Officer)
--   DEPARTMENT division + department                (Dept. Head, Specialist)
--   SECTION    division + department + section      (Staff, Section Head, Supervisor, ...)
-- Head positions (2-7) have one holder per organisation unit. A user has one position;
-- Executive Officer (5) may hold several divisions (the controller deletes first, then
-- calls this once per division).
CREATE OR ALTER PROCEDURE [dbo].[sp_UserPosition_InsertUpdate]
	@USERNAME 			VARCHAR(255),
	@POSITION_ID		INT,
	@DIVISION				VARCHAR(255),
	@DEPARTMENT_ID	INT,
	@SECTION_ID			INT,
	@LOGIN_USER 		VARCHAR(255),
	@RETURN_MSG 		VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_UserPosition_InsertUpdate';

	DECLARE @positionName VARCHAR(255), @scope VARCHAR(20), @deptCode VARCHAR(255), @sectionName VARCHAR(255);

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'User Position', 'Insert Update', @LOCATION, @LOGIN_USER

	IF @POSITION_ID IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	SELECT @positionName = POSITION_NAME,
	       @scope = UPPER(ISNULL(NULLIF(LTRIM(RTRIM(APPROVER_SCOPE)), ''), 'SECTION'))
	FROM [dbo].[TB_M_POSITION]
	WHERE POSITION_ID = @POSITION_ID AND ISNULL(DELETE_FLAG, 0) = 0;

	IF @positionName IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position not found or inactive';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	-- Keep only the fields the scope uses.
	IF @scope = 'COMPANY'    SELECT @DIVISION = NULL, @DEPARTMENT_ID = NULL, @SECTION_ID = NULL;
	IF @scope = 'DIVISION'   SELECT @DEPARTMENT_ID = NULL, @SECTION_ID = NULL;
	IF @scope = 'DEPARTMENT' SET @SECTION_ID = NULL;
	IF LEN(ISNULL(@DIVISION, '')) < 1 SET @DIVISION = NULL;

	IF @scope IN ('DIVISION', 'DEPARTMENT', 'SECTION') AND @DIVISION IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Division should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @scope IN ('DEPARTMENT', 'SECTION') AND @DEPARTMENT_ID IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Department should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @scope = 'SECTION' AND @SECTION_ID IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Section should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	-- Head positions: one holder per organisation unit.
	IF @POSITION_ID IN (2, 3, 4, 5, 6, 7)
	   AND EXISTS (	SELECT TOP 1 1 FROM [dbo].[TB_M_USER_POS]
					WHERE POSITION_ID = @POSITION_ID
					AND ISNULL(DIVISION, '') = ISNULL(@DIVISION, '')
					AND ISNULL(DEPARTMENT_ID, 0) = ISNULL(@DEPARTMENT_ID, 0)
					AND ISNULL(SECTION_ID, 0) = ISNULL(@SECTION_ID, 0)
					AND USERNAME != @USERNAME)
	BEGIN
		SELECT @deptCode = DEPARTMENT_CODE FROM TB_M_DEPARTMENT WHERE DEPARTMENT_ID = @DEPARTMENT_ID
		SELECT @sectionName = SECTION_NAME FROM TB_M_SECTION WHERE SECTION_ID = @SECTION_ID

		SET @RETURN_MSG = 'ERROR : Position ' + @positionName
			+ ISNULL(' in Division ' + @DIVISION, '')
			+ ISNULL(', Department ' + @deptCode, '')
			+ ISNULL(' and Section ' + @sectionName, '')
			+ ' is Already Exists';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @POSITION_ID = 5
	BEGIN
		IF NOT EXISTS (	SELECT TOP 1 1 FROM [dbo].[TB_M_USER_POS]
						WHERE POSITION_ID = @POSITION_ID AND DIVISION = @DIVISION AND USERNAME = @USERNAME)
		BEGIN
			INSERT INTO [dbo].[TB_M_USER_POS] (USERNAME, POSITION_ID, DIVISION)
			VALUES (@USERNAME, @POSITION_ID, @DIVISION)
		END
	END
	ELSE
	BEGIN
		DELETE FROM [dbo].[TB_M_USER_POS] WHERE USERNAME = @USERNAME;

		INSERT INTO [dbo].[TB_M_USER_POS] (USERNAME, POSITION_ID, DIVISION, DEPARTMENT_ID, SECTION_ID)
		VALUES (@USERNAME, @POSITION_ID, @DIVISION, @DEPARTMENT_ID, @SECTION_ID)
	END

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
