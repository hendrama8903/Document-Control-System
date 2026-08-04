SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_Update]
		@DOCUMENT_TRANSACTION_ID int,
		@DOCUMENT_CODE varchar(5),
		@DOCUMENT_NO varchar(5),
		@DOCUMENT_TRANSACTION_NAME varchar(255),
		@DOCUMENT_TYPE varchar(50),
		@PROCESS_CODE varchar(50),
		@COMPANY_CODE varchar(50),
		@DEPARTMENT_CODE varchar(50),
		@SECTION_CODE varchar(50),
		@ITEM_CHANGED varchar(255),
		@REASON varchar(255),
		@EXTERNAL_FLAG varchar(1),
		@REFERENCE_NO varchar(255),
		@SOURCE varchar(255),
		@DOCUMENT_DATE datetime,
		@FILE_PATH varchar(255),
		@STATUS varchar(1),
		@REVISION int,
		@IMPACT_OTHER_FLAG CHAR(1),
		@APPROVAL_ID int,
		@DELETE_FLAG int,
		@CREATED_BY varchar(50),
		@CHANGED_BY varchar(50),
		@REMARK varchar(MAX),
		@MENU_ID varchar(50),
		@RETURN_MSG 				VARCHAR(MAX) OUTPUT

AS
BEGIN TRY
		DECLARE @PROCESS_ID BIGINT,
		@LOCATION VARCHAR(255) = 'sp_DocumentMaintenance_Update';
					
		EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Maintenance', 'Update', @LOCATION, @CREATED_BY

		/*
				-	IF Level = 1																	
					-	Enable :					-	Company Code										
											-	Document Name										
											-	Date										
											-	Document Upload										
																				
				-	IF Level = 2																	
					-	Enable :					-	Company Code										
											-	Process Code										
											-	Document Name										
											-	Date										
											-	Document Upload										
																				
				-	IF Level = 3 or 4																	
					-	Enable :					-	Department 										
											-	Section 										
											-	Document Name										
											-	Date										
											-	Document Upload										
																				
		b. External check box has been checked																				
			Enable :				-	Referensi No														
							-	Source														
							-	Impact to Others														

		*/

		/*
		1.	Document Type = New													
		-	Document Name												
		-	Date												
		-	Document Upload												
		-	Refference No												
		-	Source												
		-	Impact to Other Document												
														
	2.	Document Type = Revision													
		-	Itam Changed												
		-	Reason												
		-	Date												
		-	Document Upload												
		-	Refference No												
		-	Source												
		-	Impact to Other Document												

		*/
		
	-- Create Workflow Document
	DECLARE @RETVAL INT;
	DECLARE @DOC_LEVEL INT;
	DECLARE @DOCUMENT_CREATOR VARCHAR(50);
	
	SELECT @DOC_LEVEL = LEVEL_CODE FROM TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID;
	SELECT @DOCUMENT_CREATOR = DOCUMENT_CREATOR FROM TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID;
	
	EXEC @RETVAL = sp_WorkflowDoc_Create @DOC_LEVEL, @CREATED_BY, @REMARK, @MENU_ID, @DOCUMENT_CREATOR, @RETURN_MSG OUTPUT, @APPROVAL_ID OUTPUT
	IF @RETVAL = 1
	BEGIN
		EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @CREATED_BY
	END
	ELSE BEGIN
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

	UPDATE TB_R_DOCUMENT
	SET [DOCUMENT_TRANSACTION_NAME] = IIF(@DOCUMENT_TYPE = '01', [DOCUMENT_TRANSACTION_NAME], @DOCUMENT_TRANSACTION_NAME)
		--,[DOCUMENT_TYPE] = @DOCUMENT_TYPE
        --,[PROCESS_CODE] = @PROCESS_CODE
        --,[COMPANY_CODE] = @COMPANY_CODE
        --,[SECTION_CODE] = @SECTION_CODE
        ,[ITEM_CHANGED] = IIF(@DOCUMENT_TYPE = '01', [ITEM_CHANGED], @ITEM_CHANGED)
        ,[REASON] = IIF(@DOCUMENT_TYPE = '01', [REASON], @REASON)
        --,[EXTERNAL_FLAG] = @EXTERNAL_FLAG
        ,[REFERENCE_NO] = @REFERENCE_NO
        ,[SOURCE] = IIF(@DOCUMENT_TYPE = '01', [SOURCE], @SOURCE)
        ,[DOCUMENT_DATE] = CAST(@DOCUMENT_DATE as date)
        ,[EXTERNAL_FLAG] = @EXTERNAL_FLAG
        ,[IMPACT_OTHER_FLAG] = @IMPACT_OTHER_FLAG
        ,[APPROVAL_ID] = @APPROVAL_ID
        ,[FILE_PATH] = @FILE_PATH
        ,[STATUS] = @STATUS
        --,[REVISION] = ([REVISION] + 1)
        --,[APPROVAL_ID] = @APPROVAL_ID
		, CHANGED_BY = @CHANGED_BY
		, CHANGED_DT = GETDATE()
	WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
	
	UPDATE TB_R_APPROVAL_H
	SET TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
	WHERE APPROVAL_ID = @APPROVAL_ID

	SET @RETURN_MSG = 'Successfully Save Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @CREATED_BY
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
	RETURN 0;
END CATCH
GO
