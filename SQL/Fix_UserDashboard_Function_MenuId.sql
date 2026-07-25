-- =====================================================================
-- Fix: fungsi User Dashboard (Download, Publish, Request) di
-- TB_M_FUNCTION masih menunjuk ke MENU_ID = 'M00006-03', padahal menu
-- itu sudah tidak ada di TB_M_MENU. Menu "User Dashboard" yang aktif
-- sekarang adalah M00006-06 (dibuat 2026-07-24, kemungkinan menu
-- di-reorganisasi/diberi ID baru tapi fungsi-fungsinya lupa ikut
-- diupdate).
--
-- Akibatnya: halaman Role Authorization tidak bisa menampilkan
-- checkbox Download/Publish/Request di bawah "User Dashboard" untuk
-- role manapun - jadi fungsi Publish tidak pernah bisa di-grant lewat
-- UI, walau tabel TB_M_AUTH_FUNCTION sendiri masih bisa menyimpan
-- baris lama (role yang sudah diberi akses sebelum menu di-reorganisasi
-- tetap berfungsi, tapi tidak bisa diubah/diberikan ke role baru lewat UI).
--
-- Jalankan di database DMS_NEW
-- =====================================================================

UPDATE TB_M_FUNCTION
SET MENU_ID = 'M00006-06', CHANGED_DT = GETDATE()
WHERE FUNCTION_ID IN ('USERDASHBOARD-DOWNLOAD', 'USERDASHBOARD-PUBLISH', 'USERDASHBOARD-REQUEST');
