SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Submit: kunci daftar dokumen jadi read-only, generate SUBMISSION_NO, dan
-- bikin approval chain 3-tingkat lewat sp_WorkflowDoc_Create (engine
-- approval generik yang sama dipakai Document Preparation) di
-- DOCUMENT_LEVEL = 9 - lihat Menu_Add_DocumentSubmissionForm.sql untuk
-- seed chain-nya. Urutan langkah ini meniru persis
-- sp_DocumentMaintenance_Insert.sql (panggil sp_WorkflowDoc_Create lalu
-- link balik TRANSACTION_ID).
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentSubmission_Submit]
	@SUBMISSION_ID INT,
	@LOGIN_USER    VARCHAR(255),
	@RETURN_MSG    VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;

	DECLARE @CurrentStatus CHAR(1), @DocumentCreator VARCHAR(255);
	SELECT @CurrentStatus = STATUS, @DocumentCreator = DOCUMENT_CREATOR
	FROM TB_R_DOC_SUBMISSION_FORM_H
	WHERE SUBMISSION_ID = @SUBMISSION_ID AND DELETE_FLAG = 0;

	IF @CurrentStatus IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Submission not found';
		RETURN 0;
	END

	IF @CurrentStatus <> '0'
	BEGIN
		SET @RETURN_MSG = 'ERROR: This submission has already been submitted';
		RETURN 0;
	END

	IF (SELECT COUNT(*) FROM TB_R_DOC_SUBMISSION_FORM_D WHERE SUBMISSION_ID = @SUBMISSION_ID AND DELETE_FLAG = 0) = 0
	BEGIN
		SET @RETURN_MSG = 'ERROR: At least one document row is required before submitting';
		RETURN 0;
	END

	BEGIN TRANSACTION;

	-- Nomor form: FPD/yyyy/MM/0001, satu counter per bulan
	DECLARE @SeqCode VARCHAR(50) = 'FPD/' + CONVERT(VARCHAR(4), YEAR(GETDATE())) + '/' + RIGHT('0' + CONVERT(VARCHAR(2), MONTH(GETDATE())), 2);
	DECLARE @SeqNo INT;
	EXEC [dbo].[sp_GetNextSeqNo] @SEQ_TYPE = 'DOC_SUBMISSION_NO', @SEQ_CODE = @SeqCode, @LOGIN_USER = @LOGIN_USER, @p_seq_no = @SeqNo OUTPUT;
	DECLARE @SubmissionNo VARCHAR(50) = @SeqCode + '/' + RIGHT('0000' + CONVERT(VARCHAR(4), @SeqNo), 4);

	-- Bikin approval chain
	DECLARE @WorkflowMsg VARCHAR(MAX), @WorkflowApprovalId VARCHAR(MAX), @WorkflowRetVal INT;
	EXEC @WorkflowRetVal = [dbo].[sp_WorkflowDoc_Create]
		@DOCUMENT_LEVEL = 9,
		@LOGIN_USER = @LOGIN_USER,
		@REMARK = NULL,
		@MENU_ID = 'M00006-09',
		@DOCUMENT_CREATOR = @DocumentCreator,
		@RETURN_MSG = @WorkflowMsg OUTPUT,
		@RETURN_ID = @WorkflowApprovalId OUTPUT;

	IF @WorkflowRetVal <> 1
	BEGIN
		ROLLBACK TRANSACTION;
		SET @RETURN_MSG = @WorkflowMsg;
		RETURN 0;
	END

	DECLARE @ApprovalId INT = CAST(@WorkflowApprovalId AS INT);

	UPDATE [dbo].[TB_R_DOC_SUBMISSION_FORM_H]
	SET APPROVAL_ID = @ApprovalId,
		SUBMISSION_NO = @SubmissionNo,
		STATUS = '1',
		SUBMITTED_BY = @LOGIN_USER,
		SUBMITTED_DT = GETDATE(),
		CHANGED_BY = @LOGIN_USER,
		CHANGED_DT = GETDATE()
	WHERE SUBMISSION_ID = @SUBMISSION_ID;

	-- Link balik, sama seperti sp_DocumentMaintenance_Insert
	UPDATE [dbo].[TB_R_APPROVAL_H]
	SET TRANSACTION_ID = @SUBMISSION_ID
	WHERE APPROVAL_ID = @ApprovalId;

	-- Kalau pembuat = Dept Head (chain 1 langkah), sp_WorkflowDoc_Create sudah
	-- auto-approve langkah itu sendiri - cek APPROVAL_STATUS, kalau sudah 1
	-- (Approved) samakan STATUS form ini jadi Approved juga.
	IF (SELECT APPROVAL_STATUS FROM TB_R_APPROVAL_H WHERE APPROVAL_ID = @ApprovalId) = 1
	BEGIN
		UPDATE [dbo].[TB_R_DOC_SUBMISSION_FORM_H]
		SET STATUS = '2', CHANGED_BY = @LOGIN_USER, CHANGED_DT = GETDATE()
		WHERE SUBMISSION_ID = @SUBMISSION_ID;
	END

	COMMIT TRANSACTION;

	SET @RETURN_MSG = 'Successfully Submitted as ' + @SubmissionNo;
	RETURN 1;
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
