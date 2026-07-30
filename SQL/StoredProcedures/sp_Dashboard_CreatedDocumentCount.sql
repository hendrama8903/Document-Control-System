CREATE OR ALTER PROCEDURE [dbo].[sp_Dashboard_CreatedDocumentCount]
	@USERNAME 				VARCHAR(255)
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	DECLARE @DIVISION_COUNT INT
	DECLARE @DEPARTMENT_COUNT INT
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
									COUNT(*) AS COUNT
									FROM [dbo].[TB_R_DOCUMENT] S
									WHERE 1 = 1
									AND ISNULL(S.DELETE_FLAG, 0) = 0 '
									
									IF ISNULL(@USERNAME, '') <> ''  --@YEAR IS NOT NULL
										BEGIN
										SELECT @DEPARTMENT_COUNT = COUNT(DEPARTMENT_ID) FROM TB_M_USER_POS WHERE USERNAME = @USERNAME;
										
										IF @DEPARTMENT_COUNT > 0
										BEGIN
											SET @QUERY += ' AND S.DEPARTMENT IN (SELECT DEPARTMENT_ID FROM TB_M_USER_POS WHERE USERNAME = '''+ @USERNAME+''') '
										END
									END
									
	SET @QUERY += '
								)
								SELECT * FROM data
								WHERE 1 = 1 '
									
	EXEC(@QUERY)
	
END
GO
