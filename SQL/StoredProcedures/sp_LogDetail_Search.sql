CREATE OR ALTER PROCEDURE [dbo].[sp_LogDetail_Search]
	@PROCESS_ID				INT,
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY D.SEQ_NO ASC) as RowNumber, 
										D.PROCESS_ID,
										D.SEQ_NO,
										D.MESSAGE_TYPE,
										D.MESSAGE_CONTENT,
										D.LOCATION,
										D.CREATED_DT,
										D.CREATED_BY,
										D.CHANGED_DT,
										D.CHANGED_BY
									FROM [dbo].[TB_R_LOG_D] D
									WHERE 1 = 1 '
																		
									IF @PROCESS_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND D.PROCESS_ID LIKE ''' + REPLACE(@PROCESS_ID , '*', '%') + ''' '
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
