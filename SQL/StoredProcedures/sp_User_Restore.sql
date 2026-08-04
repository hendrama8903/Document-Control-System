SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_User_Restore]
    @USERNAME   varchar(100),
    @LOGIN_USER varchar(100),
    @RETURN_MSG varchar(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF NOT EXISTS (
            SELECT TOP 1 1
            FROM dbo.TB_M_USER
            WHERE USERNAME = @USERNAME
              AND ISNULL(DELETE_FLAG, '0') = '1'
        )
        BEGIN
            SET @RETURN_MSG = 'User not found or is not deleted.';
            RETURN 0;
        END

        UPDATE dbo.TB_M_USER
        SET DELETE_FLAG = '0',
            CHANGED_BY  = @LOGIN_USER,
            CHANGED_DT  = GETDATE()
        WHERE USERNAME = @USERNAME;

        SET @RETURN_MSG = 'User restored successfully.';
        RETURN 1;
    END TRY
    BEGIN CATCH
        SET @RETURN_MSG = ERROR_MESSAGE();
        RETURN 0;
    END CATCH
END
GO
