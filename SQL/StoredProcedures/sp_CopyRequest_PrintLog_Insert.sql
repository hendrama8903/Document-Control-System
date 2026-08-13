SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Dipanggil PrintTrack (desktop) setelah eksekusi cetak selesai (sukses
-- ataupun gagal). Selalu insert baris audit ke TB_R_COPY_REQUEST_PRINT_LOG;
-- HANYA kalau @PRINT_STATUS='Success' baris TB_R_COPY_REQUEST_D dikunci
-- (PRINT_STATUS='1') supaya tidak bisa dipakai cetak lagi - kalau gagal,
-- token tetap hidup supaya desktop bisa retry tanpa request baru.
-- Divalidasi ulang di sini (bukan cuma dipercaya dari desktop) supaya tidak
-- bisa dilewati: parent request harus Approved (STATUS='2') dan token
-- belum pernah dipakai (PRINT_STATUS='0').
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_PrintLog_Insert]
	@REQUEST_DETAIL_ID INT,
	@COMPUTER_NAME     VARCHAR(100) = NULL,
	@PRINTER_NAME      VARCHAR(200) = NULL,
	@PAGE_COUNT        INT = NULL,
	@COPY_COUNT        INT = NULL,
	@PRINT_JOB_ID      INT = NULL,
	@PRINT_STATUS      VARCHAR(20),
	@ERROR_DETAIL      VARCHAR(MAX) = NULL,
	@LOGIN_USER        VARCHAR(50),
	@RETURN_MSG        VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;

	DECLARE @HeaderStatus CHAR(1), @DetailPrintStatus CHAR(1);

	SELECT @HeaderStatus = H.STATUS, @DetailPrintStatus = D.PRINT_STATUS
	FROM [dbo].[TB_R_COPY_REQUEST_D] D
	INNER JOIN [dbo].[TB_R_COPY_REQUEST_H] H ON H.REQUEST_ID = D.REQUEST_ID
	WHERE D.REQUEST_DETAIL_ID = @REQUEST_DETAIL_ID AND D.DELETE_FLAG = 0 AND H.DELETE_FLAG = 0;

	IF @HeaderStatus IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Request detail not found';
		RETURN 0;
	END

	IF @HeaderStatus <> '2'
	BEGIN
		SET @RETURN_MSG = 'ERROR: Parent request is not Approved';
		RETURN 0;
	END

	IF @DetailPrintStatus = '1'
	BEGIN
		SET @RETURN_MSG = 'ERROR: This document has already been printed - submit a new request to reprint';
		RETURN 0;
	END

	INSERT INTO [dbo].[TB_R_COPY_REQUEST_PRINT_LOG]
		(REQUEST_DETAIL_ID, COMPUTER_NAME, PRINTER_NAME, PAGE_COUNT, COPY_COUNT, PRINT_JOB_ID, PRINT_STATUS, ERROR_DETAIL, PRINTED_BY, PRINTED_DT)
	VALUES
		(@REQUEST_DETAIL_ID, @COMPUTER_NAME, @PRINTER_NAME, @PAGE_COUNT, @COPY_COUNT, @PRINT_JOB_ID, @PRINT_STATUS, @ERROR_DETAIL, @LOGIN_USER, GETDATE());

	IF @PRINT_STATUS = 'Success'
	BEGIN
		UPDATE [dbo].[TB_R_COPY_REQUEST_D]
		SET PRINT_STATUS = '1',
			PRINTED_BY = @LOGIN_USER,
			PRINTED_DT = GETDATE(),
			CHANGED_BY = @LOGIN_USER,
			CHANGED_DT = GETDATE()
		WHERE REQUEST_DETAIL_ID = @REQUEST_DETAIL_ID;
	END

	SET @RETURN_MSG = 'Print log recorded';
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
