-- =====================================================================
-- Penyempurnaan lanjutan dari SQL/NamingCleanup_MenuAndFunction.sql,
-- setelah klarifikasi alur bisnis dari Hendra:
-- "Document Maintenance" = proses membuat dokumen di level user,
-- setelah itu didaftarkan ke QMS lewat "Document Distribution Request".
--
--   M00006-01 "Document Maintenance"          -> "Document Preparation"
--     (lebih presisi: tahap penyusunan dokumen di level pemilik proses,
--     sebelum masuk kendali formal QMS - bukan cuma "maintenance" umum)
--   M00006-02 "Document Distribution Request" -> "Document Registration"
--     (nama sebelumnya kepanjangan; "Registration" tetap menangkap
--     makna "didaftarkan ke QMS" tapi lebih ringkas)
--
-- Kode ViewData["Title"] di DocumentMaintenanceController.cs dan
-- P4DMaintenanceController.cs sudah disamakan terpisah.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

UPDATE [dbo].[TB_M_MENU] SET MENU_NAME = 'Document Preparation' WHERE MENU_ID = 'M00006-01';
UPDATE [dbo].[TB_M_MENU] SET MENU_NAME = 'Document Registration' WHERE MENU_ID = 'M00006-02';

-- "Document Report" (M00008-01) namanya generik, padahal isinya spesifik:
-- sp_DocumentMaintenance_SearchReport sumbernya TB_R_DOCUMENT_DISTRIBUTION
-- (bukan cuma TB_R_DOCUMENT) - satu baris per dokumen x department tujuan
-- distribusi, lengkap tanggal distribusi + status acknowledge
-- (TB_R_PUBLISH_HISTORY). Ini register pelacakan distribusi untuk tim QMS,
-- bukan laporan umum - dinamai ulang supaya jelas fungsinya, beda dari
-- "Document Masterlist" (M00008-02, katalog semua dokumen tanpa filter
-- status registrasi/distribusi).
UPDATE [dbo].[TB_M_MENU] SET MENU_NAME = 'Document Distribution Register' WHERE MENU_ID = 'M00008-01';
