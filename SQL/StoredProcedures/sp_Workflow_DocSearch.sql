SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Workflow_DocSearch]
	@CREATOR_LEVEL  	int,
	@DOCUMENT_LEVEL  	int,
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
								    ROW_NUMBER() OVER (ORDER BY W.DOCUMENT_LEVEL ASC) as RowNumber,  
										W.WORKFLOW_DOC_ID,
										W.DOCUMENT_LEVEL,
										W.CREATOR_LEVEL,
										PH.POSITION_NAME AS CREATOR_NAME,
										CAST((SELECT MAX(WORKFLOW_SEQ) FROM TB_M_WORKFLOW_DOC_D WHERE WORKFLOW_DOC_ID = W.WORKFLOW_DOC_ID) AS INT) AS WORKFLOW_SEQ,
										(SELECT CAST(STUFF(
														(
																SELECT '', '' + CONVERT(NVARCHAR(50), POSITION_ID)
																FROM TB_M_WORKFLOW_DOC_D
																WHERE WORKFLOW_DOC_ID = W.WORKFLOW_DOC_ID
																FOR XML PATH(''''), TYPE
														).value(''.[1]'', ''nvarchar(max)''), 1, 2, ''''
												) AS VARCHAR(max)
										)) AS POSITION_ID_ARR,
										
										(SELECT CAST(STUFF(
														(
																SELECT '', '' + CONVERT(NVARCHAR(50), P.POSITION_NAME)
																FROM TB_M_WORKFLOW_DOC_D DD
																LEFT JOIN [dbo].[TB_M_POSITION] P ON P.POSITION_ID = DD.POSITION_ID
																WHERE DD.WORKFLOW_DOC_ID = W.WORKFLOW_DOC_ID
																FOR XML PATH(''''), TYPE
														).value(''.[1]'', ''nvarchar(max)''), 1, 2, ''''
												) AS VARCHAR(max)
										)) AS POSITION_NAME,
										W.CREATED_DT,
										W.CREATED_BY,
										W.CHANGED_DT,
										W.CHANGED_BY
									FROM [dbo].[TB_M_WORKFLOW_DOC_H] W
									LEFT JOIN [dbo].[TB_M_POSITION] PH ON PH.POSITION_ID = W.CREATOR_LEVEL
									WHERE 1 = 1 '
									
	IF @CREATOR_LEVEL IS NOT NULL
	BEGIN
		SET @QUERY += ' AND W.CREATOR_LEVEL LIKE ''' + REPLACE(@CREATOR_LEVEL , '*', '%') + ''' '
	END
	
	IF @DOCUMENT_LEVEL IS NOT NULL
	BEGIN
		SET @QUERY += ' AND W.DOCUMENT_LEVEL LIKE ''' + REPLACE(@DOCUMENT_LEVEL , '*', '%') + ''' '
	END
	
	SET @QUERY += '									
								)
								SELECT * FROM data
								WHERE 1 = 1'
	
	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END
	
	EXEC(@QUERY)
	
END
GO
