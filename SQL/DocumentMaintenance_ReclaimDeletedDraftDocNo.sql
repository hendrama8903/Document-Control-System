-- =====================================================================
-- Keluhan QMS: nomor dokumen (DOCUMENT_CODE) dijamin naik terus lewat
-- TB_M_SEQUENCE (sp_GetNextSeqNo, UPDATE...SET SEQ_NO+1), tidak pernah
-- ditarik balik walau draft-nya dihapus (sp_DocumentMaintenance_Delete
-- cuma soft-delete). Akibatnya kalau sebuah department create+delete
-- draft berkali-kali sebelum benar-benar submit (mis. 50x), dokumen
-- PERTAMA mereka yang beneran sampai ke P4D/QMS bisa nomor "050" -
-- padahal buat QMS itu memang dokumen pertama department itu.
--
-- Fix: sp_DocumentMaintenance_Delete sekarang menarik balik nomor ke
-- TB_M_SEQUENCE - TAPI HANYA kalau kondisinya aman:
--   1) DOCUMENT_TYPE = '01' (dokumen baru - revisi/'02' pakai ulang
--      DOCUMENT_CODE dokumen sebelumnya, tidak pernah menerbitkan
--      nomor baru dari TB_M_SEQUENCE, jadi tidak relevan).
--   2) Nomor yang dihapus adalah nomor TERAKHIR yang diterbitkan untuk
--      kombinasi Divisi/Dept/Section/Kategori itu (TB_M_SEQUENCE.SEQ_NO
--      masih persis sama dengan nomor dokumen yang dihapus) - kalau
--      sudah ada dokumen lain yang lebih baru dengan prefix sama,
--      nomor TIDAK ditarik balik (mencegah nomor lama yang sudah
--      "dilangkahi" dipakai ulang).
--
-- Aman dari duplikasi nomor / race condition: dokumen yang boleh
-- dihapus di sini SELALU STATUS=0 (guard sudah ada sebelumnya), yang
-- berarti belum pernah lolos approval internal dan TIDAK PERNAH bisa
-- sudah teregister di P4D/QMS (Document Registration cuma menampilkan
-- dokumen STATUS=1 - lihat GetDocumentCodeLoginBased di
-- DocumentMaintenanceController.cs) - jadi penarikan nomor ini murni
-- terjadi sebelum QMS pernah melihat nomor itu. Kondisi #2 di atas
-- pakai UPDATE...WHERE SEQ_NO=@seqValue (conditional pada nilai counter
-- saat itu juga), jadi kalaupun ada 2 proses bersamaan, salah satu
-- otomatis tidak match dan tidak melakukan apa-apa - tidak butuh lock/
-- transaction tambahan.
--
-- Idempotent (CREATE OR ALTER). Jalankan di database DMS_NEW.
-- =====================================================================

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
