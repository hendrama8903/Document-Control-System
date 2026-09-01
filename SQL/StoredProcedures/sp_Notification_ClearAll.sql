SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Notification_ClearAll]
	@USERNAME VARCHAR(50),
	@LOGIN_USER VARCHAR(255),
	@RETURN_MSG VARCHAR(MAX) OUTPUT
AS
BEGIN TRY

	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_Notification_ClearAll';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Notification', 'DELETE', @LOCATION, @LOGIN_USER

	-- Notifikasi tidak punya DELETE_FLAG (hapus permanen, bukan soft delete) -
	-- datanya murni transient/disposable, beda dari dokumen/transaksi bisnis
	-- lain di aplikasi ini. Selalu di-scope ke @USERNAME milik yang login,
	-- tidak pernah menghapus notifikasi user lain (lihat juga
	-- sp_Notification_Search - pola scoping yang sama).
	DELETE FROM [dbo].[TB_R_NOTIFICATION]
	WHERE USERNAME = @USERNAME

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
