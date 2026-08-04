SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Dashboard_ReceivedDocumentDateCount]
	@LOGIN_USER 				VARCHAR(255),
	@OPERATION_TYPE			INT
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	DECLARE @DIVISION_COUNT INT
	DECLARE @DEPARTMENT_COUNT INT
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT
										FORMAT(S.CREATED_DT, ''MMMM'') AS MONTH,
										COUNT(*) AS COUNT
									FROM [dbo].[TB_R_CTRL_DOCUMENT] S
									JOIN TB_M_USER US
										ON US.USERNAME = S.CREATED_BY
									JOIN TB_M_DEPARTMENT D
										ON D.DEPARTMENT_ID = S.DEPARTMENT_ID
									WHERE 1 = 1
									AND ISNULL(S.DELETE_FLAG, 0) = 0
									AND YEAR(S.CREATED_DT) = YEAR(GETDATE()) '
									
									IF @OPERATION_TYPE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.OPERATION_TYPE LIKE ''' + REPLACE(@OPERATION_TYPE , '*', '%') + ''' '
									END
									
									IF @LOGIN_USER IS NOT NULL
									BEGIN
										SELECT @DIVISION_COUNT = COUNT(DIVISION) FROM TB_M_USER_POS WHERE USERNAME = @LOGIN_USER;
										SELECT @DEPARTMENT_COUNT = COUNT(DEPARTMENT_ID) FROM TB_M_USER_POS WHERE USERNAME = @LOGIN_USER;
									
										SET @QUERY += 'AND (
																	EXISTS (
																			SELECT 1 FROM TB_R_CTRL_DOCUMENT SS
																			WHERE S.OPERATION_TYPE = 2
																			AND S.CREATED_BY = ''' + @LOGIN_USER + '''
																		)';
																		
										IF @DEPARTMENT_COUNT > 0
										BEGIN
											SET @QUERY += 'OR EXISTS (
																			SELECT 1 FROM TB_R_DOCUMENT_DISTRIBUTION DD
																			WHERE DD.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM TB_M_USER_POS WHERE USERNAME = '''+ @LOGIN_USER+''')
																			AND DD.DOCUMENT_TRANSACTION_ID = S.DOCUMENT_TRANSACTION_ID
																			AND DD.STATUS = 1
																			AND S.OPERATION_TYPE = 1
																		)'
										END
										ELSE
										BEGIN
											IF @DIVISION_COUNT > 0
											BEGIN
												SET @QUERY += 'OR EXISTS (
																				SELECT 1 FROM TB_R_DOCUMENT_DISTRIBUTION DD
																				WHERE DD.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM TB_M_DEPARTMENT WHERE DIVISION IN (SELECT DIVISION FROM TB_M_USER_POS WHERE USERNAME = '''+@LOGIN_USER+'''))
																				AND DD.DOCUMENT_TRANSACTION_ID = S.DOCUMENT_TRANSACTION_ID
																				AND DD.STATUS = 1
																				AND S.OPERATION_TYPE = 1
																			)'
											END
											ELSE
											BEGIN
											SET @QUERY += 'OR EXISTS (
																			SELECT 1 FROM TB_R_DOCUMENT_DISTRIBUTION DD
																			WHERE DD.DOCUMENT_TRANSACTION_ID = S.DOCUMENT_TRANSACTION_ID
																			AND DD.STATUS = 1
																			AND S.OPERATION_TYPE = 1
																		)'
											END
										END
																		
										SET @QUERY += ') ';
								END
									
	SET @QUERY += ' GROUP BY FORMAT(S.CREATED_DT, ''MMMM''), MONTH(S.CREATED_DT)
								)
								SELECT * FROM data
								WHERE 1 = 1 '
									
	EXEC(@QUERY)
	
END
GO
