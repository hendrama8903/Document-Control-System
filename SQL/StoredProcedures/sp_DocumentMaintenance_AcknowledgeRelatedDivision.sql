SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Aksi "Mengetahui" untuk Div Head Related Division (SPR/SIPOCOR Level 2) -
-- terpisah TOTAL dari sp_Approval_Approve/Reject (request Hendra 2026-08-20).
-- Cuma konfirmasi (tidak ada opsi reject - lihat percakapan yang melatari
-- perubahan ini) dan tidak menyentuh TB_R_APPROVAL_D/H sama sekali.
--
-- @PROMOTED_TO_APPROVED = 1 kalau acknowledgment ini adalah yang TERAKHIR
-- (semua Related Division sudah Mengetahui) DAN approval dokumennya sendiri
-- sudah selesai lebih dulu (TB_R_DOCUMENT.STATUS = 6, "Waiting
-- Acknowledgment") - controller yang memanggil lalu bertanggung jawab
-- menaikkan STATUS ke 1 (lewat sp_DocumentMaintenance_UpdateStatus, supaya
-- logika stempel NEXT_REVIEW_DATE tetap konsisten satu tempat) dan
-- menjalankan efek samping "selesai approval" (obsolete-control, hapus cache
-- PDF, email) - SP ini sengaja tidak melakukan itu sendiri.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_AcknowledgeRelatedDivision]
	@DOCUMENT_TRANSACTION_ID	INT,
	@DIVISION_CODE				VARCHAR(10),
	@LOGIN_USER					VARCHAR(255),
	@PROMOTED_TO_APPROVED		INT OUTPUT,
	@RETURN_MSG					VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	SET @PROMOTED_TO_APPROVED = 0;

	IF NOT EXISTS (
		SELECT 1 FROM [dbo].[TB_M_USER_POS]
		WHERE POSITION_ID = 4 AND DIVISION = @DIVISION_CODE AND USERNAME = @LOGIN_USER
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: You are not authorized to acknowledge on behalf of division ' + @DIVISION_CODE + '.';
		RETURN 0;
	END

	-- Cuma peran RELATED yang wajib Acknowledge (request Hendra 2026-08-20,
	-- lihat DocumentMaintenance_RelatedDivision_Role_Migration.sql) - MAIN_PIC
	-- & NOTE_RELATED tidak pernah masuk sini, tombol Acknowledge tidak
	-- ditampilkan untuk mereka di UI, dan guard ini mencegahnya juga di sisi
	-- server kalau ada yang mencoba manggil langsung.
	IF NOT EXISTS (
		SELECT 1 FROM [dbo].[TB_R_DOCUMENT_RELATED_DIVISION]
		WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND DIVISION_CODE = @DIVISION_CODE
			AND DIVISION_ROLE = 'RELATED'
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Related division record not found.';
		RETURN 0;
	END

	IF EXISTS (
		SELECT 1 FROM [dbo].[TB_R_DOCUMENT_RELATED_DIVISION]
		WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND DIVISION_CODE = @DIVISION_CODE AND ACKNOWLEDGED_FLAG = 1
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: This division has already acknowledged this document.';
		RETURN 0;
	END

	UPDATE [dbo].[TB_R_DOCUMENT_RELATED_DIVISION]
	SET ACKNOWLEDGED_FLAG = 1,
		ACKNOWLEDGED_BY = @LOGIN_USER,
		ACKNOWLEDGED_DT = GETDATE()
	WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND DIVISION_CODE = @DIVISION_CODE

	-- MAIN_PIC/NOTE_RELATED tidak pernah di-Acknowledge (ACKNOWLEDGED_FLAG-nya
	-- akan selalu 0) - kalau tidak difilter ke RELATED saja, dokumen tidak akan
	-- pernah naik ke Approved karena baris itu dianggap "masih pending" selamanya.
	IF NOT EXISTS (
		SELECT 1 FROM [dbo].[TB_R_DOCUMENT_RELATED_DIVISION]
		WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND ACKNOWLEDGED_FLAG = 0
			AND DIVISION_ROLE = 'RELATED'
	)
	BEGIN
		IF EXISTS (
			SELECT 1 FROM [dbo].[TB_R_DOCUMENT]
			WHERE DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID AND STATUS = '6'
		)
		BEGIN
			SET @PROMOTED_TO_APPROVED = 1;
		END
	END

	SET @RETURN_MSG = 'Successfully acknowledged.'
	RETURN 1;

END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
