SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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
		-- Fitur "Divisi Terkait" (SPR/SIPOCOR Level 2, request Hendra 2026-08-20) -
		-- daftar "KODE:ROLE" dipisah koma (mis. 'CED:MAIN_PIC,FAD:RELATED,
		-- ITD:NOTE_RELATED'), NULL/kosong kalau dokumen ini tidak pakai fitur
		-- Divisi Terkait. ROLE kosong/tanpa ':' dianggap 'RELATED' (default lama,
		-- backward compatible).
		@RELATED_DIVISIONS VARCHAR(MAX) = NULL,
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

		-- Obsolete-control fix (Jul 2026): dokumen lama TIDAK lagi diarsipkan/dihapus di
		-- sini. Ia tetap current sampai revisi baru ini benar-benar disetujui
		-- (lihat sp_DocumentMaintenance_SupersedeRevision, dipanggil dari langkah
		-- approval terakhir). Guard di bawah mencegah dua draft revisi berjalan
		-- bersamaan untuk dokumen yang sama.
		IF @rev_STATUS = '0'
		BEGIN
			SET @RETURN_MSG = 'ERROR: A revision is already pending approval for this document';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END

		-- P4D-gated revision (Jul 2026, guard tetap - side effect pindah Aug 2026):
		-- a revision may only be started once the CURRENT revision of this document
		-- has been registered in P4D Maintenance (an active, non-deleted
		-- TB_R_CTRL_DOCUMENT row with OPERATION_TYPE = 1 - same definition of
		-- "registered" used by GetDocumentCodeLoginBased's NOT_EXIST_FLAG filter).
		-- CUMA DICEK di sini, TIDAK DIHAPUS - dokumen lama (rev sebelumnya) masih
		-- STATUS Approved/Published dan masih beredar resmi selama revisi ini masih
		-- Draft/pending approval, jadi registrasi P4D-nya harus tetap terlihat di
		-- grid P4D Maintenance selama itu (dulu baris ini langsung soft-delete di
		-- sini, bikin dokumen yang masih sah hilang dari P4D selama revisi pending -
		-- request Hendra 2026-08-16, ditemukan lewat pengujian). Retire registrasi
		-- P4D yang lama sekarang dipindah ke sp_DocumentMaintenance_SupersedeRevision,
		-- bareng saat dokumen lama benar-benar digantikan (revisi ini disetujui
		-- penuh) - bukan saat revisi BARU DIBUAT. Double-revision (2 draft revisi
		-- berjalan bersamaan) sudah dicegah terpisah oleh guard @rev_STATUS = '0'
		-- di atas, jadi tidak perlu soft-delete P4D untuk itu.
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

	-- Divisi Terkait (SPR/SIPOCOR Level 2) - validasi tiap divisi terkait
	-- punya Div Head terdaftar (POSITION_ID=4), gagal cepat kalau tidak,
	-- SEBELUM dokumen jadi dibuat. TIDAK LAGI menambah langkah ke approval
	-- chain (TB_R_APPROVAL_D) - acknowledgment "Mengetahui" sekarang
	-- sepenuhnya terpisah, dilacak lewat kolom ACKNOWLEDGED_* di
	-- TB_R_DOCUMENT_RELATED_DIVISION sendiri (lihat
	-- sp_DocumentMaintenance_AcknowledgeRelatedDivision) - bukan bagian dari
	-- approval berjenjang biasa lagi (request Hendra 2026-08-20, koreksi
	-- desain sebelumnya yang menumpangkannya ke TB_R_APPROVAL_D).
	IF @RELATED_DIVISIONS IS NOT NULL AND LEN(@RELATED_DIVISIONS) > 0
	BEGIN
		DECLARE @missingDivision VARCHAR(10);

		-- "KODE:ROLE" -> ambil KODE saja (bagian sebelum ':') buat validasi Div
		-- Head - ROLE-nya tidak relevan di sini, semua role tetap perlu Div Head
		-- terdaftar.
		SELECT TOP 1 @missingDivision = LEFT(v, pos - 1)
		FROM (
			SELECT LTRIM(RTRIM(value)) AS v
			FROM STRING_SPLIT(@RELATED_DIVISIONS, ',')
			WHERE LEN(LTRIM(RTRIM(value))) > 0
		) trimmed
		CROSS APPLY (SELECT CHARINDEX(':', v + ':') AS pos) p
		WHERE NOT EXISTS (
				SELECT 1 FROM [dbo].[TB_M_USER_POS]
				WHERE POSITION_ID = 4 AND DIVISION = LEFT(v, pos - 1)
			);

		IF @missingDivision IS NOT NULL
		BEGIN
			SET @RETURN_MSG = 'ERROR: Div. Head belum terdaftar untuk divisi ' + @missingDivision + ' - tidak bisa menambahkan Divisi Terkait.';
			SET @RETURN_ID = 0;
			EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
			RETURN 0;
		END
	END

	-- Snapshot nama Division/Department/Section (request Hendra 2026-08-22) -
	-- dibekukan SEKALI di sini saat record dibuat, supaya rename nama di
	-- Master Data tidak ikut mengubah tampilan dokumen yang sudah ada (lihat
	-- SQL/OrgMasterData_NameSnapshot_Migration.sql). Kode/ID tetap sumber
	-- kebenaran untuk relasi - kolom ini murni untuk display/print histori.
	DECLARE @snap_DIVISION_NAME VARCHAR(255), @snap_DEPARTMENT_CODE VARCHAR(5),
					@snap_DEPARTMENT_NAME VARCHAR(255), @snap_SECTION_NAME VARCHAR(255);

	SELECT @snap_DIVISION_NAME = DIV.DIVISION_NAME
	FROM [dbo].[TB_M_DIVISION] DIV WHERE DIV.DIVISION_CODE = @DIVISION;

	SELECT @snap_DEPARTMENT_CODE = DEP.DEPARTMENT_CODE, @snap_DEPARTMENT_NAME = DEP.DEPARTMENT_NAME
	FROM [dbo].[TB_M_DEPARTMENT] DEP WHERE DEP.DEPARTMENT_ID = @DEPARTMENT_ID;

	SELECT @snap_SECTION_NAME = SEC.SECTION_NAME
	FROM [dbo].[TB_M_SECTION] SEC WHERE SEC.SECTION_CODE = @SECTION_CODE AND SEC.DEPARTMENT_CODE = @snap_DEPARTMENT_CODE;

	INSERT INTO [dbo].[TB_R_DOCUMENT]
           ([DOCUMENT_CODE]
           ,[DOCUMENT_TRANSACTION_NAME]
           ,[DOCUMENT_TYPE]
           ,[PROCESS_CODE]
           ,[COMPANY_CODE]
           ,[SECTION_CODE]
           ,[SECTION_NAME]
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
		   ,DIVISION_NAME
		   ,CLASSIFIED
		   ,DEPARTMENT
		   ,DEPARTMENT_CODE
		   ,DEPARTMENT_NAME
			 ,IMPACT_OTHER_FLAG
			 ,DOCUMENT_CREATOR)
     VALUES
           (@DOCUMENT_CODE
           ,@DOCUMENT_TRANSACTION_NAME
           ,@DOCUMENT_TYPE
           ,@PROCESS_CODE
           ,@COMPANY_CODE
           ,@SECTION_CODE
           ,@snap_SECTION_NAME
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
		   ,@snap_DIVISION_NAME
		   ,@CLASSIFIED
		   ,@DEPARTMENT_ID
		   ,@snap_DEPARTMENT_CODE
		   ,@snap_DEPARTMENT_NAME
			 ,@IMPACT_OTHER_FLAG
			 ,@DOCUMENT_CREATOR)

	DECLARE @curr_DOCUMENT_TRANSACTION_ID int
	SET @curr_DOCUMENT_TRANSACTION_ID = SCOPE_IDENTITY();

	IF @RELATED_DIVISIONS IS NOT NULL AND LEN(@RELATED_DIVISIONS) > 0
	BEGIN
		INSERT INTO [dbo].[TB_R_DOCUMENT_RELATED_DIVISION] (DOCUMENT_TRANSACTION_ID, DIVISION_CODE, DIVISION_NAME, DIVISION_ROLE, CREATED_BY, CREATED_DT)
		SELECT @curr_DOCUMENT_TRANSACTION_ID,
			   LEFT(v, pos - 1),
			   DIV.DIVISION_NAME,
			   ISNULL(NULLIF(LTRIM(RTRIM(SUBSTRING(v, pos + 1, 20))), ''), 'RELATED'),
			   @CREATED_BY, GETDATE()
		FROM (
			SELECT LTRIM(RTRIM(value)) AS v
			FROM STRING_SPLIT(@RELATED_DIVISIONS, ',')
			WHERE LEN(LTRIM(RTRIM(value))) > 0
		) trimmed
		CROSS APPLY (SELECT CHARINDEX(':', v + ':') AS pos) p
		LEFT JOIN [dbo].[TB_M_DIVISION] DIV ON DIV.DIVISION_CODE = LEFT(v, pos - 1);
	END

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
           ,[CHANGED_DT]
           ,[DIVISION]
           ,[DIVISION_NAME]
           ,[DEPARTMENT_CODE]
           ,[DEPARTMENT_NAME])
     SELECT
            @curr_DOCUMENT_TRANSACTION_ID
           ,DEPARTMENT_ID
           ,NULL
           ,0
           ,@CREATED_BY
           ,GETDATE()
           ,@CHANGED_BY
           ,GETDATE()
           ,DIVISION
           ,DIVISION_NAME
           ,DEPARTMENT_CODE
           ,DEPARTMENT_NAME
		FROM TB_R_DOCUMENT_DISTRIBUTION
		WHERE DOCUMENT_TRANSACTION_ID = @rev_DOCUMENT_TRANSACTION_ID

		-- Registrasi P4D dokumen lama SENGAJA TIDAK di-soft-delete di sini lagi -
		-- lihat komentar di guard P4D-gated revision di atas. Pindah ke
		-- sp_DocumentMaintenance_SupersedeRevision (request Hendra 2026-08-16).
	END

	-- Obsolete-control fix (Jul 2026): baris lama TIDAK dihapus di sini lagi.
	-- (dulu: DELETE FROM TB_R_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @rev_DOCUMENT_TRANSACTION_ID)
	-- Ia tetap ada & current sampai revisi baru ini disetujui penuh.

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
