CREATE OR ALTER PROCEDURE [dbo].[sp_SectionMaster_Search]
	@SECTION_CODE  		VARCHAR(5),
	@SECTION_NAME			VARCHAR(255),
	@DEPARTMENT_CODE  VARCHAR(5),
	@DEPARTMENT_ID 		int,
	@IS_VALID_ONLY		CHAR(1),
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY S.SECTION_CODE ASC) as RowNumber, 
										S.SECTION_ID,
										S.SECTION_CODE,
										S.SECTION_NAME,
										S.DEPARTMENT_CODE,
										D.DIVISION,
										D.DEPARTMENT_ID,
										S.DELETE_FLAG,
										S.VALID_FROM,
										S.VALID_TO,
										S.CREATED_DT,
										S.CREATED_BY,
										S.CHANGED_DT,
										S.CHANGED_BY
									FROM [dbo].[TB_M_SECTION] S
									LEFT JOIN [dbo].[TB_M_DEPARTMENT] D ON D.DEPARTMENT_CODE = S.DEPARTMENT_CODE AND D.DELETE_FLAG != 1
									WHERE S.DELETE_FLAG <> 1'
									
									IF @SECTION_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.SECTION_CODE LIKE ''' + REPLACE(@SECTION_CODE , '*', '%') + ''' '
									END
									
									IF @SECTION_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.SECTION_NAME LIKE ''' + REPLACE(@SECTION_NAME , '*', '%') + ''' '
									END
									
									IF @DEPARTMENT_CODE IS NOT NULL
										BEGIN
										SET @QUERY += ' AND S.DEPARTMENT_CODE LIKE ''' + REPLACE(@DEPARTMENT_CODE , '*', '%') + ''' '
									END
									
									IF @DEPARTMENT_ID IS NOT NULL
										BEGIN
										SET @QUERY += ' AND D.DEPARTMENT_ID LIKE ''' + REPLACE(@DEPARTMENT_ID , '*', '%') + ''' '
									END
									
									IF @IS_VALID_ONLY IS NOT NULL
									BEGIN
										SET @QUERY += ' AND GETDATE() BETWEEN S.VALID_FROM AND S.VALID_TO AND GETDATE() BETWEEN D.VALID_FROM AND D.VALID_TO '
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
