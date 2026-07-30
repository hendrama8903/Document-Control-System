CREATE OR ALTER PROCEDURE [dbo].[sp_Workflow_Search]
	@WORKFLOW_NAME  	VARCHAR(255),
	@PageNumber 			int,
	@PageSize 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY W.CHANGED_DT DESC) as RowNumber, 
										W.WORKFLOW_ID,
										W.WORKFLOW_CODE,
										W.WORKFLOW_NAME,
										CAST((SELECT MAX(WORKFLOW_SEQ) FROM TB_M_WORKFLOW_D WHERE WORKFLOW_ID = W.WORKFLOW_ID) AS INT) AS WORKFLOW_SEQ,
										(SELECT STUFF((SELECT '', '' + APPROVER FROM TB_M_WORKFLOW_D 
										WHERE WORKFLOW_ID = W.WORKFLOW_ID FOR XML PATH, TYPE).value(''.[1]'', ''nvarchar(max)''),1, 1, '''') 
										) AS APPROVER,
										(SELECT STUFF((SELECT '', '' + FULL_NAME FROM TB_M_WORKFLOW_D JOIN TB_M_USER ON APPROVER = USERNAME 
										WHERE WORKFLOW_ID = W.WORKFLOW_ID FOR XML PATH, TYPE).value(''.[1]'', ''nvarchar(max)''),1, 1, '''') 
										) AS APPROVER_NAME,
										W.CREATED_DT,
										W.CREATED_BY,
										W.CHANGED_DT,
										W.CHANGED_BY
									FROM [dbo].[TB_M_WORKFLOW_H] W
									WHERE 1 = 1 '
									
	IF @WORKFLOW_NAME IS NOT NULL
	BEGIN
		SET @QUERY += ' AND W.WORKFLOW_NAME LIKE ''' + REPLACE(@WORKFLOW_NAME , '*', '%') + ''' '
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
