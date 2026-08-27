SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_P4DMaintenance_DeleteDistribution]
		@DISTRIBUTION_ID int,
		@LOGIN_USER varchar(50),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT

AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT
	, @LOCATION VARCHAR(255) = 'sp_P4DMaintenance_DeleteDistribution'
	, @rev_DOCUMENT_ID int
	, @rev_REVISION int
					
	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'P4D Maintenance', 'Delete Distribution', @LOCATION, @LOGIN_USER

	IF NOT EXISTS(
		SELECT 1
		FROM TB_R_DOCUMENT_DISTRIBUTION
		WHERE DISTRIBUTION_ID = @DISTRIBUTION_ID
			--AND [STATUS] NOT IN ('0', '1', '3')
	)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data is not found';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	-- Validasi ini SEMPAT dinonaktifkan (comment-out) - diaktifkan lagi
	-- (request Hendra 2026-08-15) karena tanpanya distribusi yang sudah
	-- Sent (STATUS=1), termasuk yang department-nya sudah Accept
	-- (TB_R_PUBLISH_HISTORY), bisa dihapus begitu saja: dokumen langsung
	-- hilang dari UserDashboard department itu tanpa jejak, riwayat Accept
	-- jadi yatim, dan kalau itu satu-satunya distribusi maka Un-Receive
	-- (yang cuma cek "ada baris distribusi atau tidak") jadi bisa dipaksa
	-- lagi walau dokumen sudah pernah beredar.
	IF(
		SELECT STATUS
		FROM TB_R_DOCUMENT_DISTRIBUTION
		WHERE DISTRIBUTION_ID = @DISTRIBUTION_ID
	) <> '0'
	BEGIN
		SET @RETURN_MSG = 'ERROR: This distribution has already been Sent and cannot be deleted.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	delete from TB_R_DOCUMENT_DISTRIBUTION where DISTRIBUTION_ID = @DISTRIBUTION_ID

	SET @RETURN_MSG = 'Successfully Delete Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
