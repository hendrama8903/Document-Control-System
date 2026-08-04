-- =====================================================================
-- Fitur baru: nomor dokumen manual di Document Preparation > Add (Jul 2026).
-- Latar belakang: nomor dokumen (DOCUMENT_CODE) sebelumnya SELALU auto-
-- generate dari TB_M_SEQUENCE (sp_generate_doc_no -> sp_GetNextSeqNo),
-- yang tidak pernah mundur walau draft di tengah dihapus (lihat
-- SQL/DocumentMaintenance_ReclaimDeletedDraftDocNo.sql - itu cuma
-- menangani kasus draft TERAKHIR yang dihapus, bukan draft di tengah).
-- Fitur ini kasih jalan keluar manual: user pilih toggle "Generated"
-- (default, behavior lama tidak berubah) atau "Manual" (isi sendiri 3
-- digit terakhir, prefix tetap dari master seperti biasa, mis.
-- "ITD/SOP-APP-02/006").
--
-- Perubahan:
-- 1) sp_generate_doc_no: tambah parameter opsional @pManualSeq (default
--    NULL = behavior lama tidak berubah) + @pReturnMsg OUTPUT baru.
--    Kalau @pManualSeq diisi:
--      - Prefix dibangun dengan logika SAMA PERSIS seperti mode auto
--        (per level 1/2/3/4).
--      - Validasi: nomor hasil belum dipakai dokumen aktif lain dengan
--        prefix yang sama (DELETE_FLAG=0) - kalau sudah dipakai, ditolak
--        dengan pesan jelas.
--      - TB_M_SEQUENCE untuk prefix itu disamakan NAIK ke nomor manual
--        ini KALAU nomor manual > counter saat ini - supaya nomor
--        "Generated" berikutnya untuk prefix yang sama tidak akan pernah
--        tabrakan dengan nomor manual ini. Kalau nomor manual mengisi
--        nomor bolong yang lebih kecil dari counter, counter TIDAK
--        disentuh.
-- 2) sp_DocumentMaintenance_Insert: parameter baru @MANUAL_SEQ_NO (default
--    NULL), diteruskan ke sp_generate_doc_no. Insert dibatalkan dengan
--    pesan error yang jelas kalau validasi nomor manual gagal.
-- 3) sp_DocumentMaintenance_GetManualNoPrefix (baru): SP read-only untuk
--    preview prefix di form Add sebelum user mengetik angkanya - tidak
--    menyentuh TB_M_SEQUENCE sama sekali.
--
-- Idempotent (CREATE OR ALTER). Jalankan di database DMS_NEW.
-- =====================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_generate_doc_no]
  @pDocLevel AS varchar(10),
	@pDivCode AS varchar(10),
	@pDeptCode AS varchar(10),
	@pSectionCode AS varchar(10),
	@pDocCode AS varchar(10),
  @pProcessCode AS varchar(10),
  @pCompanyCode AS varchar(10),
	@pDate AS DATE,
	@pManualSeq AS INT = NULL,
	@pDocNo AS VARCHAR (25) OUTPUT,
	@pReturnMsg AS VARCHAR(500) OUTPUT

