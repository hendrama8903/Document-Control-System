-- =====================================================================
-- Tambah kolom P4D_STATUS_VAL ke entity DocumentMaintenance (TB_R_DOCUMENT)
-- supaya grid DocumentMaintenance/Index bisa menampilkan status dokumen
-- di P4D (Draft/On Progress/Received/Rejected/Sent/Partially Sent/Deleted)
-- tanpa harus buka menu P4D terpisah.
--
-- PENTING: model C# DocumentMaintenance.cs dipakai bareng (FromSqlRaw)
-- oleh BEBERAPA stored procedure berbeda, bukan cuma
-- sp_DocumentMaintenance_Search. EF Core FromSqlRaw<T> mewajibkan SEMUA
-- kolom yang di-map ke property T ada di hasil query, kalau tidak akan
-- error runtime "required column was not present" (lihat memory:
-- dms-fromsqlraw-column-parity-bug - job reminder dokumen pernah gagal
-- 129x gara-gara pola ini). Makanya di sini SEMUA SP yang dipetakan ke
-- DocumentMaintenance ikut ditambah kolom P4D_STATUS_VAL (isinya blank/
-- NULL kalau memang tidak relevan untuk SP tsb) - bukan cuma satu SP.
--
-- Jalankan di database DMS_NEW
-- =====================================================================

