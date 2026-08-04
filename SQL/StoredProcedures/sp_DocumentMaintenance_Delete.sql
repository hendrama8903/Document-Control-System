SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_Delete]
	@DOCUMENT_TRANSACTION_ID	  INT,
	@LOGIN_USER 		VARCHAR(255),
	@RETURN_MSG 		VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_DocumentMaintenance_Delete';
					
	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Maintenance', 'Delete', @LOCATION, @LOGIN_USER
	
	IF EXISTS(SELECT 1
				FROM TB_R_DOCUMENT
				WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
					AND DELETE_FLAG = 1)
	BEGIN
		SET @RETURN_MSG = 'Concurrent process has occurred for deletion process';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF EXISTS(SELECT 1
				FROM TB_R_DOCUMENT
				WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
					AND DELETE_FLAG = 0
					AND STATUS <> 0)
	BEGIN
		SET @RETURN_MSG = 'Delete data is not allowed for processed status';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END
	
	BEGIN
	-- Reclaim nomor dokumen (Jul 2026): dokumen yang boleh dihapus di sini SELALU
	-- STATUS=0 (guard di atas), yang berarti belum pernah lolos approval internal
	-- dan TIDAK PERNAH bisa sudah teregister di P4D/QMS (Document Registration cuma
	-- menampilkan dokumen STATUS=1 - lihat GetDocumentCodeLoginBased di
	-- DocumentMaintenanceController.cs). Jadi aman menarik balik nomornya - TAPI HANYA
	-- kalau ini nomor TERAKHIR yang diterbitkan untuk kombinasi Divisi/Dept/Section/
	-- Kategori ini (TB_M_SEQUENCE.SEQ_NO masih persis sama dengan nomor dokumen ini),
	-- supaya tidak pernah menarik balik nomor yang sudah "dilangkahi" oleh dokumen
	-- lain yang lebih baru dengan prefix sama. Cuma berlaku untuk dokumen BARU
	-- (DOCUMENT_TYPE='01') - revisi (02) pakai ulang DOCUMENT_CODE dokumen
	-- sebelumnya, tidak pernah menerbitkan nomor baru.
	DECLARE @docType VARCHAR(50), @docCode VARCHAR(50);
	SELECT @docType = DOCUMENT_TYPE, @docCode = DOCUMENT_CODE
	FROM TB_R_DOCUMENT
	WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID;

	IF @docType = '01' AND @docCode IS NOT NULL AND LEN(@docCode) > 4
	BEGIN
		DECLARE @seqPrefix VARCHAR(50) = LEFT(@docCode, LEN(@docCode) - 4);
		DECLARE @seqValue INT = TRY_CAST(RIGHT(@docCode, 3) AS INT);

		IF @seqValue IS NOT NULL
		BEGIN
			UPDATE TB_M_SEQUENCE
			SET SEQ_NO = SEQ_NO - 1,
					CHANGED_BY = @LOGIN_USER,
					CHANGED_DT = GETDATE()
			WHERE SEQ_TYPE = 'DOC_NO'
				AND SEQ_CODE = @seqPrefix
				AND SEQ_NO = @seqValue;
		END
	END

	UPDATE [dbo].[TB_R_DOCUMENT]
	SET DELETE_FLAG = 1,
			CHANGED_DT = GETDATE(),
			CHANGED_BY = @LOGIN_USER
	WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID

	DELETE FROM TB_R_DOCUMENT_LOG WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
	DELETE FROM TB_R_DOCUMENT_DISTRIBUTION WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID

	SET @RETURN_MSG = 'Successfully Delete Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	END
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
