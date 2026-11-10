SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Delete Document Category yang BELUM PERNAH dipakai (request Hendra 2026-11-09).
-- Tetap soft delete: baris disimpan dengan DELETE_FLAG = 2 (Deleted) untuk jejak audit,
-- tetapi tidak tampil di grid (sp_DocumentMaster_SearchWithStatus) maupun di pilihan
-- kategori (sp_DocumentMaster_Search hanya DELETE_FLAG = 0), dan tidak bisa di-Activate.
-- DELETE_FLAG = 1 tetap berarti Inactive (Deactivate), bisa diaktifkan lagi.
--
-- Kategori yang sudah dipakai dokumen (termasuk riwayat revisi), alur approval khusus
-- kategori, atau konfigurasi template baris/kolom ditolak - cukup di-Deactivate.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaster_Remove]
	@DOCUMENT_ID	INT,
	@LOGIN_USER		VARCHAR(255),
	@RETURN_MSG		VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	DECLARE @PROCESS_ID BIGINT,
			@LOCATION VARCHAR(255) = 'sp_DocumentMaster_Remove',
			@CODE VARCHAR(5),
			@FLAG INT,
			@USED VARCHAR(500) = '';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Master', 'Remove', @LOCATION, @LOGIN_USER;

	SELECT @CODE = DOCUMENT_CODE, @FLAG = ISNULL(DELETE_FLAG, 0)
	FROM dbo.TB_M_DOCUMENT WHERE DOCUMENT_ID = @DOCUMENT_ID;

	IF @CODE IS NULL OR @FLAG = 2
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data not found. It may have been removed - refresh the page.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER;
		RETURN 0;
	END

	IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT WHERE DOCUMENT_ID = @DOCUMENT_ID)
		SET @USED = @USED + ', documents';
	IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DOCUMENT_ID = @DOCUMENT_ID)
		SET @USED = @USED + ', document history';
	IF EXISTS (SELECT 1 FROM dbo.TB_M_WORKFLOW_DOC_H WHERE DOCUMENT_ID = @DOCUMENT_ID)
		SET @USED = @USED + ', approval workflow';
	IF EXISTS (SELECT 1 FROM dbo.TB_M_EXCEL_TEMPLATE WHERE DOCUMENT_ID = @DOCUMENT_ID)
		SET @USED = @USED + ', template configuration';

	IF @USED <> ''
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document Category ' + @CODE + ' is already used by ' + STUFF(@USED, 1, 2, '')
			+ ', so it cannot be deleted. Deactivate it instead.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER;
		RETURN 0;
	END

	UPDATE dbo.TB_M_DOCUMENT
	SET DELETE_FLAG = 2,
		CHANGED_DT = GETDATE(),
		CHANGED_BY = @LOGIN_USER
	WHERE DOCUMENT_ID = @DOCUMENT_ID;

	SET @RETURN_MSG = 'Successfully deleted';
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER;
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() + ': ' + ERROR_MESSAGE() + ', at line = ' + CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER;
	RETURN 0;
END CATCH
GO
