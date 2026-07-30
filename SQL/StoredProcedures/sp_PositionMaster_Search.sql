CREATE OR ALTER PROCEDURE [dbo].[sp_PositionMaster_Search]
	@POSITION_NAME  	VARCHAR(255),
	@POSITION_LEVEL		INT,
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY P.POSITION_LEVEL ASC) as RowNumber, 
										P.POSITION_ID,
										P.POSITION_NAME,
										P.POSITION_LEVEL,
										P.DELETE_FLAG,
										P.CREATED_DT,
										P.CREATED_BY,
										P.CHANGED_DT,
										P.CHANGED_BY
									FROM [dbo].[TB_M_POSITION] P
									WHERE P.DELETE_FLAG <> 1'
									
									IF @POSITION_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND P.POSITION_NAME LIKE ''' + REPLACE(@POSITION_NAME , '*', '%') + ''' '
									END
									
									IF @POSITION_LEVEL IS NOT NULL
									BEGIN
										SET @QUERY += ' AND P.POSITION_LEVEL LIKE ''' + REPLACE(@POSITION_LEVEL , '*', '%') + ''' '
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
