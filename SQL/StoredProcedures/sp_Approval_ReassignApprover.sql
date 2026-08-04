-- 3) Reassign a single pending approval-detail row to a new approver
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Approval_ReassignApprover]
    @APPROVAL_ID   INT,
    @WORKFLOW_SEQ  INT,
    @OLD_APPROVER  VARCHAR(255),
    @NEW_APPROVER  VARCHAR(255),
    @REASON        VARCHAR(500),
    @LOGIN_USER    VARCHAR(255),
    @RETURN_MSG    VARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PROCESS_ID BIGINT,
            @LOCATION VARCHAR(255) = 'sp_Approval_ReassignApprover';

    BEGIN TRY
        EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Approval', 'Reassign Approver', @LOCATION, @LOGIN_USER

        IF @NEW_APPROVER IS NULL OR LEN(@NEW_APPROVER) < 1
        BEGIN
            SET @RETURN_MSG = 'ERROR: New approver should not be null';
            EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
            RETURN 0;
        END

        IF @NEW_APPROVER = @OLD_APPROVER
        BEGIN
            SET @RETURN_MSG = 'ERROR: New approver must be different from the current approver';
            EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
            RETURN 0;
        END

        IF NOT EXISTS (
            SELECT TOP 1 1 FROM [dbo].[TB_M_USER]
            WHERE USERNAME = @NEW_APPROVER AND ISNULL(DELETE_FLAG,'0') <> '1'
        )
        BEGIN
            SET @RETURN_MSG = 'ERROR: New approver is not a valid active user';
            EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
            RETURN 0;
        END

        IF NOT EXISTS (
            SELECT TOP 1 1 FROM [dbo].[TB_R_APPROVAL_D]
            WHERE APPROVAL_ID = @APPROVAL_ID
              AND WORKFLOW_SEQ = @WORKFLOW_SEQ
              AND STATUS = '0'
              AND APPROVER = @OLD_APPROVER
        )
        BEGIN
            SET @RETURN_MSG = 'ERROR: This approval step is no longer pending, or the approver has changed. Please refresh and try again.';
            EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
            RETURN 0;
        END

        INSERT INTO [dbo].[TB_R_APPROVAL_REASSIGN_LOG] (
            APPROVAL_ID, WORKFLOW_SEQ, OLD_APPROVER, NEW_APPROVER, REASON, CREATED_BY, CREATED_DT
        ) VALUES (
            @APPROVAL_ID, @WORKFLOW_SEQ, @OLD_APPROVER, @NEW_APPROVER, @REASON, @LOGIN_USER, GETDATE()
        );

        UPDATE [dbo].[TB_R_APPROVAL_D]
        SET APPROVER   = @NEW_APPROVER,
            CHANGED_BY = @LOGIN_USER,
            CHANGED_DT = GETDATE()
        WHERE APPROVAL_ID = @APPROVAL_ID
          AND WORKFLOW_SEQ = @WORKFLOW_SEQ;

        SET @RETURN_MSG = 'Successfully reassigned approver';
        EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
        RETURN 1;
    END TRY
    BEGIN CATCH
        SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() + ': ' + ERROR_MESSAGE() + ', at line = ' + CAST(ERROR_LINE() AS VARCHAR);
        EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
        RETURN 0;
    END CATCH
END
GO
