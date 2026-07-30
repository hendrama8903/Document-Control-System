CREATE OR ALTER PROCEDURE [dbo].[sp_DivisionMaster_Search]
	@DIVISION_CODE  	  VARCHAR(5),
	@DIVISION_NAME		  VARCHAR(255),
	@DIVISION_CODE_NAME VARCHAR(255),
	@IS_VALID_ONLY		 	CHAR(1),
	@PageNumber 				int,
	@PageSize 					int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY S.DIVISION_CODE ASC) as RowNumber, 
										S.DIVISION_ID,
										S.DIVISION_CODE,
										S.DIVISION_NAME,
										S.DIVISION_CODE + '' - '' + S.DIVISION_NAME AS DIVISION_CODE_NAME,
										S.VALID_FROM,
										S.VALID_TO,
										S.CREATED_DT,
										S.CREATED_BY,
										S.CHANGED_DT,
										S.CHANGED_BY
									FROM [dbo].[TB_M_DIVISION] S
									WHERE 1 = 1
									AND S.DELETE_FLAG = 0'	
									
									
									IF @DIVISION_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DIVISION_CODE LIKE ''' + REPLACE(@DIVISION_CODE , '*', '%') + ''' '
									END
									
									IF @DIVISION_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DIVISION_NAME LIKE ''' + REPLACE(@DIVISION_NAME , '*', '%') + ''' '
									END
									
									IF @DIVISION_CODE_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DIVISION_CODE + '''+' - '+''' +  S.DIVISION_NAME LIKE ''' + REPLACE(@DIVISION_CODE_NAME , '*', '%') + ''' '
									END
									
									IF @IS_VALID_ONLY IS NOT NULL
									BEGIN
										SET @QUERY += ' AND GETDATE() BETWEEN S.VALID_FROM AND S.VALID_TO  '
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
