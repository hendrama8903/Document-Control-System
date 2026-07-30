CREATE OR ALTER PROCEDURE [dbo].[sp_Role_Search]
	@ROLE_ID 	 VARCHAR(50),
	@ROLE_NAME VARCHAR(255),
	@PageNumber int,
	@PageSize int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY G.ROLE_ID ASC) as RowNumber, 
										G.ROLE_ID,
										G.ROLE_NAME,
										G.ROLE_DESC,
										G.CREATED_DT,
										G.CREATED_BY,
										G.CHANGED_DT,
										G.CHANGED_BY
									FROM [dbo].[TB_M_ROLE] G
									WHERE 1 = 1 '
									
									IF @ROLE_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND ROLE_ID LIKE ''' + REPLACE(@ROLE_ID , '*', '%') + ''' '
									END
									
									IF @ROLE_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND ROLE_NAME LIKE ''' + REPLACE(@ROLE_NAME , '*', '%') + ''' '
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
