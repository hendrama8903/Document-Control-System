-- =====================================================================
-- Add: menu "Inbox" (InboxController + Views/Inbox) - halaman riwayat
-- lengkap notifikasi milik user (baca & belum dibaca), melengkapi
-- dropdown lonceng di header yang sifatnya sementara/hilang begitu
-- notifikasi diklik (STATUS berubah jadi '1' tapi tidak ada tempat
-- untuk melihat riwayatnya lagi).
--
-- Notifikasi sudah scoped per USERNAME sejak awal (lihat
-- sp_Notification_Search), jadi tidak perlu Function granular
-- (Add/Edit/Delete) - cukup akses menu saja, sama seperti Dashboard
-- (M00001) yang di-grant ke semua role.
--
-- MENU_SEQ = 2 mengisi slot kosong di antara Dashboard (1) dan
-- Transactions (3), jadi tidak perlu geser MENU_SEQ menu lain.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00009')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT)
	VALUES ('M00009', NULL, 'Inbox', 'fa-inbox', '/Inbox/Index', 2, 'dms.admin', GETDATE());
END

INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT ROLE_ID, 'M00009', 'dms.admin', GETDATE()
FROM [dbo].[TB_M_ROLE]
WHERE ROLE_ID NOT IN (SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_MENU] WHERE MENU_ID = 'M00009');