AS
BEGIN
	DECLARE @vDocNo VARCHAR(50);
	DECLARE @vYear NUMERIC;
	DECLARE @vSeq1 int ;
	DECLARE @vSeqType VARCHAR(50);

	Set @vSeqType = 'DOC_NO';
	set @vSeq1 = 0;
	SET @pReturnMsg = NULL;

	IF @pDocLevel = 1
	   BEGIN
	   SET @vYear = YEAR(@pDate);
		 SET @vDocNo = @pCompanyCode + '/' + @pDocCode + '/' + CAST (@vYear as VARCHAR) ;
		 END;
		ELSE
		IF @pDocLevel = 2
		   BEGIN
				 SET @vDocNo = @pCompanyCode + '/' + @pDocCode + '/' + @pProcessCode
			 END;
			 ELSE
		IF @pDocLevel = 4
		   BEGIN
			    SET @vDocNo = @pDivCode + '/'+  @pDocCode + '-' + @pDeptCode + '-' + @pSectionCode
			 END;
			 ELSE
			 IF @pDocLevel = 3
		   BEGIN
			    SET @vDocNo = @pDivCode + '/'+  @pDocCode + '-' + @pDeptCode + '-' + @pSectionCode
			 END;

	IF @pManualSeq IS NOT NULL
	BEGIN
		IF @pManualSeq < 1 OR @pManualSeq > 999
		BEGIN
			SET @pReturnMsg = 'ERROR: Manual document number must be between 1 and 999.';
			RETURN;
		END

		SET @vDocNo = @vDocNo + '/' + RIGHT('000' + CAST (@pManualSeq as VARCHAR(3)),3);

		IF EXISTS (
			SELECT 1 FROM TB_R_DOCUMENT
			WHERE DOCUMENT_CODE = @vDocNo AND ISNULL(DELETE_FLAG, 0) = 0
		)
		BEGIN
			SET @pReturnMsg = 'ERROR: Document number ' + @vDocNo + ' is already in use.';
			RETURN;
		END

		DECLARE @seqPrefix VARCHAR(50) = LEFT(@vDocNo, LEN(@vDocNo) - 4);
		DECLARE @curSeq INT;

		SELECT @curSeq = SEQ_NO FROM TB_M_SEQUENCE WHERE SEQ_TYPE = @vSeqType AND SEQ_CODE = @seqPrefix;

		IF @curSeq IS NULL
		BEGIN
			INSERT INTO TB_M_SEQUENCE (SEQ_TYPE, SEQ_CODE, SEQ_NO, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT)
			VALUES (@vSeqType, @seqPrefix, @pManualSeq, 'System', GETDATE(), 'System', GETDATE());
		END
		ELSE IF @pManualSeq > @curSeq
		BEGIN
			UPDATE TB_M_SEQUENCE
			SET SEQ_NO = @pManualSeq, CHANGED_BY = 'System', CHANGED_DT = GETDATE()
			WHERE SEQ_TYPE = @vSeqType AND SEQ_CODE = @seqPrefix;
		END

		SET @pDocNo = @vDocNo;
		RETURN;
	END

	EXECUTE [dbo].[sp_GetNextSeqNo]
				 			 @SEQ_TYPE  = 'DOC_NO',
						 @SEQ_CODE  = @vDocNo,
	           @LOGIN_USER = 'System',
						 @p_seq_no = @vseq1 OUTPUT

	SET @vDocNo = @vDocNo + '/' +  RIGHT('000' + CAST (@vSeq1 as VARCHAR(3)),3) ;

	set @pDocNo = @vDocNo;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_Insert]
		@DOCUMENT_TRANSACTION_ID int,
		@DOCUMENT_ID varchar(255),
		@DOCUMENT_CODE varchar(50),
		@DOCUMENT_TRANSACTION_NAME varchar(255),
		@DOCUMENT_TYPE varchar(50),
		@PROCESS_CODE varchar(50),
		@COMPANY_CODE varchar(50),
		--@DEPARTMENT_CODE varchar(50),
		@SECTION_CODE varchar(50),
		@ITEM_CHANGED varchar(255),
		@REASON varchar(255),
		@EXTERNAL_FLAG varchar(1),
		@REFERENCE_NO varchar(255),
		@SOURCE varchar(255),
		@DOCUMENT_DATE datetime,
		@FILE_PATH varchar(255),
		@STATUS varchar(1),
		@REVISION int,
		@APPROVAL_ID int,
		@DELETE_FLAG int,
		@DIVISION varchar(255),
		@CLASSIFIED varchar(255),
		@DEPARTMENT_ID int,
		@IMPACT_OTHER_FLAG CHAR(1),
		@LEVEL_CODE int,
		@CREATED_BY varchar(50),
		@CHANGED_BY varchar(50),
		@MENU_ID varchar(50),
		@DOCUMENT_CREATOR VARCHAR(50),
		@MANUAL_SEQ_NO INT = NULL,
		@RETURN_MSG VARCHAR(MAX) OUTPUT,
		@RETURN_ID	 		VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT
	, @LOCATION VARCHAR(255) = 'sp_DocumentMaintenance_Insert'
	, @rev_DOCUMENT_TRANSACTION_ID int
	, @rev_DOCUMENT_ID int
	, @rev_REVISION int
	, @rev_STATUS varchar(1)

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Maintenance', 'Insert', @LOCATION, @CREATED_BY

	IF ISNULL(@DOCUMENT_TYPE, '') = '' OR ISNULL(@DOCUMENT_TYPE, 'null') = 'null'
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document Type should not be null';
		SET @RETURN_ID = 0;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

		IF @DOCUMENT_TYPE = '02'
		BEGIN
			IF @DOCUMENT_CODE IS NULL OR LEN(@DOCUMENT_CODE) < 1
			BEGIN
				SET @RETURN_MSG = 'ERROR: Document No should not be null';
				SET @RETURN_ID = 0;
				EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
				RETURN 0;
			END
		END

		IF @LEVEL_CODE IS NULL OR LEN(@LEVEL_CODE) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Document Level should not be null';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		IF @DOCUMENT_TRANSACTION_NAME IS NULL OR LEN(@DOCUMENT_TRANSACTION_NAME) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Document Name should not be null';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		IF @CLASSIFIED IS NULL OR LEN(@CLASSIFIED) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Classified Code should not be null';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		IF @COMPANY_CODE IS NULL OR LEN(@COMPANY_CODE) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Company Code should not be null';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		IF @LEVEL_CODE = '2'
		BEGIN
			IF @PROCESS_CODE IS NULL OR LEN(@PROCESS_CODE) < 1
			BEGIN
				SET @RETURN_MSG = 'ERROR: Process Code should not be null';
				SET @RETURN_ID = 0;
				EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
				RETURN 0;
			END
		END

		IF @DIVISION IS NULL OR LEN(@DIVISION) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Division Code should not be null';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		IF @DEPARTMENT_ID IS NULL OR LEN(@DEPARTMENT_ID) < 1
		BEGIN
			SET @RETURN_MSG = 'ERROR: Department Code should not be null';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		IF RIGHT(@DOCUMENT_CODE, 1) IN ('3', '4')
		BEGIN
			IF @SECTION_CODE IS NULL OR LEN(@SECTION_CODE) < 1
			BEGIN
				SET @RETURN_MSG = 'ERROR: Section should not be null';
				SET @RETURN_ID = 0;
				EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
				RETURN 0;
			END
		END

	IF @DOCUMENT_DATE IS NULL OR LEN(@DOCUMENT_DATE) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Date should not be null';
		SET @RETURN_ID = 0;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

	IF @DOCUMENT_CREATOR IS NULL OR LEN(@DOCUMENT_CREATOR) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document Creator should not be null';
		SET @RETURN_ID = 0;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

	IF @FILE_PATH IS NULL OR LEN(@FILE_PATH) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document Upload should not be null';
		SET @RETURN_ID = 0;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

	IF @DOCUMENT_TYPE = '02' --REVISION
	BEGIN
		SELECT @rev_DOCUMENT_TRANSACTION_ID = DOCUMENT_TRANSACTION_ID
			,@rev_DOCUMENT_ID = DOCUMENT_ID, @rev_REVISION = REVISION, @rev_STATUS = STATUS
		FROM TB_R_DOCUMENT
		WHERE DOCUMENT_CODE = @DOCUMENT_CODE ORDER BY REVISION DESC

		IF @rev_STATUS = '0'
		BEGIN
			SET @RETURN_MSG = 'ERROR: A revision is already pending approval for this document';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		IF NOT EXISTS (
			SELECT 1 FROM TB_R_CTRL_DOCUMENT ctrl
			WHERE ctrl.DOCUMENT_CODE = @DOCUMENT_CODE
			AND ISNULL(ctrl.DELETE_FLAG, 0) = 0
			AND ctrl.OPERATION_TYPE = 1
		)
		BEGIN
			SET @RETURN_MSG = 'ERROR: This document has not been registered in P4D Maintenance yet - register it in P4DMaintenance before creating a revision';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END
	END

	IF @DOCUMENT_TYPE = '01' --IF NEW, GENERATE DOC NO
	BEGIN
			-- Updated By Arie (2023-08-24)
			DECLARE @DEPARTMENT_CODE VARCHAR(50), @DOC_CODE VARCHAR(50);
			SET @DEPARTMENT_CODE = (SELECT DEPARTMENT_CODE FROM TB_M_DEPARTMENT WHERE DEPARTMENT_ID = @DEPARTMENT_ID);
			SET @DOC_CODE = (SELECT DOCUMENT_CODE FROM TB_M_DOCUMENT WHERE DOCUMENT_ID = @DOCUMENT_ID);
			SET @rev_DOCUMENT_ID = @DOCUMENT_ID

			DECLARE @genDocNoMsg VARCHAR(500);
			EXEC sp_generate_doc_no
				@LEVEL_CODE,
				@DIVISION,
				@DEPARTMENT_CODE,
				@SECTION_CODE,
				@DOC_CODE,
				@PROCESS_CODE,
				@COMPANY_CODE,
				@DOCUMENT_DATE,
				@MANUAL_SEQ_NO,
				@DOCUMENT_CODE OUTPUT,
				@genDocNoMsg OUTPUT

			IF @genDocNoMsg IS NOT NULL
			BEGIN
				SET @RETURN_MSG = @genDocNoMsg;
				SET @RETURN_ID = 0;
				EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
				RETURN 0;
			END
	END

	-- Create Workflow Document
	DECLARE @RETVAL INT;
	EXEC @RETVAL = sp_WorkflowDoc_Create @LEVEL_CODE, @CREATED_BY, NULL, @MENU_ID, @DOCUMENT_CREATOR, @RETURN_MSG OUTPUT, @APPROVAL_ID OUTPUT
	IF @RETVAL = 1
	BEGIN
		EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @CREATED_BY
	END
	ELSE BEGIN
	SET @RETURN_ID = 0;
	RETURN 0;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

	INSERT INTO [dbo].[TB_R_DOCUMENT]
           ([DOCUMENT_CODE]
           ,[DOCUMENT_TRANSACTION_NAME]
           ,[DOCUMENT_TYPE]
           ,[PROCESS_CODE]
           ,[COMPANY_CODE]
           ,[SECTION_CODE]
           ,[ITEM_CHANGED]
           ,[REASON]
           ,[EXTERNAL_FLAG]
           ,[REFERENCE_NO]
           ,[SOURCE]
           ,[DOCUMENT_DATE]
           ,[FILE_PATH]
           ,[STATUS]
           ,[REVISION]
           ,[APPROVAL_ID]
           ,[LEVEL_CODE]
           ,[DOCUMENT_ID]
           ,[DELETE_FLAG]
           ,[CREATED_BY]
           ,[CREATED_DT]
           ,[CHANGED_BY]
           ,[CHANGED_DT]
		   ,DIVISION
		   ,CLASSIFIED
		   ,DEPARTMENT
			 ,IMPACT_OTHER_FLAG
			 ,DOCUMENT_CREATOR)
     VALUES
           (@DOCUMENT_CODE
           ,@DOCUMENT_TRANSACTION_NAME
           ,@DOCUMENT_TYPE
           ,@PROCESS_CODE
           ,@COMPANY_CODE
           ,@SECTION_CODE
           ,@ITEM_CHANGED
           ,@REASON
           ,@EXTERNAL_FLAG
           ,@REFERENCE_NO
           ,@SOURCE
           ,@DOCUMENT_DATE
           ,@FILE_PATH
           ,0--@STATUS
           ,IIF(@DOCUMENT_TYPE = '02', @rev_REVISION + 1, 0)
           ,@APPROVAL_ID
           ,@LEVEL_CODE
           ,@rev_DOCUMENT_ID
           ,0
           ,@CREATED_BY
           ,GETDATE()
           ,@CHANGED_BY
           ,GETDATE()
		   ,@DIVISION
		   ,@CLASSIFIED
		   ,@DEPARTMENT_ID
			 ,@IMPACT_OTHER_FLAG
			 ,@DOCUMENT_CREATOR)

	DECLARE @curr_DOCUMENT_TRANSACTION_ID int
	SET @curr_DOCUMENT_TRANSACTION_ID = SCOPE_IDENTITY();

	IF @DOCUMENT_TYPE = '02' --REVISION
	BEGIN
		INSERT INTO [dbo].[TB_R_DOCUMENT_DISTRIBUTION]
           ([DOCUMENT_TRANSACTION_ID]
           ,[DEPARTMENT_ID]
           ,[DISTRIBUTION_DATE]
           ,[STATUS]
           ,[CREATED_BY]
           ,[CREATED_DT]
           ,[CHANGED_BY]
           ,[CHANGED_DT])
     SELECT
            @curr_DOCUMENT_TRANSACTION_ID
           ,DEPARTMENT_ID
           ,NULL
           ,0
           ,@CREATED_BY
           ,GETDATE()
           ,@CHANGED_BY
           ,GETDATE()
		FROM TB_R_DOCUMENT_DISTRIBUTION
		WHERE DOCUMENT_TRANSACTION_ID = @rev_DOCUMENT_TRANSACTION_ID

		UPDATE TB_R_CTRL_DOCUMENT
		SET DELETE_FLAG = '1'
		WHERE DOCUMENT_TRANSACTION_ID = @rev_DOCUMENT_TRANSACTION_ID
	END

	UPDATE TB_R_APPROVAL_H
	SET TRANSACTION_ID = @curr_DOCUMENT_TRANSACTION_ID
	WHERE APPROVAL_ID = @APPROVAL_ID

	SET @RETURN_ID = @curr_DOCUMENT_TRANSACTION_ID
	SET @RETURN_MSG = 'Successfully Save Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @CREATED_BY
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_ID = 0;
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
	RETURN 0;
