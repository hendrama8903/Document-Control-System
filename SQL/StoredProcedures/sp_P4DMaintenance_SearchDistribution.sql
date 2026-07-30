CREATE OR ALTER PROCEDURE [dbo].[sp_P4DMaintenance_SearchDistribution]
	@DISTRIBUTION_ID 		int,
	@DOCUMENT_TRANSACTION_ID 	int,
	@DOCUMENT_CODE			VARCHAR(50),
	@STATUS							CHAR(1),
	@PageNumber 				int,
	@PageSize 					int
AS
BEGIN  

	DECLARE @QUERY VARCHAR(MAX)

	/*
	public int? NO { get; set; }
        public int? DOCUMENT_ID { get; set; }
        public string? DEPARTMENT_ID { get; set; }
        public string? DEPARTMENT_CODE { get; set; }
        public string? DEPARTMENT_NAME { get; set; }
        public string? DATE { get; set; }
        public int? QTY { get; set; }
        public string? STATUS { get; set; }
	*/
	
	SET @QUERY = 'WITH data AS 
								(
									SELECT ROW_NUMBER() OVER (ORDER BY DISTRIBUTION_ID ASC)  RowNumber
										, DISTRIBUTION_ID
										, A.DOCUMENT_TRANSACTION_ID
										, CTRL.DOCUMENT_CODE
										, CAST(A.DEPARTMENT_ID as varchar) DEPARTMENT_ID
										, C.DEPARTMENT_CODE
										, C.DEPARTMENT_NAME DEPARTMENT_NAME
										, ''0'' DIVISION_ID
										, C.DIVISION
										, DSYS.DIVISION_NAME
										, CAST(DISTRIBUTION_DATE as varchar) DATE
										, A.STATUS STATUS
										, B.SYSTEM_VALUE STATUS_DISPLAY
									FROM TB_R_DOCUMENT_DISTRIBUTION A
									JOIN TB_M_SYSTEM B
										on B.SYSTEM_CODE = A.STATUS
											AND B.SYSTEM_TYPE = ''DISTRIBUTION_STATUS''
									JOIN TB_M_DEPARTMENT C
										on C.DEPARTMENT_ID = A.DEPARTMENT_ID
									LEFT JOIN TB_R_DOCUMENT CTRL
										on A.DOCUMENT_TRANSACTION_ID = CTRL.DOCUMENT_TRANSACTION_ID AND CTRL.DELETE_FLAG != ''1''
									JOIN TB_M_DIVISION DSYS
										on DSYS.DIVISION_CODE = ISNULL(C.DIVISION, ''0'')
									WHERE 1=1 '
									
									
									IF @DISTRIBUTION_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND DISTRIBUTION_ID LIKE ''' + REPLACE(@DISTRIBUTION_ID , '*', '%') + ''' '
										--SET @QUERY += ' '
									END
									

									IF @DOCUMENT_TRANSACTION_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND A.DOCUMENT_TRANSACTION_ID LIKE ''' + REPLACE(@DOCUMENT_TRANSACTION_ID , '*', '%') + ''' '
										--SET @QUERY += ' '
									END
									
									IF @DOCUMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND CTRL.DOCUMENT_CODE LIKE ''' + REPLACE(@DOCUMENT_CODE , '*', '%') + ''' '
										--SET @QUERY += ' '
									END
									
									IF @STATUS IS NOT NULL
									BEGIN
										SET @QUERY += ' AND A.STATUS LIKE ''' + REPLACE(@STATUS , '*', '%') + ''' '
										--SET @QUERY += ' '
									END

									/*IF @DEPARTMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DEPARTMENT_ID LIKE ''' + REPLACE(@DEPARTMENT_CODE , '*', '%') + ''' '
										--SET @QUERY += ' '
									END
									
									IF @DOCUMENT_NO IS NOT NULL
									BEGIN
										SET @QUERY += ' AND (S.DOCUMENT_CODE LIKE ''' + REPLACE(@DOCUMENT_NO , '*', '%') + ''' '
										SET @QUERY += ' OR S.DOCUMENT_ID LIKE ''' + REPLACE(@DOCUMENT_NO , '*', '%') + ''') '
									END
									
									IF @YEAR IS NOT NULL
										BEGIN
										SET @QUERY += ' AND YEAR(S.DOCUMENT_DATE) = ''' + REPLACE(@YEAR , '*', '') + ''' '
									END*/

	SET @QUERY += ' 									
								)
								SELECT * FROM data
								WHERE 1 = 1 '
	
	IF (@PageSize IS NOT NULL AND @PageSize > 0) AND @PageNumber IS NOT NULL 
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END

	--SET @QUERY += ' UNION ALL SELECT 0 RowNumber
	--									, 0 DISTRIBUTION_ID
	--									, 0 DOCUMENT_ID
	--									, '''' DEPARTMENT_ID
	--									, ''0'' DEPARTMENT_CODE
	--									, '''' DEPARTMENT_NAME
	--									, ''0'' DIVISION_ID
	--									, ''0'' DIVISION_NAME
	--									, '''' DATE
	--									, 1000 QTY
	--									, '''' STATUS
	--									, '''' STATUS_DISPLAY'
	--print @QUERY
	EXEC(@QUERY)
	
END
GO
