SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Submit: kunci daftar dokumen jadi read-only, generate REQUEST_NO, dan
-- pindah ke status Waiting Approval. Approval-nya satu langkah oleh QMS
-- (fungsi COPYREQUEST-APPROVE) - lihat sp_CopyRequest_ApproveReject,
-- bukan chain internal berjenjang seperti Document Submission
-- (keputusan Hendra 2026-08-11, lihat SQL/CopyRequest_SimplifyApproval.sql).
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_Submit]
	@REQUEST_ID    INT,
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

	IF @CurrentStatus <> '0'
	BEGIN
		SET @RETURN_MSG = 'ERROR: This request has already been submitted';
		RETURN 0;
	END

	IF (SELECT COUNT(*) FROM TB_R_COPY_REQUEST_D WHERE REQUEST_ID = @REQUEST_ID AND DELETE_FLAG = 0) = 0
	BEGIN
		SET @RETURN_MSG = 'ERROR: At least one document row is required before submitting';
		RETURN 0;
	END

	-- Nomor form: CCR/yyyy/MM/0001, satu counter per bulan
	DECLARE @SeqCode VARCHAR(50) = 'CCR/' + CONVERT(VARCHAR(4), YEAR(GETDATE())) + '/' + RIGHT('0' + CONVERT(VARCHAR(2), MONTH(GETDATE())), 2);
	DECLARE @SeqNo INT;
	EXEC [dbo].[sp_GetNextSeqNo] @SEQ_TYPE = 'COPY_REQUEST_NO', @SEQ_CODE = @SeqCode, @LOGIN_USER = @LOGIN_USER, @p_seq_no = @SeqNo OUTPUT;
	DECLARE @RequestNo VARCHAR(50) = @SeqCode + '/' + RIGHT('0000' + CONVERT(VARCHAR(4), @SeqNo), 4);

	UPDATE [dbo].[TB_R_COPY_REQUEST_H]
	SET REQUEST_NO = @RequestNo,
		STATUS = '1',
		SUBMITTED_BY = @LOGIN_USER,
		SUBMITTED_DT = GETDATE(),
		CHANGED_BY = @LOGIN_USER,
		CHANGED_DT = GETDATE()
	WHERE REQUEST_ID = @REQUEST_ID;

	SET @RETURN_MSG = 'Successfully Submitted as ' + @RequestNo;
	RETURN 1;
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
