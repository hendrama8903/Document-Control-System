CREATE OR ALTER PROCEDURE [dbo].[sp_Menu_Parent_Search]
	@MENU_NAME 	 VARCHAR(255),
	@PageNumber int,
	@PageSize int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY M.MENU_ID ASC) as RowNumber, 
										M.MENU_ID,
										M.PARENT_ID,
										B.MENU_NAME AS PARENT_NAME,
										M.MENU_NAME,
										M.MENU_ICON,
										M.MENU_URL,
										M.MENU_SEQ,
										M.CREATED_DT,
										M.CREATED_BY,
										M.CHANGED_DT,
										M.CHANGED_BY
									FROM [dbo].[TB_M_MENU] M
									LEFT JOIN [dbo].[TB_M_MENU] B ON M.[PARENT_ID] = B.[MENU_ID]
									WHERE (M.PARENT_ID IS NULL OR LEN(M.PARENT_ID) < 1) '
										
									IF @MENU_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND M.MENU_NAME LIKE ''' + REPLACE(@MENU_NAME , '*', '%') + ''' '
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