-- 1) sp_DocumentMaintenance_Search - grid utama DocumentMaintenance/Index.
--    Nilai P4D_STATUS_VAL = label status dari baris TB_R_CTRL_DOCUMENT
--    TERBARU (DOCUMENT_CTRL_ID terbesar) yang belum dihapus untuk
--    DOCUMENT_CODE tsb. Kosong ('') kalau belum pernah didaftarkan ke P4D.
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
										DCAT.DOCUMENT_NAME,
										ISNULL((
											SELECT TOP 1 CSYS.SYSTEM_VALUE
											FROM TB_R_CTRL_DOCUMENT CTRL
											LEFT JOIN TB_M_SYSTEM CSYS
												ON CSYS.SYSTEM_TYPE = ''DOC_CTRL_STATUS'' AND CSYS.SYSTEM_CODE = CTRL.STATUS
											WHERE CTRL.DOCUMENT_CODE = S.DOCUMENT_CODE
											AND ISNULL(CTRL.DELETE_FLAG, 0) = 0
											ORDER BY CTRL.DOCUMENT_CTRL_ID DESC
										), '''') P4D_STATUS_VAL
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

-- 2) sp_DocumentMaintenance_GetDueForReview - dipakai job reminder email,
--    tidak perlu nilai P4D asli, cukup NULL supaya kolom entity terpenuhi.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_GetDueForReview]
    @DAYS_AHEAD             INT,
    @REMINDER_THROTTLE_DAYS INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        S.DOCUMENT_TRANSACTION_ID,
        S.DOCUMENT_CODE,
        S.DOCUMENT_TRANSACTION_NAME,
        S.CLASSIFIED,
        CLSYS.SYSTEM_VALUE AS CLASSIFIED_VAL,
        ISNULL(S.STATUS, '0') AS STATUS,
        SYS.SYSTEM_VALUE AS STATUS_DISPLAY,
        ISNULL(S.REVISION, 0) AS REVISION,
        S.NEXT_REVIEW_DATE,
        ISNULL(S.ITEM_CHANGED, '') AS ITEM_CHANGED,
        ISNULL(S.REASON, '') AS REASON,
        ISNULL(S.REFERENCE_NO, '') AS REFERENCE_NO,
        ISNULL(S.SOURCE, '') AS SOURCE,
        S.CREATED_DT,
        S.CREATED_BY,
        S.CHANGED_DT,
        S.CHANGED_BY,
        '' AS DISTRIBUTION,
        S.APPROVAL_ID,
        S.DOCUMENT_TYPE,
        S.PROCESS_CODE,
        S.COMPANY_CODE,
        S.SECTION_CODE,
        SEC.SECTION_NAME,
        ISNULL(S.EXTERNAL_FLAG, '') AS EXTERNAL_FLAG,
        CAST(S.DOCUMENT_DATE AS DATE) AS DOCUMENT_DATE,
        ISNULL((SELECT MIN(HS.DOCUMENT_DATE) FROM TB_R_DOCUMENT_HISTORY HS WHERE HS.REVISION = 0 AND HS.DOCUMENT_CODE = S.DOCUMENT_CODE), S.DOCUMENT_DATE) AS DOCUMENT_REVISION_0_DATE,
        ISNULL(S.FILE_PATH, '') AS FILE_PATH,
        ISNULL(S.DELETE_FLAG, 0) AS DELETE_FLAG,
        ISNULL(S.DEPARTMENT, 0) AS DEPARTMENT_ID,
        D.DEPARTMENT_CODE AS DEPARTMENT_CODE,
        D.DEPARTMENT_NAME AS DEPARTMENT_NAME,
        CAST(YEAR(S.DOCUMENT_DATE) AS VARCHAR(4)) AS DOCUMENT_YEAR,
        S.DIVISION AS DIVISION,
        DSYS.DIVISION_NAME,
        '' AS IS_APPROVED,
        S.IMPACT_OTHER_FLAG,
        S.LEVEL_CODE,
        S.DOCUMENT_ID,
        S.DOCUMENT_CREATOR,
        DCAT.DOCUMENT_NAME,
        NULL AS P4D_STATUS_VAL
    FROM [dbo].[TB_R_DOCUMENT] S
    LEFT JOIN TB_M_DEPARTMENT D ON D.DEPARTMENT_ID = S.DEPARTMENT
    LEFT JOIN TB_M_SECTION SEC ON SEC.SECTION_CODE = S.SECTION_CODE AND SEC.DEPARTMENT_CODE = D.DEPARTMENT_CODE AND GETDATE() BETWEEN SEC.VALID_FROM AND SEC.VALID_TO AND SEC.DELETE_FLAG != 1
    LEFT JOIN TB_M_SYSTEM SYS ON SYS.SYSTEM_TYPE = 'DOC_STATUS' AND SYS.SYSTEM_CODE = ISNULL(S.STATUS, '0')
    LEFT JOIN TB_M_SYSTEM CLSYS ON CLSYS.SYSTEM_TYPE = 'CLASSIFIED' AND CLSYS.SYSTEM_CODE = ISNULL(S.CLASSIFIED, '0')
    LEFT JOIN TB_M_DIVISION DSYS ON DSYS.DIVISION_CODE = ISNULL(S.DIVISION, '0')
    LEFT JOIN TB_M_DOCUMENT DCAT ON DCAT.DOCUMENT_ID = S.DOCUMENT_ID
    WHERE ISNULL(S.DELETE_FLAG, 0) = 0
    AND S.STATUS = '1'
    AND S.NEXT_REVIEW_DATE IS NOT NULL
    AND S.NEXT_REVIEW_DATE <= DATEADD(DAY, @DAYS_AHEAD, GETDATE())
    AND (S.LAST_REVIEW_REMINDER_DT IS NULL OR S.LAST_REVIEW_REMINDER_DT <= DATEADD(DAY, -@REMINDER_THROTTLE_DAYS, GETDATE()));

END
GO

-- 3) sp_DocumentHistory_Search - hasil dimap ke DocumentMaintenance juga
--    (DocumentMaintenanceRepo.SearchHistoryToMaintenance) - baris histori
--    lama, P4D_STATUS_VAL dikosongkan saja.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentHistory_Search]
	@DOCUMENT_TRANSACTION_ID 	INT,
	@DOCUMENT_TRANSACTION_NAME  		VARCHAR(255),
	@DOCUMENT_CODE			VARCHAR(255),
	@DIVISION				  	VARCHAR(50),
	@DEPARTMENT_ID	  	INT,
	@DEPARTMENT_CODE  	VARCHAR(5),
	@PageNumber 				int,
	@PageSize 					int
AS
BEGIN

	DECLARE @QUERY VARCHAR(MAX)

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
										0 AS DELETE_FLAG,
										DCAT.DOCUMENT_NAME,
										'''' P4D_STATUS_VAL
									FROM [dbo].[TB_R_DOCUMENT_HISTORY] S
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
									WHERE 1 = 1 '

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

-- 4) sp_DocumentMaintenance_GenerateDocumentNo - baris sintetis preview
--    nomor dokumen baru, P4D_STATUS_VAL dikosongkan.
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
										'' P4D_STATUS_VAL
	from TB_R_DOCUMENT

END
GO

-- 5) sp_MDepartment_Search - baris department dipetakan ke DocumentMaintenance
--    juga (DocumentMaintenanceRepo.SearchDepartmentByDivision), P4D_STATUS_VAL
--    dikosongkan.
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
										'''' P4D_STATUS_VAL
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
