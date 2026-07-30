CREATE OR ALTER PROCEDURE [dbo].[sp_Role_GetByKey]
	@ROLE_ID VARCHAR(50)
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'SELECT 
								ROLE_ID,
								ROLE_NAME,
								ROLE_DESC,
								CREATED_DT,
								CREATED_BY,
								CHANGED_BY,
								CHANGED_DT
							FROM [dbo].[TB_M_ROLE] 
							WHERE 1 = 1 '
							
	IF @ROLE_ID IS NOT NULL
	BEGIN
		SET @QUERY += ' AND ROLE_ID LIKE ''' + REPLACE(@ROLE_ID , '*', '%') + ''' '
	END
	
	EXEC(@QUERY)
	
END
GO
