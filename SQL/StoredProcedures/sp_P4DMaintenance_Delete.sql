SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_P4DMaintenance_Delete]
		@DOCUMENT_CTRL_ID int,
		@DOCUMENT_NO varchar(5),
		@LOGIN_USER varchar(50),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT

AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT
	, @LOCATION VARCHAR(255) = 'sp_P4DMaintenance_Delete'
	, @rev_DOCUMENT_ID int
	, @rev_REVISION int
					
	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'P4D Maintenance', 'Delete', @LOCATION, @LOGIN_USER

	-- Checking (1) SENGAJA tidak boleh dihapus - QMS sudah mulai Receive/review
	-- dokumen ini, dan Delete di bawah menghapus PERMANEN (bukan soft-delete)
	-- log aktivitas & data distribusinya, jadi kalau dibolehkan di tengah
	-- proses QMS jejaknya hilang tanpa audit trail dan tanpa QMS tahu. Department
	-- harus Un-Receive dulu (balik ke Draft) baru bisa Delete (request Hendra
	-- 2026-08-14).
	IF EXISTS(
		SELECT 1
		FROM TB_R_CTRL_DOCUMENT
		WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID
			AND [STATUS] NOT IN ('0', '3')
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Only Document with status Draft or Rejected could be deleted. If this document is Checking, Un-Receive it first.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	UPDATE TB_R_CTRL_DOCUMENT
	SET [STATUS] = '4'
		, CHANGED_BY = @LOGIN_USER
		, CHANGED_DT = GETDATE()
	WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID
	
	UPDATE TB_R_CTRL_DOCUMENT
	SET [DELETE_FLAG] = '1'
		, CHANGED_BY = @LOGIN_USER
		, CHANGED_DT = GETDATE()
	WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID
	
	DELETE FROM TB_R_DOCUMENT_LOG WHERE DOCUMENT_TRANSACTION_ID = (SELECT DOCUMENT_TRANSACTION_ID FROM TB_R_CTRL_DOCUMENT WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID)
	DELETE FROM TB_R_DOCUMENT_DISTRIBUTION WHERE DOCUMENT_TRANSACTION_ID = (SELECT DOCUMENT_TRANSACTION_ID FROM TB_R_CTRL_DOCUMENT WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID)

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
