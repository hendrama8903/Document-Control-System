SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_User_Insert]
  @USERNAME VARCHAR(255),
  @REG_NO VARCHAR(50),
	@FULL_NAME VARCHAR(255),
	@PASSWORD VARCHAR(255),
	@CONFIRM_PASSWORD VARCHAR(255),
	@EMAIL VARCHAR(255),
	@PHONE VARCHAR(15),
	@ROLE_ID VARCHAR(50),
	@DEPARTMENT_ID INT,
	@FILE_PATH 	VARCHAR(255),
	@AD_USER CHAR(1) = '0',
	@LOGIN_USER VARCHAR(255),
	@RETURN_MSG VARCHAR(MAX) OUTPUT
AS
BEGIN TRY

	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_User_Insert';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'User Management', 'Insert', @LOCATION, @LOGIN_USER

	IF @USERNAME IS NULL OR LEN(@USERNAME) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Username should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	-- Username yang masih AKTIF saja yang dianggap "sudah dipakai" (request
	-- Hendra 2026-08-21) - username yang pernah ada tapi sudah di-nonaktifkan
	-- (soft-delete, DELETE_FLAG=1) tidak lagi memblokir Add di sini; baris
	-- lama itu justru dihidupkan ulang lewat UPDATE di bawah (PK tidak
	-- mengizinkan INSERT baris kedua dengan USERNAME yang sama).
	IF EXISTS(SELECT TOP 1 1 FROM [dbo].[TB_M_USER] WHERE USERNAME = @USERNAME AND ISNULL(DELETE_FLAG, '0') = '0')
 	BEGIN
 			SET @RETURN_MSG = 'ERROR: Username Already Exist';
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
 			RETURN 0;
 	END

	IF @REG_NO IS NULL OR LEN(@REG_NO) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Reg No should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @FULL_NAME IS NULL OR LEN(@FULL_NAME) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Full Name should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	-- Password hanya wajib untuk user non-AD. User AD login pakai kredensial domain, bukan password lokal.
	IF ISNULL(@AD_USER, '0') <> '1'
	BEGIN
		IF @PASSWORD IS NULL OR LEN(@PASSWORD) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Password not be null';
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
			RETURN 0;
		END

		IF @CONFIRM_PASSWORD IS NULL OR LEN(@CONFIRM_PASSWORD) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Confirm Password should not be null';
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
			RETURN 0;
		END

		IF @CONFIRM_PASSWORD <> @PASSWORD
		BEGIN
			SET @RETURN_MSG = 'ERROR: Confirm Password should be same with Password';
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
			RETURN 0;
		END
	END

	IF @EMAIL IS NULL OR LEN(@EMAIL) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Email should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @EMAIL NOT LIKE '%_@__%.__%'
	BEGIN
		SET @RETURN_MSG = 'ERROR: Invalid Email Address';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

-- 	IF @PHONE IS NULL OR LEN(@PHONE) < 1
-- 	BEGIN
-- 		SET @RETURN_MSG = 'ERROR: Phone should not be null';
-- 		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
-- 		RETURN 0;
-- 	END

-- 	IF @DEPARTMENT_ID IS NULL OR LEN(@DEPARTMENT_ID) < 1
-- 	BEGIN
-- 		SET @RETURN_MSG = 'ERROR: Division and Department should not be null';
-- 		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
-- 		RETURN 0;
-- 	END


	-- Username sama pernah ada tapi sudah di-nonaktifkan (soft-delete) -
	-- "Add" dengan username itu dianggap menghidupkan kembali baris LAMA
	-- (UPDATE), bukan bikin baris baru (PK TB_M_USER tidak mengizinkan itu).
	-- CREATED_BY/CREATED_DT SENGAJA tidak diubah - tetap mencatat kapan &
	-- oleh siapa username ini pertama kali dibuat, sama seperti perilaku
	-- sp_User_Restore (request Hendra 2026-08-21). Posisi (TB_M_USER_POS)
	-- juga TIDAK ikut dipulihkan di sini - sama seperti Restore, harus
	-- di-assign ulang manual.
	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_USER] WHERE USERNAME = @USERNAME AND ISNULL(DELETE_FLAG, '0') = '1')
	BEGIN
		UPDATE [dbo].[TB_M_USER]
		SET REG_NO = @REG_NO,
			FULL_NAME = @FULL_NAME,
			PASSWORD = CASE WHEN ISNULL(@AD_USER, '0') = '1' THEN NULL ELSE @PASSWORD END,
			EMAIL = @EMAIL,
			PHONE = @PHONE,
			ROLE_ID = @ROLE_ID,
			FILE_PATH = @FILE_PATH,
			AD_USER = ISNULL(@AD_USER, '0'),
			DELETE_FLAG = '0',
			CHANGED_BY = @LOGIN_USER,
			CHANGED_DT = GETDATE()
		WHERE USERNAME = @USERNAME;
	END
	ELSE
	BEGIN
		INSERT INTO [dbo].[TB_M_USER] (
			USERNAME,
			REG_NO,
			FULL_NAME,
			PASSWORD,
			EMAIL,
			PHONE,
			ROLE_ID,
	-- 		DEPARTMENT_ID,
			FILE_PATH,
			AD_USER,
			CREATED_DT,
			CREATED_BY
		) VALUES (
			@USERNAME,
			@REG_NO,
			@FULL_NAME,
			CASE WHEN ISNULL(@AD_USER, '0') = '1' THEN NULL ELSE @PASSWORD END,
			@EMAIL,
			@PHONE,
			@ROLE_ID,
			@FILE_PATH,
	-- 		@DEPARTMENT_ID,
			ISNULL(@AD_USER, '0'),
			GETDATE(),
			@LOGIN_USER
		)
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
