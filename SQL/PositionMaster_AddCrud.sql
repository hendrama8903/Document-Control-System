-- =====================================================================
-- Position Master saat ini cuma bisa dibaca (sp_PositionMaster_Search /
-- sp_PositionMaster_GetByKey ada, tapi tidak ada Insert/Update/Delete)
-- dan tidak punya halaman admin sama sekali - beda dengan Department,
-- Section, dan Division yang masing-masing punya Controller + View CRUD
-- lengkap. Data TB_M_POSITION (Staff, Section Head, Dept. Head, dst)
-- selama ini hanya bisa diubah langsung lewat database.
--
-- Fix: tambah 3 stored procedure (Insert/Update/Delete) mengikuti pola
-- sp_SectionMaster_* / sp_DivisionMaster_* (validasi wajib isi, cek
-- duplikat, soft delete via DELETE_FLAG), plus daftarkan menu "Position
-- Master" di bawah "Master Data" (M00002) dan Function POSITIONMASTER-
-- ADD/EDIT/DELETE, di-grant ke role Admin (DMS-ADMIN) dulu supaya bisa
-- langsung dites lalu di-share ke role lain lewat halaman Role
-- Authorization.
--
-- Duplikat hanya dicek berdasarkan POSITION_NAME. POSITION_LEVEL SENGAJA
-- BOLEH berulang di beberapa nama posisi - TB_M_WORKFLOW_DOC_H memetakan
-- rute approval berdasarkan CREATOR_LEVEL (angka), bukan nama/ID posisi,
-- jadi ini memang dipakai untuk kasus "grade sama, sebutan beda" (mis.
-- grade 6 = Supervisor di satu bagian, Specialist di bagian lain; atau
-- Dept. Head yang di beberapa departemen disebut Coordinator) - keduanya
-- otomatis ikut alur approval yang sama selama POSITION_LEVEL-nya sama.
-- POSITION_ID == 5 tetap di-hardcode sebagai Executive Officer di kode
-- C#, jadi khusus ID itu jangan diubah/dihapus.
--
-- Catatan: menu di sidebar dan Function (tombol Add/Edit/dst) diatur
-- oleh DUA tabel otorisasi terpisah - TB_M_AUTH_MENU (menu apa yang
-- tampil di sidebar per role) dan TB_M_AUTH_FUNCTION (tombol apa yang
-- aktif per role). Menu baru harus di-grant ke KEDUANYA.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_PositionMaster_Insert]
	@POSITION_NAME 			VARCHAR(255),
	@POSITION_LEVEL 		INT,
	@LOGIN_USER 				VARCHAR(255),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_PositionMaster_Insert';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Position Master', 'Insert', @LOCATION, @LOGIN_USER

	IF @POSITION_NAME IS NULL OR LEN(@POSITION_NAME) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position Name should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @POSITION_LEVEL IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position Level should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_POSITION]
	WHERE [POSITION_NAME] = @POSITION_NAME
	AND ISNULL([DELETE_FLAG], 0) != 1)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data already exist Position Name = ' + @POSITION_NAME;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	INSERT INTO [dbo].[TB_M_POSITION] (
		POSITION_NAME,
		POSITION_LEVEL,
		DELETE_FLAG,
		CREATED_DT,
		CREATED_BY,
		CHANGED_DT,
		CHANGED_BY
	) VALUES (
		@POSITION_NAME,
		@POSITION_LEVEL,
		0,
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

CREATE OR ALTER PROCEDURE [dbo].[sp_PositionMaster_Update]
	@POSITION_ID				INT,
	@POSITION_NAME 			VARCHAR(255),
	@POSITION_LEVEL 		INT,
	@LOGIN_USER 				VARCHAR(255),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_PositionMaster_Update';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Position Master', 'Update', @LOCATION, @LOGIN_USER

	IF @POSITION_NAME IS NULL OR LEN(@POSITION_NAME) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position Name should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @POSITION_LEVEL IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position Level should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_POSITION]
	WHERE [POSITION_NAME] = @POSITION_NAME
	AND [POSITION_ID] != @POSITION_ID
	AND ISNULL([DELETE_FLAG], 0) != 1)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data already exist Position Name = ' + @POSITION_NAME;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	UPDATE [dbo].[TB_M_POSITION]
	SET POSITION_NAME 	= @POSITION_NAME,
			POSITION_LEVEL = @POSITION_LEVEL,
			CHANGED_DT 			= GETDATE(),
			CHANGED_BY 			= @LOGIN_USER
	WHERE POSITION_ID = @POSITION_ID

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

CREATE OR ALTER PROCEDURE [dbo].[sp_PositionMaster_Delete]
	@POSITION_ID	INT,
	@LOGIN_USER 	VARCHAR(255),
	@RETURN_MSG 	VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_PositionMaster_Delete';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Position Master', 'Delete', @LOCATION, @LOGIN_USER

	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_USER_POS] WHERE POSITION_ID = @POSITION_ID)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position is still assigned to one or more users, cannot be deleted';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	UPDATE [dbo].[TB_M_POSITION]
	SET DELETE_FLAG 	= 1,
			CHANGED_DT 		= GETDATE(),
			CHANGED_BY 		= @LOGIN_USER
	WHERE [POSITION_ID] = @POSITION_ID

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

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00002-10')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT)
	VALUES ('M00002-10', 'M00002', 'Position Master', 'fa-address-card-o', '/PositionMaster/Index', 8, 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'POSITIONMASTER-ADD')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('POSITIONMASTER-ADD', 'M00002-10', 'Add Position Master', 'Add Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'POSITIONMASTER-EDIT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('POSITIONMASTER-EDIT', 'M00002-10', 'Edit Position Master', 'Edit Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'POSITIONMASTER-DELETE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('POSITIONMASTER-DELETE', 'M00002-10', 'Delete Position Master', 'Delete Function', 'dms.admin', GETDATE());
END

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT 'DMS-ADMIN', f.FUNCTION_ID, 'dms.admin', GETDATE()
FROM (VALUES
	('POSITIONMASTER-ADD'),
	('POSITIONMASTER-EDIT'),
	('POSITIONMASTER-DELETE')
) AS f(FUNCTION_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] a
	WHERE a.ROLE_ID = 'DMS-ADMIN' AND a.FUNCTION_ID = f.FUNCTION_ID
);

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] WHERE ROLE_ID = 'DMS-ADMIN' AND MENU_ID = 'M00002-10')
BEGIN
	INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'M00002-10', 'dms.admin', GETDATE());
END
