-- =====================================================================
-- Fix: sp_Menu_Delete sebelumnya melakukan hard DELETE tanpa mengecek
-- apakah masih ada Function (TB_M_FUNCTION) atau sub-menu
-- (TB_M_MENU.PARENT_ID) yang menempel ke menu tsb. Kasus nyata:
-- menu "User Dashboard" dihapus lalu dibuat ulang dengan MENU_ID baru
-- untuk pindah parent, tapi 3 Function (Download/Publish/Request) yang
-- masih menunjuk ke MENU_ID lama jadi orphan - checkbox-nya hilang dari
-- halaman Role Authorization untuk semua role.
--
-- Fix: sp_Menu_Delete sekarang menolak penghapusan (RETURN 0, pesan
-- error) kalau menu tsb masih:
--   a) punya Function terdaftar (TB_M_FUNCTION.MENU_ID), atau
--   b) punya sub-menu (TB_M_MENU.PARENT_ID)
-- User harus memindahkan/menghapus Function & sub-menu itu dulu (atau
-- pakai Edit untuk pindah parent, bukan Delete+Add) sebelum menu bisa
-- dihapus.
--
-- Jalankan di database DMS_NEW
-- =====================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_Menu_Delete]
	@MENU_ID VARCHAR(50),
	@LOGIN_USER VARCHAR(255),
	@RETURN_MSG VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;

	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_Menu_Delete';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Menu & Function - Menu', 'Delete', @LOCATION, @LOGIN_USER

	IF EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE MENU_ID = @MENU_ID)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Menu masih memiliki Function terdaftar - hapus/pindahkan Function tersebut dulu sebelum menghapus menu ini';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE PARENT_ID = @MENU_ID)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Menu masih memiliki sub-menu - hapus/pindahkan sub-menu tersebut dulu sebelum menghapus menu ini';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	BEGIN
	DELETE FROM [dbo].[TB_M_MENU]
	WHERE MENU_ID = @MENU_ID

	SET @RETURN_MSG = 'Successfully Delete Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	END
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
