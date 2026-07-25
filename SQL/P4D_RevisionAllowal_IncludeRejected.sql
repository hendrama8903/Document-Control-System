-- =====================================================================
-- Fix: dokumen yang REJECTED di P4D harus tetap bisa direvisi lewat
-- DocumentMaintenance "Add Document" -> Document Type = Revision.
--
-- Sebelumnya (fix tanggal sebelum ini) dropdown Revision
-- (GetDocumentNoAllowedRevision, sp_DocumentMaintenance_Search dengan
-- REVISION_ALLOWAL_FLAG='1') hanya menampilkan dokumen yang status P4D
-- control-nya RECEIVED (2). Ternyata dokumen yang REJECTED (3) di P4D
-- juga harus tetap muncul di dropdown ini, supaya user bisa membuat
-- revisi baru (REVISION+1) untuk dokumen tsb - revisi baru itu akan
-- melalui approval ulang dari awal seperti biasa sebelum didaftarkan
-- lagi ke P4D. Yang TIDAK boleh muncul: dokumen yang P4D control-nya
-- masih Draft/On Progress/Sent/Partially Sent (belum ada keputusan
-- akhir Received/Rejected).
--
-- Jalankan di database DMS_NEW
-- =====================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_Search]
	@DOCUMENT_TRANSACTION_ID 	INT,
	@DOCUMENT_CODE			VARCHAR(255),
	@DIVISION				  	VARCHAR(50),
	@DEPARTMENT_ID	  	INT,
	@DEPARTMENT_CODE  	VARCHAR(5),
	@DOCUMENT_TRANSACTION_NAME  		VARCHAR(255),
	@YEAR								VARCHAR(4),
	@STATUS							CHAR,
	@NOT_EXIST_FLAG 		char,
	@REVISION_ALLOWAL_FLAG	char,
	@USERNAME			  		VARCHAR(255),
	@PageNumber 				int,
	@PageSize 					int
