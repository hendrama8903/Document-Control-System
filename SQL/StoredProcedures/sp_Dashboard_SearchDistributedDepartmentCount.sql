SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Dashboard_SearchDistributedDepartmentCount]
	@USERNAME 				VARCHAR(255)
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	DECLARE @DIVISION_COUNT INT
	DECLARE @DIVISION_IAD_COUNT INT
	DECLARE @DEPARTMENT_COUNT INT
	
	-- Document Control = role DOC-CONTROL or DMS-ADMIN (2026-11-17, was the department flag per division).
	SET @DIVISION_IAD_COUNT = CAST(dbo.fn_User_IsDocumentControl(@USERNAME) AS INT);
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										D.DEPARTMENT_CODE,
										COUNT(D.DEPARTMENT_CODE) AS DEPARTMENT_COUNT
									FROM [dbo].[TB_R_CTRL_DOCUMENT] S
									JOIN TB_M_DEPARTMENT D
										ON D.DEPARTMENT_ID = S.DEPARTMENT_ID
									WHERE 1 = 1
									AND ISNULL(S.DELETE_FLAG, 0) = 0 
									AND S.OPERATION_TYPE = 1
									AND S.STATUS IN (4,5) '
									
									-- Management System (2026-11-19): Document Control sees the documents of its own systems
									-- (plus its own departments), see fn_User_IsDocumentControlFor.
									IF @USERNAME IS NOT NULL AND @DIVISION_IAD_COUNT = 1
										SET @QUERY += ' AND (dbo.fn_User_IsDocumentControlFor(''' + REPLACE(@USERNAME, '''', '''''') + ''', (SELECT X.MGMT_SYSTEM FROM TB_R_DOCUMENT X WHERE X.DOCUMENT_TRANSACTION_ID = S.DOCUMENT_TRANSACTION_ID)) = 1 OR S.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM TB_M_USER_POS WHERE USERNAME = ''' + REPLACE(@USERNAME, '''', '''''') + ''')) ';

									IF @USERNAME IS NOT NULL AND @DIVISION_IAD_COUNT = 0
										BEGIN										
										SELECT @DIVISION_COUNT = COUNT(DIVISION) FROM TB_M_USER_POS WHERE USERNAME = @USERNAME;
										
										IF @DIVISION_COUNT > 0
										BEGIN
											SET @QUERY += ' AND S.DIVISION IN (SELECT DIVISION FROM TB_M_USER_POS WHERE USERNAME = '''+ @USERNAME+''') '
										END
									END
									
									IF @USERNAME IS NOT NULL AND @DIVISION_IAD_COUNT = 0
										BEGIN
										SELECT @DEPARTMENT_COUNT = COUNT(DEPARTMENT_ID) FROM TB_M_USER_POS WHERE USERNAME = @USERNAME;
										
										IF @DEPARTMENT_COUNT > 0
										BEGIN
											SET @QUERY += ' AND S.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM TB_M_USER_POS WHERE USERNAME = '''+ @USERNAME+''') '
										END
									END
									
	SET @QUERY += ' GROUP BY D.DEPARTMENT_CODE
								)
								SELECT * FROM data
								WHERE 1 = 1 '
									
	EXEC(@QUERY)
	
END
GO
