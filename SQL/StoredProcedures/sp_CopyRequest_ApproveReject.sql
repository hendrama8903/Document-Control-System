SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Approve/Reject satu langkah oleh QMS (fungsi COPYREQUEST-APPROVE) -
-- tidak ada chain internal berjenjang, pola sama seperti
-- sp_P4DMaintenance_ApproveReject. Fulfillment (bikin baris
-- TB_R_CTRL_DOCUMENT) TETAP jadi tanggung jawab
-- CopyRequestController.ApproveReject, bukan SP ini.
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_ApproveReject]
	@REQUEST_ID    INT,
	@IS_APPROVED   CHAR(1),
	@REMARK        VARCHAR(MAX) = NULL,
	@LOGIN_USER    VARCHAR(255),
	@RETURN_MSG    VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;

	DECLARE @CurrentStatus CHAR(1);
	SELECT @CurrentStatus = STATUS
	FROM TB_R_COPY_REQUEST_H
	WHERE REQUEST_ID = @REQUEST_ID AND DELETE_FLAG = 0;

	IF @CurrentStatus IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Request not found';
		RETURN 0;
	END

	IF @CurrentStatus <> '1'
	BEGIN
		SET @RETURN_MSG = 'ERROR: This request is not waiting for approval';
		RETURN 0;
	END

	UPDATE [dbo].[TB_R_COPY_REQUEST_H]
	SET STATUS = IIF(@IS_APPROVED = 'Y', '2', '3'),
		APPROVED_BY = @LOGIN_USER,
		APPROVED_DT = GETDATE(),
		APPROVAL_REMARK = @REMARK,
		CHANGED_BY = @LOGIN_USER,
		CHANGED_DT = GETDATE()
	WHERE REQUEST_ID = @REQUEST_ID;

	SET @RETURN_MSG = 'Successfully ' + IIF(@IS_APPROVED = 'Y', 'Approved', 'Rejected') + ' Request';
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
