-- Preview (read-only, tidak mengubah TB_M_SEQUENCE) untuk mode "Manual" di
-- form Add Document Preparation - membangun prefix nomor dokumen (mis.
-- "ITD/SOP-APP-02") persis dengan logika sp_generate_doc_no, supaya user
-- tahu format yang harus diikuti sebelum mengetik 3 digit angkanya sendiri.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_GetManualNoPrefix]
	@LEVEL_CODE int,
	@DIVISION varchar(255),
	@DEPARTMENT_ID int,
	@SECTION_CODE varchar(50),
	@DOCUMENT_ID varchar(255),
	@PROCESS_CODE varchar(50),
	@COMPANY_CODE varchar(50),
	@DOCUMENT_DATE datetime
AS
BEGIN
	DECLARE @DEPARTMENT_CODE VARCHAR(50), @DOC_CODE VARCHAR(50), @vDocNo VARCHAR(50), @vYear NUMERIC;

	SET @DEPARTMENT_CODE = (SELECT DEPARTMENT_CODE FROM TB_M_DEPARTMENT WHERE DEPARTMENT_ID = @DEPARTMENT_ID);
	SET @DOC_CODE = (SELECT DOCUMENT_CODE FROM TB_M_DOCUMENT WHERE DOCUMENT_ID = @DOCUMENT_ID);

	IF @LEVEL_CODE = 1
	BEGIN
		SET @vYear = YEAR(@DOCUMENT_DATE);
		SET @vDocNo = @COMPANY_CODE + '/' + @DOC_CODE + '/' + CAST(@vYear AS VARCHAR);
	END
	ELSE IF @LEVEL_CODE = 2
	BEGIN
		SET @vDocNo = @COMPANY_CODE + '/' + @DOC_CODE + '/' + @PROCESS_CODE;
	END
	ELSE IF @LEVEL_CODE IN (3, 4)
	BEGIN
		SET @vDocNo = @DIVISION + '/' + @DOC_CODE + '-' + @DEPARTMENT_CODE + '-' + @SECTION_CODE;
	END

	SELECT @vDocNo AS PREFIX;
END
GO
