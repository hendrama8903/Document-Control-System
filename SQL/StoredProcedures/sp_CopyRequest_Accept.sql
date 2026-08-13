SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Requester mengonfirmasi sudah menerima copy fisik dokumennya, setelah QMS
-- Approve - pola sama seperti sp_P4DMaintenance_Accept, tapi di sini cukup
-- 1 flag di header karena satu Copy Request = satu requester/department saja
-- (request Hendra 2026-08-13). STATUS tetap '2' (Approved) - tidak di-renumber,
-- ACCEPTED_FLAG jalan terpisah supaya kompatibel dengan data lama.
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_Accept]
	@REQUEST_ID    INT,
	@LOGIN_USER    VARCHAR(255),
	@RETURN_MSG    VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;

	DECLARE @CurrentStatus CHAR(1);
	DECLARE @CurrentAcceptedFlag INT;

	SELECT @CurrentStatus = STATUS, @CurrentAcceptedFlag = ACCEPTED_FLAG
	FROM TB_R_COPY_REQUEST_H
	WHERE REQUEST_ID = @REQUEST_ID AND DELETE_FLAG = 0;

	IF @CurrentStatus IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Request not found';
		RETURN 0;
	END

	IF @CurrentStatus <> '2'
	BEGIN
		SET @RETURN_MSG = 'ERROR: This request has not been Approved by QMS yet';
		RETURN 0;
	END

	IF ISNULL(@CurrentAcceptedFlag, 0) = 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: This request has already been accepted';
		RETURN 0;
	END

	UPDATE [dbo].[TB_R_COPY_REQUEST_H]
	SET ACCEPTED_FLAG = 1,
		ACCEPTED_BY = @LOGIN_USER,
		ACCEPTED_DT = GETDATE(),
		CHANGED_BY = @LOGIN_USER,
		CHANGED_DT = GETDATE()
	WHERE REQUEST_ID = @REQUEST_ID;

	SET @RETURN_MSG = 'Successfully Accepted Request';
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
