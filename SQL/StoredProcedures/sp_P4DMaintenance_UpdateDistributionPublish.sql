-- Phase 1 (document lifecycle labels, Jul 2026): after logging this department's publish,
-- check whether EVERY non-deleted TB_R_CTRL_DOCUMENT row for the same document now has a
-- TB_R_PUBLISH_HISTORY entry. If so, the document is fully distributed - flip
-- TB_R_DOCUMENT.STATUS from '1' (Approved) to '5' (Published/Effective). Only Approved
-- documents are promoted; a document that somehow isn't STATUS='1' is left alone.
--
-- Fix (Aug 2026): "fully distributed" now means every DEPARTMENT actually targeted by
-- TB_R_DOCUMENT_DISTRIBUTION (STATUS=1/sent) has a publish-history entry - NOT every
-- TB_R_CTRL_DOCUMENT row. CTRL_DOCUMENT rows can multiply beyond real distribution
-- targets (e.g. a user self-requesting via UserDashboard creates an OPERATION_TYPE=2
-- row), which used to let a single unrelated acknowledgment flip an under-distributed
-- document straight to Published, or block a fully-acknowledged one on a stray request
-- row. Also requires at least one distribution row to exist (a document that was never
-- actually sent anywhere must not auto-publish just because someone requested it).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_P4DMaintenance_UpdateDistributionPublish]
	@DOCUMENT_CTRL_ID					int,
	@DEPARTMENT_ID 						int,
	@LOGIN_USER 							VARCHAR(255),
	@RETURN_MSG 							VARCHAR(MAX) OUTPUT

AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT
	, @LOCATION VARCHAR(255) = 'sp_P4DMaintenance_UpdateDistributionPublish'
	, @rev_DOCUMENT_ID int
	, @rev_REVISION int
	, @DOC_TRANSACTION_ID int

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'P4D Maintenance', 'Update Distribution Publish', @LOCATION, @LOGIN_USER

	INSERT INTO [dbo].[TB_R_PUBLISH_HISTORY] (
		DOCUMENT_CTRL_ID,
		DEPARTMENT_ID,
		CREATED_DT,
		CREATED_BY,
		CHANGED_DT,
		CHANGED_BY
	) VALUES (
		@DOCUMENT_CTRL_ID,
		@DEPARTMENT_ID,
		GETDATE(),
		@LOGIN_USER,
		GETDATE(),
		@LOGIN_USER
	)

	SELECT @DOC_TRANSACTION_ID = DOCUMENT_TRANSACTION_ID
	FROM [dbo].[TB_R_CTRL_DOCUMENT]
	WHERE DOCUMENT_CTRL_ID = @DOCUMENT_CTRL_ID;

	IF @DOC_TRANSACTION_ID IS NOT NULL
	BEGIN
		IF EXISTS (
			SELECT 1 FROM [dbo].[TB_R_DOCUMENT_DISTRIBUTION]
			WHERE DOCUMENT_TRANSACTION_ID = @DOC_TRANSACTION_ID AND STATUS = 1
		)
		AND NOT EXISTS (
			SELECT 1
			FROM [dbo].[TB_R_DOCUMENT_DISTRIBUTION] DD
			WHERE DD.DOCUMENT_TRANSACTION_ID = @DOC_TRANSACTION_ID
			AND DD.STATUS = 1
			AND NOT EXISTS (
				SELECT 1
				FROM [dbo].[TB_R_PUBLISH_HISTORY] PH
				JOIN [dbo].[TB_R_CTRL_DOCUMENT] CT ON CT.DOCUMENT_CTRL_ID = PH.DOCUMENT_CTRL_ID
				WHERE CT.DOCUMENT_TRANSACTION_ID = @DOC_TRANSACTION_ID
				AND PH.DEPARTMENT_ID = DD.DEPARTMENT_ID
			)
		)
		BEGIN
			UPDATE [dbo].[TB_R_DOCUMENT]
			SET STATUS = '5', CHANGED_BY = @LOGIN_USER, CHANGED_DT = GETDATE()
			WHERE DOCUMENT_TRANSACTION_ID = @DOC_TRANSACTION_ID AND STATUS = '1';
		END
	END

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
