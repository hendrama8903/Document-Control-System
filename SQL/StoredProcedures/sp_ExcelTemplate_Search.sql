SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_ExcelTemplate_Search]
	@DOCUMENT_ID 				int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										E.TEMPLATE_ID,
										E.DOCUMENT_ID,
										E.SHEET_ORIENTATION,
										E.FIELD_NAME,
										E.ROW,
										E.COL,
										E.TYPE,
										E.MERGE_CELL_ROW,
										E.MERGE_CELL_COL,
										E.CHECK_SHEET_POSITION,
										E.SHEET_POSITION,
										E.TARGET_POSITION_ID
									FROM [dbo].[TB_M_EXCEL_TEMPLATE] E
									WHERE 1 = 1 '	
									
									IF @DOCUMENT_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND E.DOCUMENT_ID LIKE ''' + REPLACE(@DOCUMENT_ID , '*', '%') + ''' '
									END

	SET @QUERY += ' 									
								)
								SELECT * FROM data
								WHERE 1 = 1 '
	
	EXEC(@QUERY)
	
END
GO
