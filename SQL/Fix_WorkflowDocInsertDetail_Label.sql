-- =====================================================================
-- Fix: sp_Workflow_DocInsertDetail (dipakai admin UI /WorkflowDoc/Index,
-- "Approval Workflow Setup") tidak pernah menulis kolom LABEL ke
-- TB_M_WORKFLOW_DOC_D, padahal sp_WorkflowDoc_Create membaca LABEL itu
-- dan menyalinnya ke TB_R_APPROVAL_D.LABEL - yang sekarang dipakai kolom
-- "Role" di popup Approval List yang baru dirapikan (DocumentMaintenance
-- & DocumentSubmission). Akibatnya: workflow apapun yang dikonfigurasi
-- lewat admin UI itu tampil Role kosong.
--
-- Fix: default LABEL dari nama posisi (TB_M_POSITION.POSITION_NAME) saat
-- insert, plus backfill baris lama yang LABEL-nya masih NULL. Tidak ada
-- perubahan C#/parameter SP - defaultnya murni di dalam SP.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Workflow_DocInsertDetail]
	@WORKFLOW_DOC_ID		INT,
	@WORKFLOW_SEQ 			INT,
	@POSITION_ID 			  INT,
	@LOGIN_USER 				VARCHAR(255),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT
AS
BEGIN TRY

	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_Workflow_DocInsertDetail';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Workflow Document Detail', 'Insert', @LOCATION, @LOGIN_USER

	IF @WORKFLOW_DOC_ID IS NULL OR LEN(@WORKFLOW_DOC_ID) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Workflow Doc ID should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @WORKFLOW_SEQ IS NULL OR LEN(@WORKFLOW_SEQ) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Sequence should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @POSITION_ID IS NULL OR LEN(@POSITION_ID) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position Id should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	INSERT INTO [dbo].[TB_M_WORKFLOW_DOC_D] (
		WORKFLOW_DOC_ID,
		WORKFLOW_SEQ,
		POSITION_ID,
		LABEL,
		CREATED_DT,
		CREATED_BY,
		CHANGED_DT,
		CHANGED_BY
	) VALUES (
		@WORKFLOW_DOC_ID,
		@WORKFLOW_SEQ,
		@POSITION_ID,
		(SELECT POSITION_NAME FROM TB_M_POSITION WHERE POSITION_ID = @POSITION_ID),
		GETDATE(),
		@LOGIN_USER,
		GETDATE(),
		@LOGIN_USER
	)

	SET @RETURN_MSG = 'Successfully Save Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;

END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO

-- Backfill baris lama yang LABEL-nya masih kosong
UPDATE D
SET D.LABEL = P.POSITION_NAME
FROM [dbo].[TB_M_WORKFLOW_DOC_D] D
JOIN [dbo].[TB_M_POSITION] P ON P.POSITION_ID = D.POSITION_ID
WHERE D.LABEL IS NULL;
GO
