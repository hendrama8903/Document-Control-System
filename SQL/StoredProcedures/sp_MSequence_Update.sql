-- Allow editing SEQ_TYPE/SEQ_CODE (the key) in addition to SEQ_NO.
-- Identifies the row via the ORIGINAL key (@OLD_SEQ_TYPE/@OLD_SEQ_CODE), then renames it to
-- the new key + sets the new SEQ_NO. Guards:
--  - SEQ_NO can't drop below the value the OLD row currently has (same collision-prevention rule)
--  - can't rename onto a key that already belongs to a DIFFERENT existing row
CREATE OR ALTER PROCEDURE [dbo].[sp_MSequence_Update]
    @OLD_SEQ_TYPE VARCHAR(50),
    @OLD_SEQ_CODE VARCHAR(50),
    @SEQ_TYPE     VARCHAR(50),
    @SEQ_CODE     VARCHAR(50),
    @SEQ_NO       INT,
    @LOGIN_USER   VARCHAR(255),
    @RETURN_MSG   VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
    DECLARE @PROCESS_ID BIGINT,
            @LOCATION VARCHAR(255) = 'sp_MSequence_Update',
            @CURRENT_SEQ_NO INT

    EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Document Numbering', 'Update', @LOCATION, @LOGIN_USER

    SELECT @CURRENT_SEQ_NO = SEQ_NO FROM [dbo].[TB_M_SEQUENCE]
    WHERE SEQ_TYPE = @OLD_SEQ_TYPE AND SEQ_CODE = @OLD_SEQ_CODE;

    IF @CURRENT_SEQ_NO IS NULL
    BEGIN
        SET @RETURN_MSG = 'ERROR: Sequence ' + @OLD_SEQ_CODE + ' not found';
        EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
        RETURN 0;
    END

    IF @SEQ_NO < @CURRENT_SEQ_NO
    BEGIN
        SET @RETURN_MSG = 'ERROR: New number (' + CAST(@SEQ_NO AS VARCHAR) + ') cannot be lower than the current value (' + CAST(@CURRENT_SEQ_NO AS VARCHAR) + ') - this would risk generating a document code that already exists.';
        EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
        RETURN 0;
    END

    IF (@SEQ_TYPE <> @OLD_SEQ_TYPE OR @SEQ_CODE <> @OLD_SEQ_CODE)
       AND EXISTS (SELECT 1 FROM [dbo].[TB_M_SEQUENCE] WHERE SEQ_TYPE = @SEQ_TYPE AND SEQ_CODE = @SEQ_CODE)
    BEGIN
        SET @RETURN_MSG = 'ERROR: Sequence Type/Code ' + @SEQ_TYPE + '/' + @SEQ_CODE + ' is already used by another row.';
        EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
        RETURN 0;
    END

    UPDATE [dbo].[TB_M_SEQUENCE]
    SET SEQ_TYPE = @SEQ_TYPE,
        SEQ_CODE = @SEQ_CODE,
        SEQ_NO = @SEQ_NO,
        CHANGED_BY = @LOGIN_USER,
        CHANGED_DT = GETDATE()
    WHERE SEQ_TYPE = @OLD_SEQ_TYPE AND SEQ_CODE = @OLD_SEQ_CODE;

    SET @RETURN_MSG = 'Successfully Save Data'
    EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
    RETURN 1;
END TRY
BEGIN CATCH
    SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() + ': ' + ERROR_MESSAGE() + ', at line = ' + CAST(ERROR_LINE() AS VARCHAR);
    EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
    RETURN 0;
END CATCH
GO
