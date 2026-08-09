SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentSubmission_Insert]
	@DIVISION          VARCHAR(50),
	@DEPARTMENT_ID     INT,
	@SECTION_CODE      VARCHAR(5) = NULL,
	@SUBMISSION_DATE   DATETIME,
	@DOC_CATEGORY      VARCHAR(200) = NULL,
	@REMARK            VARCHAR(MAX) = NULL,
	@DOCUMENT_CREATOR  VARCHAR(255),
	@LOGIN_USER        VARCHAR(255),
	@RETURN_MSG        VARCHAR(MAX) OUTPUT,
	@RETURN_ID         VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	SET @RETURN_ID = 0;

	IF @DEPARTMENT_ID IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Department should not be null';
		RETURN 0;
	END

	-- Nomor form: FPD/yyyy/MM/0001, satu counter per bulan - langsung
	-- diterbitkan saat Save. Sebelumnya nomor ini baru dibuat lewat step
	-- Submit terpisah (sp_DocumentSubmission_Submit), tapi modul ini
	-- sekarang tidak pakai approval digital lagi (request user 2026-08-08:
	-- cukup Save lalu langsung preview PDF, tanpa Submit for Approval).
	DECLARE @SeqCode VARCHAR(50) = 'FPD/' + CONVERT(VARCHAR(4), YEAR(GETDATE())) + '/' + RIGHT('0' + CONVERT(VARCHAR(2), MONTH(GETDATE())), 2);
	DECLARE @SeqNo INT;
	EXEC [dbo].[sp_GetNextSeqNo] @SEQ_TYPE = 'DOC_SUBMISSION_NO', @SEQ_CODE = @SeqCode, @LOGIN_USER = @LOGIN_USER, @p_seq_no = @SeqNo OUTPUT;
	DECLARE @SubmissionNo VARCHAR(50) = @SeqCode + '/' + RIGHT('0000' + CONVERT(VARCHAR(4), @SeqNo), 4);

	INSERT INTO [dbo].[TB_R_DOC_SUBMISSION_FORM_H] (
		SUBMISSION_NO, DIVISION, DEPARTMENT_ID, SECTION_CODE, SUBMISSION_DATE, DOC_CATEGORY,
		REMARK, STATUS, DOCUMENT_CREATOR, DELETE_FLAG, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT
	) VALUES (
		@SubmissionNo, @DIVISION, @DEPARTMENT_ID, @SECTION_CODE, @SUBMISSION_DATE, @DOC_CATEGORY,
		@REMARK, '0', @DOCUMENT_CREATOR, 0, @LOGIN_USER, GETDATE(), @LOGIN_USER, GETDATE()
	)

	SET @RETURN_ID = SCOPE_IDENTITY();
	SET @RETURN_MSG = 'Successfully Save Data';
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
