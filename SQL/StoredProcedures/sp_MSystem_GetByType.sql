SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_MSystem_GetByType]
	@SYSTEM_TYPE VARCHAR(50),
	@PageNumber int,
	@PageSize int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY SYSTEM_TYPE ASC) as RowNumber, 
										SYSTEM_TYPE,
										SYSTEM_CODE,
										SYSTEM_CODE + '' - '' + SYSTEM_VALUE AS SYSTEM_CODE_VALUE,
										SYSTEM_VALUE,
										STATUS,
										CREATED_DT,
										CREATED_BY,
										CHANGED_DT,
										CHANGED_BY
									FROM [dbo].[TB_M_SYSTEM]
									WHERE 1 = 1'
									
									IF @SYSTEM_TYPE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND SYSTEM_TYPE LIKE ''' + REPLACE(@SYSTEM_TYPE , '*', '%') + ''' '
									END
									
SET @QUERY += '
								)
								SELECT * FROM data
								WHERE STATUS = 1 '
								
	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END
	
	
-- 	SET @QUERY += ' ORDER BY SYSTEM_TYPE ASC '
-- 	
-- 	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
-- 	BEGIN
-- 		SET @QUERY += ' OFFSET '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' ROWS 	FETCH NEXT '+CAST(@PageSize AS VARCHAR) +' ROWS ONLY';
-- 	END
	
	EXEC(@QUERY)
	
END
GO
