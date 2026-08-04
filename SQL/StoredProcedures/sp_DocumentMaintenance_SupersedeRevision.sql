SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_SupersedeRevision]
	@DOCUMENT_TRANSACTION_ID	INT,		-- id revisi yang baru saja selesai disetujui
	@LOGIN_USER					VARCHAR(255),
	@RETURN_MSG					VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @DOCUMENT_CODE VARCHAR(50), @PREV_TRANSACTION_ID INT;

	SELECT @DOCUMENT_CODE = DOCUMENT_CODE
	FROM TB_R_DOCUMENT
	WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID;

	IF @DOCUMENT_CODE IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document not found';
		RETURN 0;
	END

	-- Phase 1 (document lifecycle labels, Jul 2026): STATUS '5' (Published/Effective) is a
	-- terminal state reached AFTER '1' (Approved) once fully distributed - a previous
	-- revision sitting at '5' still needs to be superseded/archived here, same as '1'.
	SELECT TOP 1 @PREV_TRANSACTION_ID = DOCUMENT_TRANSACTION_ID
	FROM TB_R_DOCUMENT
	WHERE DOCUMENT_CODE = @DOCUMENT_CODE
	  AND DOCUMENT_TRANSACTION_ID <> @DOCUMENT_TRANSACTION_ID
	  AND STATUS IN ('1', '5');

	IF @PREV_TRANSACTION_ID IS NOT NULL
	BEGIN
		INSERT INTO [dbo].[TB_R_DOCUMENT_HISTORY]
           ([DOCUMENT_CODE]
           ,[DOCUMENT_TRANSACTION_ID]
           ,[DOCUMENT_TRANSACTION_NAME]
           ,[DOCUMENT_TYPE]
           ,[PROCESS_CODE]
           ,[COMPANY_CODE]
           ,[SECTION_CODE]
           ,[ITEM_CHANGED]
           ,[REASON]
           ,[EXTERNAL_FLAG]
           ,[REFERENCE_NO]
           ,[SOURCE]
           ,[DOCUMENT_DATE]
           ,[FILE_PATH]
           ,[STATUS]
           ,[REVISION]
           ,[APPROVAL_ID]
           ,[LEVEL_CODE]
           ,[DOCUMENT_ID]
           ,[DIVISION]
           ,[DEPARTMENT]
           ,[CLASSIFIED]
           ,[CREATED_BY]
           ,[CREATED_DT]
           ,[CHANGED_BY]
           ,[CHANGED_DT]
           ,[DOCUMENT_CREATOR])
		SELECT [DOCUMENT_CODE]
			   ,[DOCUMENT_TRANSACTION_ID]
			   ,[DOCUMENT_TRANSACTION_NAME]
			   ,[DOCUMENT_TYPE]
			   ,[PROCESS_CODE]
			   ,[COMPANY_CODE]
			   ,[SECTION_CODE]
			   ,[ITEM_CHANGED]
			   ,[REASON]
			   ,[EXTERNAL_FLAG]
			   ,[REFERENCE_NO]
			   ,[SOURCE]
			   ,[DOCUMENT_DATE]
			   ,[FILE_PATH]
			   ,'4' -- OBSOLETE, dipaksa terlepas dari STATUS asal (bisa '1' APPROVED atau '5' PUBLISHED)
			   ,[REVISION]
			   ,[APPROVAL_ID]
			   ,[LEVEL_CODE]
			   ,[DOCUMENT_ID]
				 ,[DIVISION]
				 ,[DEPARTMENT]
				 ,[CLASSIFIED]
			   ,[CREATED_BY]
			   ,[CREATED_DT]
			   ,@LOGIN_USER
			   ,GETDATE()
			   ,[DOCUMENT_CREATOR]
		FROM TB_R_DOCUMENT
		WHERE DOCUMENT_TRANSACTION_ID = @PREV_TRANSACTION_ID;

		DELETE FROM TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @PREV_TRANSACTION_ID;
	END

	SET @RETURN_MSG = 'OK';
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() + ': ' + ERROR_MESSAGE() + ', at line = ' + CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
