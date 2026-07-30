CREATE OR ALTER PROCEDURE [dbo].[sp_Function_GetByMenu]
	@MENU_ID VARCHAR(50)
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'SELECT 
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
							
	IF @MENU_ID IS NOT NULL
	BEGIN
		SET @QUERY += ' AND F.MENU_ID LIKE ''' + REPLACE(@MENU_ID , '*', '%') + ''' '
	END
	
	EXEC(@QUERY)
	
END
GO
