SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
										G.CHANGED_BY,
										ISNULL(UC.USERS_COUNT, 0) AS USERS_COUNT,
										ISNULL(AM.MENU_COUNT, 0) + ISNULL(AF.FUNCTION_COUNT, 0) AS PERMISSIONS_COUNT
									FROM [dbo].[TB_M_ROLE] G
									LEFT JOIN (SELECT ROLE_ID, COUNT(*) AS USERS_COUNT FROM [dbo].[TB_M_USER] WHERE ISNULL(DELETE_FLAG, ''0'') <> ''1'' GROUP BY ROLE_ID) UC ON UC.ROLE_ID = G.ROLE_ID
									LEFT JOIN (SELECT ROLE_ID, COUNT(*) AS MENU_COUNT FROM [dbo].[TB_M_AUTH_MENU] GROUP BY ROLE_ID) AM ON AM.ROLE_ID = G.ROLE_ID
									LEFT JOIN (SELECT ROLE_ID, COUNT(*) AS FUNCTION_COUNT FROM [dbo].[TB_M_AUTH_FUNCTION] GROUP BY ROLE_ID) AF ON AF.ROLE_ID = G.ROLE_ID
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
