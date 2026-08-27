-- =====================================================================
-- P4D Maintenance - urutan alur dibalik: Receive/Reject dulu, baru Distribution
-- Jalankan di database DMS_NEW
--
-- Alur baru:
--   1) Dokumen didaftarkan ke P4D -> STATUS = 0 (DRAFT)
--   2) QMS Receive/Reject langsung dari DRAFT (tanpa perlu Distribution dulu)
--        Receive -> STATUS = 2 (RECEIVED)
--        Reject  -> STATUS = 3 (REJECTED), kembali ke user pembuat
--   3) Setelah RECEIVED, QMS baru Assign Distribution (departemen tujuan)
--   4) Send ke departemen -> STATUS = 5 (SENT) / 6 (PARTIALLY SENT)
--
-- Perubahan: sp_P4DMaintenance_InsertDistribution sebelumnya otomatis
-- memindahkan STATUS DRAFT(0) -> ON PROGRESS(1) begitu distribusi pertama
-- ditambahkan. Itu sesuai alur lama (Distribution dulu, baru Receive/Reject).
-- Karena urutannya sekarang dibalik, dokumen sudah berstatus RECEIVED(2)
-- pada saat Distribution ditambahkan, jadi transisi ke ON PROGRESS(1) ini
-- tidak lagi relevan dan dihapus. Branch STATUS 5 -> 6 (Partially Sent)
-- dipertahankan karena itu untuk kasus lain (distribusi baru ditambahkan
-- setelah dokumen sudah pernah di-Send).
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_P4DMaintenance_InsertDistribution]
		@DOCUMENT_TRANSACTION_ID int,
		@DEPARTMENT_ID varchar(50),
		@DATE varchar(50),
		@CREATED_BY varchar(50),
		@CHANGED_BY varchar(50),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT

AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT
	, @LOCATION VARCHAR(255) = 'sp_P4DMaintenance_InsertDistribution'
	, @rev_DOCUMENT_ID int
	, @rev_REVISION int

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'P4D Maintenance', 'Insert Distribution', @LOCATION, @CREATED_BY

	IF ISNULL(@DEPARTMENT_ID, '') = '' OR ISNULL(@DEPARTMENT_ID, 'null') = 'null' --OR LEN(@DOCUMENT_TYPE) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Department should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

	IF EXISTS(
		SELECT 1
		FROM TB_R_DOCUMENT_DISTRIBUTION
		WHERE DEPARTMENT_ID = @DEPARTMENT_ID AND DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Selected Department is already included in this document distribution';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
		RETURN 0;
	END

	-- Snapshot nama Division/Department (request Hendra 2026-08-22) - dibekukan
	-- saat distribusi dibuat, lihat SQL/OrgMasterData_NameSnapshot_Migration.sql.
	DECLARE @snap_DIVISION VARCHAR(50), @snap_DIVISION_NAME VARCHAR(255),
					@snap_DEPARTMENT_CODE VARCHAR(5), @snap_DEPARTMENT_NAME VARCHAR(255);

	SELECT @snap_DIVISION = DEP.DIVISION, @snap_DEPARTMENT_CODE = DEP.DEPARTMENT_CODE, @snap_DEPARTMENT_NAME = DEP.DEPARTMENT_NAME
	FROM [dbo].[TB_M_DEPARTMENT] DEP WHERE DEP.DEPARTMENT_ID = @DEPARTMENT_ID;

	SELECT @snap_DIVISION_NAME = DIV.DIVISION_NAME
	FROM [dbo].[TB_M_DIVISION] DIV WHERE DIV.DIVISION_CODE = @snap_DIVISION;

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
     SELECT @DOCUMENT_TRANSACTION_ID
           ,@DEPARTMENT_ID
           ,NULL--GETDATE()
           ,'0' [STATUS]
           ,@CREATED_BY
           ,GETDATE()
           ,NULL
           ,NULL
           ,@snap_DIVISION
           ,@snap_DIVISION_NAME
           ,@snap_DEPARTMENT_CODE
           ,@snap_DEPARTMENT_NAME

	DECLARE @CURRENT_STATUS INT;
	SET @CURRENT_STATUS = (SELECT STATUS FROM TB_R_CTRL_DOCUMENT WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID)

	-- STATUS 6 = "Partially Sent": flags that a new distribution was added after
	-- the document was already Sent, so the Send button re-enables (front-end
	-- keys off STATUS == 2 or 6). Registered in TB_M_SYSTEM (DOC_CTRL_STATUS)
	-- so it resolves correctly and no longer vanishes from search results.
	IF @CURRENT_STATUS = 5
	BEGIN
		 	UPDATE TB_R_CTRL_DOCUMENT
			SET [STATUS] = '6'
				, CHANGED_BY = @CHANGED_BY
				, CHANGED_DT = GETDATE()
			WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND DELETE_FLAG != '1'
	END

	SET @RETURN_MSG = 'Successfully Save Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @CREATED_BY
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @CREATED_BY
	RETURN 0;
END CATCH
GO