END CATCH
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_GetManualNoPrefix]
	@LEVEL_CODE int,
	@DIVISION varchar(255),
	@DEPARTMENT_ID int,
	@SECTION_CODE varchar(50),
	@DOCUMENT_ID varchar(255),
	@PROCESS_CODE varchar(50),
	@COMPANY_CODE varchar(50),
	@DOCUMENT_DATE datetime
AS
BEGIN
	DECLARE @DEPARTMENT_CODE VARCHAR(50), @DOC_CODE VARCHAR(50), @vDocNo VARCHAR(50), @vYear NUMERIC;

	SET @DEPARTMENT_CODE = (SELECT DEPARTMENT_CODE FROM TB_M_DEPARTMENT WHERE DEPARTMENT_ID = @DEPARTMENT_ID);
	SET @DOC_CODE = (SELECT DOCUMENT_CODE FROM TB_M_DOCUMENT WHERE DOCUMENT_ID = @DOCUMENT_ID);

	IF @LEVEL_CODE = 1
	BEGIN
		SET @vYear = YEAR(@DOCUMENT_DATE);
		SET @vDocNo = @COMPANY_CODE + '/' + @DOC_CODE + '/' + CAST(@vYear AS VARCHAR);
	END
	ELSE IF @LEVEL_CODE = 2
	BEGIN
		SET @vDocNo = @COMPANY_CODE + '/' + @DOC_CODE + '/' + @PROCESS_CODE;
	END
	ELSE IF @LEVEL_CODE IN (3, 4)
	BEGIN
		SET @vDocNo = @DIVISION + '/' + @DOC_CODE + '-' + @DEPARTMENT_CODE + '-' + @SECTION_CODE;
	END

	SELECT @vDocNo AS PREFIX;
END
GO
