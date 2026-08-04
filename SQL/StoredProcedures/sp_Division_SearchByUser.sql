SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Division_SearchByUser]
	@DIVISION_CODE				VARCHAR(50),
	@DIVISION_NAME				VARCHAR(255),
	@DIVISION_CODE_NAME		VARCHAR(255),
	@USERNAME							VARCHAR(255),
	@PageNumber 					int,
	@PageSize 						int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
									ROW_NUMBER() OVER (ORDER BY G.CHANGED_DT ASC) as RowNumber,
									G.DIVISION_CODE,
									G.DIVISION_NAME,
									G.DIVISION_CODE + '' - '' + G.DIVISION_NAME AS DIVISION_CODE_NAME,
									UP.USERNAME
									FROM [dbo].[TB_M_DIVISION] G
									JOIN TB_M_USER_POS UP ON UP.DIVISION = G.DIVISION_CODE
									AND 1 = 1 '
									
									
							IF @DIVISION_CODE IS NOT NULL
							BEGIN
								SET @QUERY += ' AND DIVISION_CODE LIKE ''' + REPLACE(@DIVISION_CODE , '*', '%') + ''' '
							END
							
							IF @DIVISION_NAME IS NOT NULL
							BEGIN
								SET @QUERY += ' AND DIVISION_NAME LIKE ''' + REPLACE(@DIVISION_NAME , '*', '%') + ''' '
							END
							
							IF @DIVISION_CODE_NAME IS NOT NULL
							BEGIN
								SET @QUERY += ' AND DIVISION_CODE + '''+' - '+''' +  DIVISION_NAME LIKE ''' + REPLACE(@DIVISION_CODE_NAME , '*', '%') + ''' '
							END
							
							IF @USERNAME IS NOT NULL
							BEGIN
								SET @QUERY += ' AND UP.USERNAME LIKE ''' + REPLACE(@USERNAME , '*', '%') + ''' '
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
