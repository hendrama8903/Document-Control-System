CREATE OR ALTER PROCEDURE [dbo].[sp_StartLog]
	@ro_v_PROCESS_ID 	BIGINT OUTPUT,
	@ri_v_MODULE 			VARCHAR(255),
	@ri_v_FUNCTION 		VARCHAR(255),
	@ri_v_LOCATION 		VARCHAR(255),
	@ri_v_USER_ID 		VARCHAR(50)
AS
BEGIN TRY
-- 	PROCESS_STATUS : 
-- 	0 = Start, 1 = Process, 2 = Finish, 3 = Finish with error, 4 = Abnormal

-- 	MESSAGE_TYPE : 
-- 	ERR, INF, WRN
	
	IF (@ri_v_MODULE IS NULL AND 
			@ri_v_FUNCTION IS NULL AND 
			@ri_v_LOCATION IS NULL AND
			@ri_v_USER_ID IS NULL 
	)
	BEGIN 
		RETURN 0;
	END;
	
	if @ri_v_USER_ID IS NULL OR (LEN(@ri_v_USER_ID) < 1)
	BEGIN
		SET @ri_v_USER_ID = ORIGINAL_LOGIN();
	END
	
	SELECT @ro_v_PROCESS_ID = ISNULL((MAX(PROCESS_ID) + 1), 1) FROM [dbo].[TB_R_LOG_H];
	
	INSERT [dbo].[TB_R_LOG_H] (
		[PROCESS_ID],
		[MODULE],
		[FUNCTION], 
		[START_DT], 
		[END_DT], 
		[PROCESS_STATUS], 
		[CREATED_BY], 
		[CREATED_DT]
	) VALUES (
		@ro_v_PROCESS_ID,
		@ri_v_MODULE,
		@ri_v_FUNCTION,
		SYSDATETIME(), 
		SYSDATETIME(), 
		'0', --Start
		@ri_v_USER_ID, 
		SYSDATETIME()
	)
	
	IF @@ROWCOUNT = 1
	BEGIN
		INSERT [dbo].[TB_R_LOG_D] (
			[PROCESS_ID], 
			[SEQ_NO], 
			[MESSAGE_TYPE], 
			[MESSAGE_CONTENT], 
			[LOCATION], 
			[CREATED_BY],
			[CREATED_DT]
		) VALUES (
			@ro_v_PROCESS_ID, 
			1, 
			'INF', 
			'Process Start', 
			@ri_v_LOCATION, 
			@ri_v_USER_ID,
			SYSDATETIME()
		)
	END
	
	RETURN 1;
	
END TRY
BEGIN CATCH 
	PRINT ERROR_MESSAGE();
	RETURN 0;
END CATCH
GO
