CREATE OR ALTER PROCEDURE [dbo].[sp_Menu_GetByKey]
	@MENU_ID VARCHAR(50)
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'SELECT 
								A.MENU_ID,
								A.PARENT_ID,
								B.MENU_NAME AS PARENT_NAME,
								A.MENU_NAME,
								A.MENU_ICON,
								A.MENU_URL,
								A.MENU_SEQ,
								A.CREATED_DT,
								A.CREATED_BY,
								A.CHANGED_BY,
								A.CHANGED_DT
							FROM [dbo].[TB_M_MENU] A
							LEFT JOIN [dbo].[TB_M_MENU] B ON A.[PARENT_ID] = B.[MENU_ID]
							WHERE 1 = 1 '
							
	IF @MENU_ID IS NOT NULL
	BEGIN
		SET @QUERY += ' AND A.MENU_ID LIKE ''' + REPLACE(@MENU_ID , '*', '%') + ''' '
	END
	
	EXEC(@QUERY)
	
END
GO
