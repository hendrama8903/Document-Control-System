CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentLog_Search]
	@DOCUMENT_TRANSACTION_ID 	 INT,
	@LOG_TYPE		 	 VARCHAR(50),
	@PageNumber 	 int,
	@PageSize 	   int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY L.CREATED_DT DESC) as RowNumber, 
										L.DOCUMENT_LOG_ID,
										L.DOCUMENT_TRANSACTION_ID,
										L.LOG_TYPE,
										S.SYSTEM_VALUE AS LOG_TYPE_VAL,
										L.CREATED_DT,
										L.CREATED_BY,
										L.CHANGED_DT,
										L.CHANGED_BY
									FROM [dbo].[TB_R_DOCUMENT_LOG] L
									LEFT JOIN [dbo].[TB_M_SYSTEM] S ON S.[SYSTEM_TYPE] = ''LOG_TYPE'' AND S.[SYSTEM_CODE] = L.[LOG_TYPE]
									WHERE 1 = 1 '
									
									IF @DOCUMENT_TRANSACTION_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND L.DOCUMENT_TRANSACTION_ID LIKE ''' + REPLACE(@DOCUMENT_TRANSACTION_ID , '*', '%') + ''' '
									END
									
									IF @LOG_TYPE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND L.LOG_TYPE LIKE ''' + REPLACE(@LOG_TYPE , '*', '%') + ''' '
									END
									
	SET @QUERY += '
								)
								SELECT * FROM data
								WHERE 1 = 1 '
		
	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END
	
	SET @QUERY += ' ORDER BY CREATED_DT DESC'
									
	EXEC(@QUERY)
	
END
GO
