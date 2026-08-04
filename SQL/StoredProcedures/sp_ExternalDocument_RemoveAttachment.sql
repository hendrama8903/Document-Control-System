-- ============================================================
-- sp_ExternalDocument_RemoveAttachment
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_ExternalDocument_RemoveAttachment]
    @EXTERNAL_DOCUMENT_ID INT,
    @LOGIN_USER           VARCHAR(255),
    @RETURN_MSG           VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
    UPDATE [dbo].[TB_M_EXTERNAL_DOCUMENT]
    SET FILE_PATH = NULL,
        CHANGED_DT = GETDATE(),
        CHANGED_BY = @LOGIN_USER
    WHERE EXTERNAL_DOCUMENT_ID = @EXTERNAL_DOCUMENT_ID

    SET @RETURN_MSG = 'Successfully Remove Attachment Data'
    RETURN 1;
END TRY
BEGIN CATCH
    SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
    RETURN 0;
END CATCH
GO
