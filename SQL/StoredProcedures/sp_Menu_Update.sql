SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Menu_Update]
	@MENU_ID VARCHAR(50),
	@PARENT_ID VARCHAR(50),
	@MENU_NAME VARCHAR(255), 
	@MENU_ICON VARCHAR(255), 
	@MENU_URL VARCHAR(255), 
	@MENU_SEQ INT, 
	@LOGIN_USER VARCHAR(255),
	@RETURN_MSG VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_Menu_Update';
					
	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Menu & Function - Menu', 'Update', @LOCATION, @LOGIN_USER	
	
	IF @MENU_ID IS NULL OR LEN(@MENU_ID) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Menu ID should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	IF @MENU_NAME IS NULL OR LEN(@MENU_NAME) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Menu Name should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	IF @MENU_ICON IS NULL OR LEN(@MENU_ICON) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Menu Icon should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	IF @MENU_URL IS NULL OR LEN(@MENU_URL) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Menu URL should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	IF @MENU_SEQ IS NULL OR LEN(@MENU_SEQ) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Sequence should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	-- Pindah Parent <-> Sub Menu lewat Edit (request Hendra 2026-11-09). Menu hanya dua
	-- tingkat: parent tujuan harus menu utama, dan menu yang masih punya submenu tidak
	-- boleh dijadikan submenu.
	IF LEN(ISNULL(@PARENT_ID, '')) = 0 SET @PARENT_ID = NULL;
	IF @PARENT_ID IS NOT NULL
	BEGIN
		SET @RETURN_MSG = CASE
			WHEN @PARENT_ID = @MENU_ID THEN 'ERROR: A menu cannot be its own parent.'
			WHEN NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE [MENU_ID] = @PARENT_ID AND [PARENT_ID] IS NULL)
				THEN 'ERROR: Parent Menu must be a main menu, not a sub menu.'
			WHEN EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE [PARENT_ID] = @MENU_ID)
				THEN 'ERROR: This menu still has sub menus. Move or delete its sub menus first, then make it a sub menu.'
			WHEN @MENU_URL = '#' THEN 'ERROR: A sub menu needs a page URL (for example /Report/Index), not #.'
		END;
		IF @RETURN_MSG IS NOT NULL
		BEGIN
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
			RETURN 0;
		END
	END
	IF @PARENT_ID IS NULL
	BEGIN
		IF EXISTS ( SELECT TOP 1 1 FROM [dbo].[TB_M_MENU] 
								WHERE [PARENT_ID] IS NULL
								AND [MENU_SEQ] = @MENU_SEQ
								AND [MENU_ID] <> @MENU_ID )
		BEGIN
				SET @RETURN_MSG = 'ERROR: Sequence No ' + CAST(@MENU_SEQ AS VARCHAR) + ' already exist';
				EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
				RETURN 0;
		END
	END
	ELSE BEGIN
		IF EXISTS ( SELECT TOP 1 1 FROM [dbo].[TB_M_MENU] 
								WHERE [PARENT_ID] = @PARENT_ID 
								AND [MENU_SEQ] = @MENU_SEQ
								AND [MENU_ID] <> @MENU_ID )
		BEGIN
				SET @RETURN_MSG = 'ERROR: Sequence No ' + CAST(@MENU_SEQ AS VARCHAR) + ' already exist in parent ' + @PARENT_ID;
				EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
				RETURN 0;
		END
	END
		
	UPDATE [dbo].[TB_M_MENU]
	SET PARENT_ID = @PARENT_ID,
			MENU_NAME = @MENU_NAME, 
			MENU_ICON = @MENU_ICON, 
			MENU_URL = @MENU_URL, 
			MENU_SEQ = @MENU_SEQ, 
			CHANGED_DT = GETDATE(),
			CHANGED_BY = @LOGIN_USER
	WHERE MENU_ID = @MENU_ID

	-- Sidebar hanya menampilkan submenu kalau parent-nya juga diizinkan untuk role itu
	-- (sp_Auth_Menu). Role yang punya akses ke menu ini ikut diberi akses ke parent barunya.
	IF @PARENT_ID IS NOT NULL
		INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
		SELECT A.ROLE_ID, @PARENT_ID, @LOGIN_USER, GETDATE()
		FROM [dbo].[TB_M_AUTH_MENU] A
		WHERE A.MENU_ID = @MENU_ID
			AND NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] X WHERE X.ROLE_ID = A.ROLE_ID AND X.MENU_ID = @PARENT_ID);

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
