SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DivisionMaster_Insert]
	@DIVISION_CODE  	  VARCHAR(5),
	@DIVISION_NAME		  VARCHAR(255),
	@VALID_FROM				  DATE = NULL,
	@VALID_TO					  DATE = NULL,
	@LOGIN_USER 				VARCHAR(255),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_DivisionMaster_Insert';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Division Master', 'Insert', @LOCATION, @LOGIN_USER

	-- Division Master tidak punya field Valid From/To di UI-nya (beda dari
	-- Section Master) - kolom ini cuma ada di skema, tidak dipakai filter di
	-- modul manapun (dicek sys.sql_modules). Sebelumnya param ini WAJIB diisi
	-- tanpa default, sementara DivisionMasterRepo.Insert tidak pernah
	-- mengirimnya sama sekali - Add selalu gagal SQL error "parameter
	-- @VALID_FROM tidak disuplai". Kasih default masuk akal di sini supaya
	-- Add jalan tanpa perlu ubah C#/form (bug ditemukan 2026-08-12).
	IF @VALID_FROM IS NULL SET @VALID_FROM = CAST(GETDATE() AS DATE);
	IF @VALID_TO IS NULL SET @VALID_TO = '99991231';

	IF @DIVISION_CODE IS NULL OR LEN(@DIVISION_CODE) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Division Code should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	IF @DIVISION_NAME IS NULL OR LEN(@DIVISION_NAME) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Division Name should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	IF @VALID_FROM IS NULL OR LEN(@VALID_FROM) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Valid From should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
			
	IF @VALID_TO IS NULL OR LEN(@VALID_TO) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Valid To should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	IF @VALID_FROM > @VALID_TO
	BEGIN
		SET @RETURN_MSG = 'ERROR: Valid From cannot bigger than Valid To';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
-- 	IF @WORKFLOW_ID IS NULL OR LEN(@WORKFLOW_ID) < 1
-- 	BEGIN
-- 		SET @RETURN_MSG = 'ERROR: Workflow should not be null';
-- 		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
-- 		RETURN 0;
-- 	END
	
	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_DIVISION] 
 	WHERE [DIVISION_CODE] = @DIVISION_CODE
	AND DELETE_FLAG != 1)
	BEGIN
 			SET @RETURN_MSG = 'ERROR: Data already exist Division Code = ' + @DIVISION_CODE;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
			RETURN 0;
 	END
	
	INSERT INTO [dbo].[TB_M_DIVISION] (
		DIVISION_CODE,
		DIVISION_NAME,
		VALID_FROM,
		VALID_TO,
		CREATED_DT,
		CREATED_BY,
		CHANGED_DT,
		CHANGED_BY
	) VALUES (
		@DIVISION_CODE,
		@DIVISION_NAME,
		@VALID_FROM,
		@VALID_TO,
		GETDATE(),
		@LOGIN_USER,
		GETDATE(),
		@LOGIN_USER
	)
	
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
