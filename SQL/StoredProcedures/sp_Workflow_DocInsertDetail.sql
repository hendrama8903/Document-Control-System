SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- LABEL diisi nama jabatan (lihat SQL/Fix_WorkflowDocInsertDetail_Label.sql -
-- file ini dulu belum ikut ter-update).
-- 2026-11-06: @IS_MANDATORY (opsional, default 0) - langkah wajib, lihat
-- sp_WorkflowDoc_Create. @LABEL (opsional): label langkah ("Dibuat Oleh", dst.)
-- dipertahankan saat alur disimpan ulang lewat menu; kosong = nama jabatan.
CREATE OR ALTER PROCEDURE [dbo].[sp_Workflow_DocInsertDetail]
	@WORKFLOW_DOC_ID		INT,
	@WORKFLOW_SEQ 			INT,
	@POSITION_ID 			  INT,
	@LOGIN_USER 				VARCHAR(255),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT,
	@IS_MANDATORY				BIT = 0,
	@LABEL							VARCHAR(255) = NULL
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
		IS_MANDATORY,
		CREATED_DT,
		CREATED_BY,
		CHANGED_DT,
		CHANGED_BY
	) VALUES (
		@WORKFLOW_DOC_ID,
		@WORKFLOW_SEQ,
		@POSITION_ID,
		ISNULL(NULLIF(LTRIM(RTRIM(@LABEL)), ''), (SELECT POSITION_NAME FROM TB_M_POSITION WHERE POSITION_ID = @POSITION_ID)),
		ISNULL(@IS_MANDATORY, 0),
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
