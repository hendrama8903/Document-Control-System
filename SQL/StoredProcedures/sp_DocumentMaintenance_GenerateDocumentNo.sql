-- 4) sp_DocumentMaintenance_GenerateDocumentNo - baris sintetis preview
--    nomor dokumen baru, P4D_STATUS_VAL dikosongkan.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_GenerateDocumentNo]
AS
BEGIN

	select CONCAT('D-', FORMAT(MAX(CAST(RIGHT(DOCUMENT_CODE, 3) as int)) + 1, '000')) DOCUMENT_NO
	, '' DOCUMENT_CODE,
										'' DOCUMENT_NAME,
										''  DEPARTMENT_NAME, --S.DEPARTMENT_NAME,
										''  CLASSIFIED, --S.CLASSIFIED,
										'' STATUS,
										'' STATUS_DISPLAY,
										0 REVISION,
										'' ITEM_CHANGED,
										'' REASON,
										'' REFERENCE_NO,
										'' SOURCE,
										CAST(GETDATE() as date) CREATED_DT,
										'' CREATED_BY,
										CAST(GETDATE() as date) CHANGED_DT,
										'' CHANGED_BY,
										''  DISTRIBUTION,
										0  DOCUMENT_ID,
										0  APPROVAL_ID,
										''  DOCUMENT_TYPE,
										''  PROCESS_CODE,
										''  COMPANY_CODE,
										''  SECTION_CODE,
										''  EXTERNAL_FLAG,
										CAST(GETDATE() as date)  DOCUMENT_DATE,
										''  FILE_PATH,
										0 DELETE_FLAG,
										0  DEPARTMENT_ID,
										''  DEPARTMENT_CODE,
										''  DOCUMENT_YEAR,
										'' DIVISION,
										'' IS_APPROVED,
										'' CATEGORY_CODE,
										'' P4D_STATUS_VAL,
										NULL CURRENT_APPROVAL_SEQ,
										NULL TOTAL_APPROVAL_SEQ,
										'' CURRENT_APPROVAL_LABEL,
										'' CURRENT_APPROVER,
										'' CURRENT_APPROVER_NAME
	from TB_R_DOCUMENT

END
GO
