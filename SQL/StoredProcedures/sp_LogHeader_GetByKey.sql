CREATE OR ALTER PROCEDURE [dbo].[sp_LogHeader_GetByKey]
	@PROCESS_ID INT
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'SELECT 
								H.PROCESS_ID,
								H.MODULE,
								H.[FUNCTION],
								H.START_DT,
								H.END_DT,
								H.PROCESS_STATUS,
								H.CREATED_DT,
								H.CREATED_BY,
								H.CHANGED_DT,
								H.CHANGED_BY
							FROM [dbo].[TB_R_LOG_H] H
							WHERE 1 = 1 '
							
	IF @PROCESS_ID IS NOT NULL
	BEGIN
		SET @QUERY += ' AND H.PROCESS_ID LIKE ''' + REPLACE(@PROCESS_ID , '*', '%') + ''' '
	END
	
	EXEC(@QUERY)
	
END
GO
