-- =====================================================================
-- Accept: langkah kedua registrasi P4D, setelah Receive. Receive
-- (sp_P4DMaintenance_ApproveReject, IS_APPROVED='Y') memindahkan status
-- dari Draft (0) ke On Progress (1); Accept memindahkan dari On Progress
-- (1) ke Received (2) - baru dari sini dokumen bisa masuk tahap
-- Distribution/Send. Request user 2026-08-13: registrasi P4D butuh 2
-- langkah terpisah oleh QMS (Receive lalu Accept), bukan satu langkah
-- Approve langsung ke Received seperti sebelumnya.
--
-- Jalankan di database DMS_NEW
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_P4DMaintenance_Accept]
		@DOCUMENT_CTRL_ID int,
		@LOGIN_USER varchar(50),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT

AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT
	, @LOCATION VARCHAR(255) = 'sp_P4DMaintenance_Accept'
	, @cur_STATUS char(1)
	, @cur_DOCUMENT_NO varchar(5)

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'P4D Maintenance', 'Accept', @LOCATION, @LOGIN_USER

	SELECT @cur_STATUS = [STATUS]
		, @cur_DOCUMENT_NO = DOCUMENT_CODE
	FROM TB_R_CTRL_DOCUMENT
	WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID

	IF @cur_STATUS IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data is not found';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @cur_STATUS <> '1'
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document No ' + @cur_DOCUMENT_NO + ' is not in On Progress status, accept is not allowed';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	UPDATE TB_R_CTRL_DOCUMENT
	SET [STATUS] = '2'
		, CHANGED_BY = @LOGIN_USER
		, CHANGED_DT = GETDATE()
	WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID

	SET @RETURN_MSG = 'Successfully Accept Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
