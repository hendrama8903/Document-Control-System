SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- 2026-11-06: @DOCUMENT_ID (opsional) = alur khusus Document Category; NULL =
-- alur default level. Kombinasi (level, level pembuat, kategori) tidak boleh
-- dobel - dulu cek duplikat di-comment, sehingga dua alur untuk kombinasi yang
-- sama bisa tersimpan dan sp_WorkflowDoc_Create memilih salah satunya acak.
-- 2026-11-07: @IS_ACTIVE; larangan dobel hanya berlaku antar alur aktif. Mengedit
-- alur nonaktif menyimpan ulang dengan @IS_ACTIVE = 0.
CREATE OR ALTER PROCEDURE [dbo].[sp_Workflow_DocInsertHeader]
  @DOCUMENT_LEVEL 	INT,
	@CREATOR_LEVEL 		INT,
	@LOGIN_USER 			VARCHAR(255),
	@RETURN_MSG 			VARCHAR(MAX) OUTPUT,
	@RETURN_ID				INT OUTPUT,
	@DOCUMENT_ID			INT = NULL,
	@IS_ACTIVE				BIT = 1
AS
BEGIN TRY

	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_Workflow_DocInsertHeader';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Workflow Document Header', 'Insert', @LOCATION, @LOGIN_USER

	IF @DOCUMENT_LEVEL IS NULL OR LEN(@DOCUMENT_LEVEL) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document Level should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @CREATOR_LEVEL IS NULL OR LEN(@CREATOR_LEVEL) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Creator Level should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		SET @RETURN_ID = 0;
		RETURN 0;
	END

	IF @DOCUMENT_ID IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_DOCUMENT] WHERE DOCUMENT_ID = @DOCUMENT_ID AND [LEVEL] = @DOCUMENT_LEVEL)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Document Category does not belong to Document Level ' + CAST(@DOCUMENT_LEVEL AS VARCHAR) + '.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		SET @RETURN_ID = 0;
		RETURN 0;
	END

	IF @IS_ACTIVE = 1 AND EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_WORKFLOW_DOC_H]
		WHERE IS_ACTIVE = 1 AND DOCUMENT_LEVEL = @DOCUMENT_LEVEL AND CREATOR_LEVEL = @CREATOR_LEVEL
		AND ISNULL(DOCUMENT_ID, 0) = ISNULL(@DOCUMENT_ID, 0))
	BEGIN
		SET @RETURN_MSG = 'ERROR: An active workflow already exists for this Document Level and Creator Level'
			+ CASE WHEN @DOCUMENT_ID IS NULL THEN '' ELSE ' for this Document Category' END + '. Edit it, or deactivate it first.';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		SET @RETURN_ID = 0;
		RETURN 0;
	END

	INSERT INTO [dbo].[TB_M_WORKFLOW_DOC_H] (
		DOCUMENT_LEVEL,
		CREATOR_LEVEL,
		DOCUMENT_ID,
		IS_ACTIVE,
		CREATED_DT,
		CREATED_BY,
		CHANGED_DT,
		CHANGED_BY
	) VALUES (
		@DOCUMENT_LEVEL,
		@CREATOR_LEVEL,
		@DOCUMENT_ID,
		@IS_ACTIVE,
		GETDATE(),
		@LOGIN_USER,
		GETDATE(),
		@LOGIN_USER
	)

	IF @@ROWCOUNT > 0
	BEGIN
		DECLARE @workflowId INT;
		SET @workflowId = SCOPE_IDENTITY();

		SET @RETURN_ID = @workflowId;
	END

	SET @RETURN_MSG = 'Successfully Save Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;

END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	SET @RETURN_ID = 0;
	RETURN 0;
END CATCH
GO
