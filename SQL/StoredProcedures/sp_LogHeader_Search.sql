CREATE OR ALTER PROCEDURE [dbo].[sp_LogHeader_Search]
	@START_DT					DATE,
	@END_DT						DATE,
	@PROCESS_ID				INT,
	@MODULE						VARCHAR(50),
	@FUNCTION 				VARCHAR(50),
	@CREATED_BY 			VARCHAR(50),
	@PROCESS_STATUS 	VARCHAR(50),
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY H.PROCESS_ID DESC) as RowNumber, 
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
									
									IF @START_DT IS NOT NULL AND @END_DT IS NOT NULL 
										BEGIN
										SET @QUERY += ' AND CONVERT(VARCHAR, H.START_DT, 23) >=  ''' + CONVERT(VARCHAR, @START_DT, 21) + ''' 
																		AND CONVERT(VARCHAR, H.END_DT, 23) <=  ''' + CONVERT(VARCHAR, @END_DT, 21) + ''' '
									END
									
									IF @PROCESS_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND H.PROCESS_ID LIKE ''' + REPLACE(@PROCESS_ID , '*', '%') + ''' '
									END
									
									IF @MODULE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND H.MODULE LIKE ''' + REPLACE(@MODULE , '*', '%') + ''' '
									END
									
									IF @FUNCTION IS NOT NULL
									BEGIN
										SET @QUERY += ' AND H.[FUNCTION] LIKE ''' + REPLACE(@FUNCTION , '*', '%') + ''' '
									END
									
									IF @CREATED_BY IS NOT NULL
									BEGIN
										SET @QUERY += ' AND H.CREATED_BY LIKE ''' + REPLACE(@CREATED_BY , '*', '%') + ''' '
									END
									
									IF @PROCESS_STATUS IS NOT NULL
									BEGIN
										SET @QUERY += ' AND H.PROCESS_STATUS LIKE ''' + REPLACE(@PROCESS_STATUS , '*', '%') + ''' '
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
