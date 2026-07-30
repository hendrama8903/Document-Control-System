CREATE OR ALTER PROCEDURE [dbo].[sp_Function_Search]
	@FUNCTION_ID  VARCHAR(50),
	@FUNCTION_NAME VARCHAR(255),
	@PageNumber int,
	@PageSize int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY F.FUNCTION_ID ASC) as RowNumber, 
										F.FUNCTION_ID,
										F.FUNCTION_NAME,
										F.FUNCTION_DESC,
										F.MENU_ID,
										M.MENU_NAME,
										F.CREATED_DT,
										F.CREATED_BY,
										F.CHANGED_DT,
										F.CHANGED_BY
									FROM [dbo].[TB_M_FUNCTION] F
									LEFT JOIN [dbo].[TB_M_MENU] M ON F.MENU_ID = M.MENU_ID
									WHERE 1 = 1 '
									
									IF @FUNCTION_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND F.FUNCTION_ID LIKE ''' + REPLACE(@FUNCTION_ID , '*', '%') + ''' '
									END
									
									IF @FUNCTION_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND F.FUNCTION_NAME LIKE ''' + REPLACE(@FUNCTION_NAME , '*', '%') + ''' '
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
