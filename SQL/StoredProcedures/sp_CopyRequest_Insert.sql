SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Insert cuma bikin baris Draft (STATUS='0') - REQUEST_NO baru dibuat di
-- sp_CopyRequest_Submit begitu di-submit for approval (beda dari
-- sp_DocumentSubmission_Insert yang sekarang menerbitkan nomor langsung
-- saat Save - itu peninggalan simplifikasi Agu 2026 yang bikin nomor bisa
-- dobel-generate kalau Submit tetap dipanggil; di modul ini Submit tetap
-- jadi satu-satunya titik penerbitan nomor).
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_Insert]
	@DIVISION          VARCHAR(50),
	@DEPARTMENT_ID     INT,
	@SECTION_CODE      VARCHAR(5) = NULL,
	@REQUEST_DATE      DATETIME,
	@DOC_CATEGORY      VARCHAR(200) = NULL,
	@REMARK            VARCHAR(MAX) = NULL,
	@REQUESTED_BY      VARCHAR(255),
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

	INSERT INTO [dbo].[TB_R_COPY_REQUEST_H] (
		DIVISION, DEPARTMENT_ID, SECTION_CODE, REQUEST_DATE, DOC_CATEGORY,
		REMARK, STATUS, REQUESTED_BY, DELETE_FLAG, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT
	) VALUES (
		@DIVISION, @DEPARTMENT_ID, @SECTION_CODE, @REQUEST_DATE, @DOC_CATEGORY,
		@REMARK, '0', @REQUESTED_BY, 0, @LOGIN_USER, GETDATE(), @LOGIN_USER, GETDATE()
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
