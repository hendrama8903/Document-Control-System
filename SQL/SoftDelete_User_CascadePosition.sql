-- =====================================================================
-- Lanjutan migrasi soft-delete User (lihat SQL/SoftDelete_User.sql dan
-- memory dms-user-softdelete-migration.md). sp_User_Delete sudah soft-
-- delete TB_M_USER dengan benar sejak lama, dan sudah menolak delete
-- kalau user masih pending approver aktif (fix 2026-07-20) - tapi belum
-- pernah melepas slot posisi user itu di TB_M_USER_POS.
--
-- Akibatnya: begitu user dihapus, slot posisinya (termasuk slot
-- SINGLETON per divisi seperti Executive Officer, Div. Head, Direktur,
-- Presiden Direktur) tetap "dipegang" user yang sudah tidak bisa login,
-- memblokir assignment orang baru ke slot itu selamanya tanpa ada cara
-- untuk tahu dari UI. Ditemukan saat migrasi ini dibuat (2026-07-30):
-- 26 user yang sudah di-soft-delete masih memegang total 30 baris
-- TB_M_USER_POS, termasuk slot Presiden Direktur, Direktur, dan
-- Executive Officer/Div. Head di hampir semua divisi.
--
-- Fix ini 2 bagian:
-- 1) sp_User_Delete di-ALTER supaya cascade DELETE ke TB_M_USER_POS
--    setiap kali user dihapus (definisi lengkap: lihat
--    SQL/StoredProcedures/sp_User_Delete.sql, sudah CREATE OR ALTER).
-- 2) One-time cleanup: lepas 30 baris TB_M_USER_POS milik user yang
--    SUDAH terlanjur dihapus sebelum fix ini ada.
--
-- PENTING - dampak organisasi: setelah cleanup ini, slot-slot singleton
-- di atas (EO/Div. Head/Direktur/Presdir per divisi) jadi KOSONG lagi
-- (bukan dipegang user hantu). Ini memang tujuannya (supaya bisa
-- di-assign ke orang yang benar-benar aktif), tapi berarti sampai ada
-- yang meng-assign ulang, approval yang butuh role itu tidak akan
-- punya approver sama sekali - beda gejala dari sebelumnya (approver
-- ada tapi tidak bisa login), bukan berarti masalahnya selesai total
-- tanpa tindak lanjut assign ulang oleh admin.
--
-- Idempotent - aman dijalankan ulang (cleanup akan 0 baris kalau sudah
-- pernah jalan).
-- Jalankan di database DMS_NEW
-- =====================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_User_Delete]
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

        IF EXISTS (
            SELECT TOP 1 1
            FROM dbo.TB_R_APPROVAL_D
            WHERE APPROVER = @USERNAME AND STATUS = 0
        )
        BEGIN
            SET @RETURN_MSG = 'User still has pending document approval(s). Please reassign the approver before deleting this user.';
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

        DELETE FROM dbo.TB_M_USER_POS
        WHERE USERNAME = @USERNAME;

        SET @RETURN_MSG = 'User deleted successfully.';
        RETURN 1;
    END TRY
    BEGIN CATCH
        SET @RETURN_MSG = ERROR_MESSAGE();
        RETURN 0;
    END CATCH
END
GO

DELETE P
FROM [dbo].[TB_M_USER_POS] P
JOIN [dbo].[TB_M_USER] U ON U.USERNAME = P.USERNAME
WHERE ISNULL(U.DELETE_FLAG, '0') = '1';
