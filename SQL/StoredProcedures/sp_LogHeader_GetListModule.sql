CREATE OR ALTER PROCEDURE [dbo].[sp_LogHeader_GetListModule]
	@MODULE						VARCHAR(50),
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY H.MODULE ASC) as RowNumber, 
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
									JOIN (SELECT 
													MAX(L.PROCESS_ID) AS PROCESS_IDS,
													L.MODULE
												FROM [dbo].[TB_R_LOG_H] L
												GROUP BY L.MODULE ) B
									ON H.PROCESS_ID = B.PROCESS_IDS									
									WHERE 1 = 1 '
									
									IF @MODULE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND H.MODULE LIKE ''' + REPLACE(@MODULE , '*', '%') + ''' '
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