AS
BEGIN

	DECLARE @QUERY VARCHAR(MAX)
	DECLARE @DIVISION_COUNT INT
	DECLARE @DEPARTMENT_COUNT INT

	SET @QUERY = 'WITH data AS
								(
									SELECT
										ROW_NUMBER() OVER (ORDER BY S.DOCUMENT_TRANSACTION_ID DESC) as RowNumber,
										S.DOCUMENT_TRANSACTION_ID,
										S.DOCUMENT_CODE,
										S.DOCUMENT_TRANSACTION_NAME,
										S.CLASSIFIED,
										CLSYS.SYSTEM_VALUE AS CLASSIFIED_VAL,
										ISNULL(S.STATUS, ''0'') STATUS,
										SYS.SYSTEM_VALUE STATUS_DISPLAY,
										ISNULL(S.REVISION, 0) REVISION,
						S.NEXT_REVIEW_DATE,
										ISNULL(S.ITEM_CHANGED, '''') ITEM_CHANGED,
										ISNULL(S.REASON, '''') REASON,
										ISNULL(S.REFERENCE_NO, '''') REFERENCE_NO,
										ISNULL(S.SOURCE, '''') SOURCE,
										S.CREATED_DT,
										S.CREATED_BY,
										S.CHANGED_DT,
										S.CHANGED_BY,
										'''' DISTRIBUTION,
										S.APPROVAL_ID APPROVAL_ID,
										S.DOCUMENT_TYPE DOCUMENT_TYPE,
										S.PROCESS_CODE PROCESS_CODE,
										S.COMPANY_CODE COMPANY_CODE,
										S.SECTION_CODE SECTION_CODE,
										SEC.SECTION_NAME,
										ISNULL(S.EXTERNAL_FLAG, '''') EXTERNAL_FLAG,
										CAST(S.DOCUMENT_DATE as date) DOCUMENT_DATE,
										ISNULL((SELECT MIN(HS.DOCUMENT_DATE) FROM TB_R_DOCUMENT_HISTORY HS WHERE HS.REVISION = 0 AND HS.DOCUMENT_CODE = S.DOCUMENT_CODE), S.DOCUMENT_DATE)  DOCUMENT_REVISION_0_DATE,
										ISNULL(S.FILE_PATH, '''') FILE_PATH,
										ISNULL(S.DELETE_FLAG, 0) DELETE_FLAG,
										ISNULL(S.DEPARTMENT, 0) DEPARTMENT_ID,
										D.DEPARTMENT_CODE AS DEPARTMENT_CODE,
										D.DEPARTMENT_NAME AS DEPARTMENT_NAME,
										CAST(YEAR(S.DOCUMENT_DATE) as varchar(4)) DOCUMENT_YEAR,
										S.DIVISION DIVISION,
										DSYS.DIVISION_NAME,
										'''' IS_APPROVED,
										S.IMPACT_OTHER_FLAG,
										S.LEVEL_CODE,
										S.DOCUMENT_ID,
										S.DOCUMENT_CREATOR,
										DCAT.DOCUMENT_NAME
									FROM [dbo].[TB_R_DOCUMENT] S
									LEFT JOIN TB_M_DEPARTMENT D
										ON D.DEPARTMENT_ID = S.DEPARTMENT
									LEFT JOIN TB_M_SECTION SEC
										ON SEC.SECTION_CODE = S.SECTION_CODE AND SEC.DEPARTMENT_CODE = D.DEPARTMENT_CODE AND GETDATE() BETWEEN SEC.VALID_FROM AND SEC.VALID_TO AND SEC.DELETE_FLAG != 1
									LEFT JOIN TB_M_SYSTEM SYS
										on SYS.SYSTEM_TYPE = ''DOC_STATUS''
											AND SYS.SYSTEM_CODE = ISNULL(S.STATUS, ''0'')
									LEFT JOIN TB_M_SYSTEM CLSYS
										on CLSYS.SYSTEM_TYPE = ''CLASSIFIED''
											AND CLSYS.SYSTEM_CODE = ISNULL(S.CLASSIFIED, ''0'')
									LEFT JOIN TB_M_DIVISION DSYS
										on DSYS.DIVISION_CODE = ISNULL(S.DIVISION, ''0'')
									LEFT JOIN TB_M_DOCUMENT DCAT
										on DCAT.DOCUMENT_ID = S.DOCUMENT_ID
									WHERE 1 = 1
									AND ISNULL(S.DELETE_FLAG, 0) = 0'

									IF ISNULL(@DIVISION, '') <> ''
									BEGIN
										SET @QUERY += ' AND D.DIVISION LIKE ''' + REPLACE(@DIVISION , '*', '%') + ''' '
										SET @QUERY += ' '
									END

									IF ISNULL(@DEPARTMENT_ID, '') <> ''
									BEGIN
										SET @QUERY += ' AND D.DEPARTMENT_ID LIKE ''' + REPLACE(@DEPARTMENT_ID , '*', '%') + ''' '
										SET @QUERY += ' '
									END

									IF ISNULL(@DEPARTMENT_CODE, '') <> ''
									BEGIN
										SET @QUERY += ' AND D.DEPARTMENT_CODE LIKE ''' + REPLACE(@DEPARTMENT_CODE , '*', '%') + ''' '
										SET @QUERY += ' '
									END

									IF ISNULL(@DOCUMENT_TRANSACTION_NAME, '') <> ''
									BEGIN
										SET @QUERY += ' AND S.DOCUMENT_TRANSACTION_NAME LIKE ''' + REPLACE(@DOCUMENT_TRANSACTION_NAME , '*', '%') + ''' '
									END

									IF ISNULL(@DOCUMENT_TRANSACTION_ID, '') <> '' --@DOCUMENT_NO IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DOCUMENT_TRANSACTION_ID LIKE ''' + REPLACE(@DOCUMENT_TRANSACTION_ID , '*', '%') + ''' '
									END

									IF ISNULL(@DOCUMENT_CODE, '') <> '' --@DOCUMENT_NO IS NOT NULL
									BEGIN
										SET @QUERY += ' AND S.DOCUMENT_CODE LIKE ''' + REPLACE(@DOCUMENT_CODE , '*', '%') + ''' '
									END

									IF ISNULL(@STATUS, '') <> '' --@DOCUMENT_NO IS NOT NULL
									BEGIN
										SET @QUERY += ' AND (S.STATUS LIKE ''' + REPLACE(@STATUS , '*', '%') + ''') '
									END

									IF ISNULL(@YEAR, '') <> ''  --@YEAR IS NOT NULL
										BEGIN
										SET @QUERY += ' AND YEAR(S.DOCUMENT_DATE) = ''' + REPLACE(@YEAR , '*', '') + ''' '
									END

									IF ISNULL(@USERNAME, '') <> ''  --@YEAR IS NOT NULL
										BEGIN
										SELECT @DIVISION_COUNT = COUNT(DIVISION) FROM TB_M_USER_POS WHERE USERNAME = @USERNAME;

										IF @DIVISION_COUNT > 0
										BEGIN
											SET @QUERY += ' AND S.DIVISION IN (SELECT DIVISION FROM TB_M_USER_POS WHERE USERNAME = '''+ @USERNAME+''') '
										END
									END

									IF ISNULL(@USERNAME, '') <> ''  --@YEAR IS NOT NULL
										BEGIN
										SELECT @DEPARTMENT_COUNT = COUNT(DEPARTMENT_ID) FROM TB_M_USER_POS WHERE USERNAME = @USERNAME;

										IF @DEPARTMENT_COUNT > 0
										BEGIN
											SET @QUERY += ' AND S.DEPARTMENT IN (SELECT DEPARTMENT_ID FROM TB_M_USER_POS WHERE USERNAME = '''+ @USERNAME+''') '
										END
									END

										IF @NOT_EXIST_FLAG = '1'
										BEGIN
											SET @QUERY += ' AND NOT EXISTS (
															SELECT 1
															FROM TB_R_CTRL_DOCUMENT ctrl
															WHERE ctrl.DOCUMENT_CODE = S.DOCUMENT_CODE
															AND ctrl.DELETE_FLAG = ''0'' AND ctrl.OPERATION_TYPE = ''1''
														)';
										END

										IF @REVISION_ALLOWAL_FLAG = '1'
										BEGIN
											-- Phase (Jul 2026): Published (''5'') documents are current/effective too, same
											-- as Approved (''1'') - both are revisable. A revision may only be picked here
											-- once its P4D control entry reached a final QMS decision: RECEIVED (''2'') or
											-- REJECTED (''3''). A document still Draft/On Progress/Sent/Partially Sent in
											-- P4D (no final decision yet) is not eligible, even though TB_R_DOCUMENT itself
											-- still shows Approved/Published.
											SET @QUERY += ' AND S.STATUS IN (''1'', ''5'') '
											SET @QUERY += ' AND EXISTS (
															SELECT 1
															FROM TB_R_CTRL_DOCUMENT ctrl
															WHERE ctrl.DOCUMENT_CODE = S.DOCUMENT_CODE
															AND ISNULL(ctrl.DELETE_FLAG, 0) = 0 AND ctrl.OPERATION_TYPE = 1
															AND ctrl.STATUS IN (2, 3)
														)';
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
