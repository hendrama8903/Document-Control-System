-- =====================================================================
-- Un-Receive: kebalikan dari Receive (ApproveReject dengan IS_APPROVED='Y').
-- Dipakai kalau QMS salah klik Approve/Receive dan perlu membatalkannya
-- sebelum lanjut ke tahap distribusi.
--
-- Hanya boleh dari STATUS = 2 (RECEIVED) kembali ke STATUS = 0 (DRAFT), dan
-- HANYA kalau belum ada baris TB_R_DOCUMENT_DISTRIBUTION sama sekali untuk
-- dokumen ini (baik yang sudah dikirim maupun yang baru di-assign tapi belum
-- dikirim) - supaya tidak meninggalkan data distribusi yatim. Kalau sudah ada
-- distribusi, user harus hapus assignment distribusi itu dulu (fitur Delete
-- Distribution yang sudah ada) sebelum bisa Un-Receive.
--
-- Jalankan di database DMS_NEW
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_P4DMaintenance_UnReceive]
		@DOCUMENT_CTRL_ID int,
		@LOGIN_USER varchar(50),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT

AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT
	, @LOCATION VARCHAR(255) = 'sp_P4DMaintenance_UnReceive'
	, @cur_STATUS char(1)
	, @cur_DOCUMENT_NO varchar(5)
	, @cur_DOCUMENT_TRANSACTION_ID int

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'P4D Maintenance', 'UnReceive', @LOCATION, @LOGIN_USER

	SELECT @cur_STATUS = [STATUS]
		, @cur_DOCUMENT_NO = DOCUMENT_CODE
		, @cur_DOCUMENT_TRANSACTION_ID = DOCUMENT_TRANSACTION_ID
	FROM TB_R_CTRL_DOCUMENT
	WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID

	IF @cur_STATUS IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data is not found';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @cur_STATUS <> '2'
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document No ' + @cur_DOCUMENT_NO + ' is not in Received status, un-receive is not allowed';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF EXISTS(
		SELECT 1 FROM TB_R_DOCUMENT_DISTRIBUTION
		WHERE DOCUMENT_TRANSACTION_ID = @cur_DOCUMENT_TRANSACTION_ID
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document No ' + @cur_DOCUMENT_NO + ' already has distribution assigned - remove the distribution first before un-receive';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	UPDATE TB_R_CTRL_DOCUMENT
	SET [STATUS] = '0'
		, CHANGED_BY = @LOGIN_USER
		, CHANGED_DT = GETDATE()
	WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID

	SET @RETURN_MSG = 'Successfully Un-Receive Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
