-- ============================================================
-- sp_ExternalDocument_Delete (soft delete)
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_ExternalDocument_Delete]
    @EXTERNAL_DOCUMENT_ID INT,
    @LOGIN_USER           VARCHAR(255),
    @RETURN_MSG           VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
    SET NOCOUNT ON;
    DECLARE @PROCESS_ID BIGINT,
            @LOCATION VARCHAR(255) = 'sp_ExternalDocument_Delete';

    EXEC sp_StartLog @PROCESS_ID OUTPUT, 'External Document', 'Delete', @LOCATION, @LOGIN_USER

    UPDATE [dbo].[TB_M_EXTERNAL_DOCUMENT]
    SET DELETE_FLAG = 1,
        CHANGED_DT = GETDATE(),
        CHANGED_BY = @LOGIN_USER
    WHERE EXTERNAL_DOCUMENT_ID = @EXTERNAL_DOCUMENT_ID

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
