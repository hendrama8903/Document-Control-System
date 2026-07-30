CREATE OR ALTER PROCEDURE [dbo].[sp_UserPosition_Search]
  @USERNAME varchar(255),
  @DIVISION varchar(50),
  @DEPARTMENT_ID INT,
	@DOCUMENT_CONTROL_ACCESS VARCHAR(5),
	@PageNumber int,
	@PageSize int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT 
										ROW_NUMBER() OVER (ORDER BY U.USERNAME ASC) as RowNumber,   
										U.USER_POS_ID,
										U.USERNAME,
										U.POSITION_ID,
										P.POSITION_NAME AS POSITION_NAME,
										U.DIVISION,
										S.DIVISION_NAME,
										U.DEPARTMENT_ID,
										D.DEPARTMENT_NAME AS DEPARTMENT_NAME,
										U.SECTION_ID,
										SC.SECTION_NAME AS SECTION_NAME,
										DV.DOCUMENT_CONTROL_ACCESS
									FROM TB_M_USER_POS U
									LEFT JOIN TB_M_POSITION P ON P.POSITION_ID = U.POSITION_ID
									LEFT JOIN TB_M_DIVISION S ON S.DIVISION_CODE = U.DIVISION
									LEFT JOIN TB_M_DEPARTMENT D ON D.DEPARTMENT_ID = U.DEPARTMENT_ID
									LEFT JOIN TB_M_SECTION SC ON SC.SECTION_ID = U.SECTION_ID
									LEFT JOIN TB_M_DEPARTMENT DV ON DV.DIVISION = U.DIVISION
									WHERE 1 = 1 '
									
									IF @USERNAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND U.USERNAME LIKE ''' + REPLACE(@USERNAME, '*', '%') + ''' '
									END
									
									IF @DIVISION IS NOT NULL
									BEGIN
										SET @QUERY += ' AND U.DIVISION LIKE ''' + REPLACE(@DIVISION, '*', '%') + ''' '
									END
									
									IF @DEPARTMENT_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND U.DEPARTMENT_ID LIKE ''' + REPLACE(@DEPARTMENT_ID, '*', '%') + ''' '
									END
									
									IF @DOCUMENT_CONTROL_ACCESS IS NOT NULL
									BEGIN
										SET @QUERY += ' AND DV.DOCUMENT_CONTROL_ACCESS LIKE ''' + REPLACE(@DOCUMENT_CONTROL_ACCESS, '*', '%') + ''' '
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
