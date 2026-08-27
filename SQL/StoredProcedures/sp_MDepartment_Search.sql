-- 5) sp_MDepartment_Search - baris department dipetakan ke DocumentMaintenance
--    juga (DocumentMaintenanceRepo.SearchDepartmentByDivision), P4D_STATUS_VAL
--    dikosongkan.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_MDepartment_Search]
	@DIVISION					VARCHAR(50)
AS
BEGIN
	DECLARE @QUERY VARCHAR(MAX)

	SET @QUERY = 'WITH data AS
		(
			SELECT
										'''' DOCUMENT_CODE,
										'''' DOCUMENT_NAME,
										A.DEPARTMENT_NAME DEPARTMENT_NAME, --S.DEPARTMENT_NAME,
										'''' CLASSIFIED, --S.CLASSIFIED,
										'''' STATUS,
										0 REVISION,
										'''' ITEM_CHANGED,
										'''' REASON,
										'''' REFERENCE_NO,
										'''' SOURCE,
										A.CREATED_DT,
										A.CREATED_BY,
										A.CHANGED_DT,
										A.CHANGED_BY,
										'''' DISTRIBUTION,
										0 DOCUMENT_ID,
										0 APPROVAL_ID,
										'''' DOCUMENT_TYPE,
										'''' PROCESS_CODE,
										'''' COMPANY_CODE,
										'''' SECTION_CODE,
										'''' EXTERNAL_FLAG,
										CAST(GETDATE() as date) DOCUMENT_DATE,
										'''' FILE_PATH,
										0 DELETE_FLAG,
										CAST(A.DEPARTMENT_ID as varchar) DEPARTMENT_ID,
										A.DEPARTMENT_CODE DEPARTMENT_CODE,
										'''' DOCUMENT_NO,
										CAST(YEAR(GETDATE()) as varchar(4)) DOCUMENT_YEAR,
										A.DIVISION DIVISION,
										'''' CATEGORY_CODE,
										'''' P4D_STATUS_VAL,
										NULL CURRENT_APPROVAL_SEQ,
										NULL TOTAL_APPROVAL_SEQ,
										'''' CURRENT_APPROVAL_LABEL,
										'''' CURRENT_APPROVER,
										'''' CURRENT_APPROVER_NAME
			FROM [dbo].[TB_M_DEPARTMENT] A
			WHERE 1 = 1
				AND ISNULL(DELETE_FLAG, 0) = 0 '

			IF @DIVISION IS NOT NULL
			BEGIN
				SET @QUERY += ' AND DIVISION LIKE ''' + REPLACE(@DIVISION , '*', '%') + ''' '
			END


	SET @QUERY += '
		)
		SELECT * FROM data
		WHERE 1 = 1 '

	EXEC(@QUERY)

END
GO
