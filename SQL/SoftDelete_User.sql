-- =====================================================================
-- Soft Delete User - jalankan di database DMS
-- 1) Tambah kolom DELETE_FLAG di TB_M_USER
-- 2) Buat SP sp_User_Delete (soft delete)
-- 3) MANUAL: tambahkan filter DELETE_FLAG di sp_User_Search dan SP login
-- =====================================================================

-- 1) Kolom flag (default '0' = aktif)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.TB_M_USER') AND name = 'DELETE_FLAG'
)
BEGIN
    ALTER TABLE dbo.TB_M_USER
    ADD DELETE_FLAG char(1) NOT NULL CONSTRAINT DF_TB_M_USER_DELETE_FLAG DEFAULT '0';
END
GO

-- 2) SP soft delete
CREATE OR ALTER PROCEDURE dbo.sp_User_Delete
    @USERNAME   varchar(100),
    @LOGIN_USER varchar(100),
    @RETURN_MSG varchar(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        IF @USERNAME = @LOGIN_USER
        BEGIN
            SET @RETURN_MSG = 'You cannot delete your own account.';
            RETURN 0;
        END

        UPDATE dbo.TB_M_USER
        SET DELETE_FLAG = '1',
            CHANGED_BY  = @LOGIN_USER,
            CHANGED_DT  = GETDATE()
        WHERE USERNAME = @USERNAME
          AND ISNULL(DELETE_FLAG, '0') <> '1';

        IF @@ROWCOUNT = 0
        BEGIN
            SET @RETURN_MSG = 'User not found or already deleted.';
            RETURN 0;
        END

        SET @RETURN_MSG = 'User deleted successfully.';
        RETURN 1;
    END TRY
    BEGIN CATCH
        SET @RETURN_MSG = ERROR_MESSAGE();
        RETURN 0;
    END CATCH
END
GO

-- =====================================================================
-- 3) LANGKAH MANUAL (source SP hanya ada di DB, sesuaikan sendiri):
--
--    a. sp_User_Search  -> tambahkan di klausa WHERE:
--          AND ISNULL(u.DELETE_FLAG, '0') <> '1'
--       supaya user yang sudah dihapus tidak muncul di grid User Management.
--
--    b. SP yang dipakai proses LOGIN (cek di AuthController / LoginRepo,
--       mis. sp_Login / sp_Auth_Login) -> tambahkan filter yang sama
--       supaya user terhapus tidak bisa login lagi.
--
--    c. (Opsional) SP dropdown yang menampilkan daftar user, mis.
--       sp yang dipakai GetUserName / GetUserNameByDepartmentId,
--       tambahkan filter yang sama.
-- =====================================================================
