SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DepartmentMaster_Search]
	@DEPARTMENT_CODE  	VARCHAR(5),
	@DEPARTMENT_NAME		VARCHAR(255),
	@DEPARTMENT_CODE_NAME VARCHAR(255),
	@DIVISION  					VARCHAR(50),
	@DOCUMENT_CONTROL_ACCESS	VARCHAR(5),
	@IS_VALID_ONLY		 	CHAR(1),
	@PageNumber 				int,
	@PageSize 					int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY S.DIVISION ASC) as RowNumber, 
										S.DEPARTMENT_ID,
										S.DEPARTMENT_CODE,
										S.DEPARTMENT_NAME,
										S.DEPARTMENT_CODE + '' - '' + S.DEPARTMENT_NAME AS DEPARTMENT_CODE_NAME,
										S.DIVISION + '' - '' + Y.DIVISION_NAME AS DIVISION,
										S.DOCUMENT_CONTROL_ACCESS,
										S.VALID_FROM,
										S.VALID_TO,
										S.CREATED_DT,
										S.CREATED_BY,
										S.CHANGED_DT,
										S.CHANGED_BY
									FROM [dbo].[TB_M_DEPARTMENT] S
									JOIN [dbo].[TB_M_DIVISION] Y ON Y.[DIVISION_CODE] = S.[DIVISION]
									WHERE 1 = 1
									AND S.DELETE_FLAG = 0'	
									
									
									IF @DEPARTMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DEPARTMENT_CODE LIKE ''' + REPLACE(@DEPARTMENT_CODE , '*', '%') + ''' '
									END
									
									IF @DEPARTMENT_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DEPARTMENT_NAME LIKE ''' + REPLACE(@DEPARTMENT_NAME , '*', '%') + ''' '
									END
									
									IF @DIVISION IS NOT NULL
										BEGIN
										SET @QUERY += ' AND S.DIVISION LIKE ''' + REPLACE(@DIVISION , '*', '%') + ''' '
									END
									
									IF @DEPARTMENT_CODE_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DEPARTMENT_CODE + '''+' - '+''' +  S.DEPARTMENT_NAME LIKE ''' + REPLACE(@DEPARTMENT_CODE_NAME , '*', '%') + ''' '
									END
									
									IF @DOCUMENT_CONTROL_ACCESS IS NOT NULL
										BEGIN
										SET @QUERY += ' AND S.DOCUMENT_CONTROL_ACCESS LIKE ''' + REPLACE(@DOCUMENT_CONTROL_ACCESS , '*', '%') + ''' '
									END
									
									IF @IS_VALID_ONLY IS NOT NULL
									BEGIN
										SET @QUERY += ' AND GETDATE() BETWEEN S.VALID_FROM AND S.VALID_TO AND GETDATE() BETWEEN Y.VALID_FROM AND Y.VALID_TO '
									END

	SET @QUERY += ' 									
								)
								SELECT * FROM data
								WHERE 1 = 1 '
	
	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END
	
	EXEC(@QUERY)
	
END
GO
