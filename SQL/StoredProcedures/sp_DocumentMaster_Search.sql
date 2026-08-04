SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaster_Search]
	@DOCUMENT_CODE  	VARCHAR(5),
	@DOCUMENT_NAME		VARCHAR(255),
	@LEVEL  					INT,
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN

	DECLARE @QUERY VARCHAR(MAX)

	SET @QUERY = 'WITH data AS
								(
									SELECT
										ROW_NUMBER() OVER (ORDER BY D.DOCUMENT_CODE ASC) as RowNumber,
										D.DOCUMENT_ID,
										D.DOCUMENT_CODE,
										D.DOCUMENT_NAME,
										D.LEVEL,
										S.SYSTEM_VALUE AS LEVEL_VAL,
										D.FILE_PATH,
										D.REVIEW_CYCLE_MONTHS,
										D.CREATED_DT,
										D.CREATED_BY,
										D.CHANGED_DT,
										D.CHANGED_BY
									FROM [dbo].[TB_M_DOCUMENT] D
									LEFT JOIN [dbo].[TB_M_SYSTEM] S ON S.SYSTEM_TYPE = ''LEVEL_CODE'' AND S.SYSTEM_CODE = D.LEVEL
									WHERE 1 = 1
									AND D.DELETE_FLAG = 0'

									IF @DOCUMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND D.DOCUMENT_CODE LIKE ''' + REPLACE(@DOCUMENT_CODE , '*', '%') + ''' '
									END

									IF @DOCUMENT_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND D.DOCUMENT_NAME LIKE ''' + REPLACE(@DOCUMENT_NAME , '*', '%') + ''' '
									END

									IF @LEVEL IS NOT NULL
										BEGIN
										SET @QUERY += ' AND D.LEVEL LIKE ''' + REPLACE(@LEVEL , '*', '%') + ''' '
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
