-- Admin-only forced reset of SEQ_NO. Unlike sp_MSequence_Update, this deliberately
-- SKIPS the "can't go lower" guard so an admin can restart a counter. Every call is
-- logged as a WRN entry (old -> new, by whom) since lowering the number risks the
-- app generating a document code that already exists on a future save.
CREATE OR ALTER PROCEDURE [dbo].[sp_MSequence_Reset]
    @SEQ_TYPE     VARCHAR(50),
    @SEQ_CODE     VARCHAR(50),
    @SEQ_NO       INT,
    @LOGIN_USER   VARCHAR(255),
    @RETURN_MSG   VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
    DECLARE @PROCESS_ID BIGINT,
            @LOCATION VARCHAR(255) = 'sp_MSequence_Reset',
            @CURRENT_SEQ_NO INT

    EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Numbering', 'Reset', @LOCATION, @LOGIN_USER

    SELECT @CURRENT_SEQ_NO = SEQ_NO FROM [dbo].[TB_M_SEQUENCE]
    WHERE SEQ_TYPE = @SEQ_TYPE AND SEQ_CODE = @SEQ_CODE;

    IF @CURRENT_SEQ_NO IS NULL
    BEGIN
        SET @RETURN_MSG = 'ERROR: Sequence ' + @SEQ_CODE + ' not found';
        EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
        RETURN 0;
    END

    UPDATE [dbo].[TB_M_SEQUENCE]
    SET SEQ_NO = @SEQ_NO,
        CHANGED_BY = @LOGIN_USER,
        CHANGED_DT = GETDATE()
    WHERE SEQ_TYPE = @SEQ_TYPE AND SEQ_CODE = @SEQ_CODE;

    SET @RETURN_MSG = 'Successfully Save Data';
    DECLARE @WARN_MSG VARCHAR(MAX) = 'Sequence ' + @SEQ_CODE + ' force-reset from ' + CAST(@CURRENT_SEQ_NO AS VARCHAR) + ' to ' + CAST(@SEQ_NO AS VARCHAR) + ' by ' + @LOGIN_USER + ' - possible document code collision risk.';
    EXEC sp_WriteLog @PROCESS_ID, '2', 'WRN', @WARN_MSG, @LOCATION, @LOGIN_USER
    RETURN 1;
END TRY
BEGIN CATCH
    SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() + ': ' + ERROR_MESSAGE() + ', at line = ' + CAST(ERROR_LINE() AS VARCHAR);
    EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
    RETURN 0;
END CATCH
GO
