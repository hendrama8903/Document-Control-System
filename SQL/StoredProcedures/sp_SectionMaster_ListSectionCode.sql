SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_SectionMaster_ListSectionCode]
	@DEPARTMENT_CODE	VARCHAR(255),
	@SECTION_CODE			VARCHAR(255),
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY S.SECTION_CODE ASC) as RowNumber, 
										S.SECTION_CODE
									FROM [dbo].[TB_M_SECTION] S
									WHERE S.DELETE_FLAG <> 1'
									
									IF @DEPARTMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DEPARTMENT_CODE LIKE ''' + REPLACE(@DEPARTMENT_CODE , '*', '%') + ''' '
									END
									
									IF @SECTION_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.SECTION_CODE LIKE ''' + REPLACE(@SECTION_CODE , '*', '%') + ''' '
									END
																		
									SET @QUERY += ' GROUP BY S.SECTION_CODE'
									
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
