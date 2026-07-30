CREATE OR ALTER PROCEDURE [dbo].[sp_AuthFunction_Search]
	@ROLE_ID 	 VARCHAR(50)
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'SELECT
								G.ROLE_ID,
								G.FUNCTION_ID,
								G.CREATED_DT,
								G.CREATED_BY,
								G.CHANGED_DT,
								G.CHANGED_BY
							FROM [dbo].[TB_M_AUTH_FUNCTION] G
							WHERE 1 = 1 '
							
	IF @ROLE_ID IS NOT NULL
	BEGIN
		SET @QUERY += ' AND G.ROLE_ID LIKE ''' + REPLACE(@ROLE_ID , '*', '%') + ''' '
	END
	
	EXEC(@QUERY)
	
END
GO
