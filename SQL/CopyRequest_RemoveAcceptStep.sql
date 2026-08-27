-- =====================================================================
-- Copy Request: hapus step "Accept" (konfirmasi terima copy fisik) dan
-- auto-distribusi TB_R_CTRL_DOCUMENT setelah Approve.
--
-- Alasan: kedua fitur ini peninggalan model lama di mana QMS/orang lain
-- yang mencetak & menyerahkan fisik ke requester, jadi perlu langkah
-- konfirmasi terpisah ("saya sudah terima") dan pencatatan distribusi ke
-- UserDashboard untuk di-Acknowledge. Sekarang requester CETAK SENDIRI
-- lewat PrintTrack begitu status Approved - tidak ada lagi pihak ketiga
-- yang menyerahkan sesuatu, dan kontrol yang relevan sudah tercatat
-- lengkap di TB_R_COPY_REQUEST_PRINT_LOG. Alur yang diinginkan cuma:
-- Submit -> QMS Approve -> requester langsung bisa cetak (request Hendra
-- 2026-08-15).
--
-- Fulfillment (FulfillApprovedRequest, bikin baris TB_R_CTRL_DOCUMENT
-- lewat sp_UserDashboard_Request) sudah dihapus dari
-- CopyRequestController.cs di commit yang sama - tidak ada perubahan SP
-- untuk itu karena sp_UserDashboard_Request sendiri masih dipakai fitur
-- lain (tombol "Request Document" di UserDashboard), cuma pemanggilannya
-- dari Copy Request yang dicabut.
--
-- Sekalian rename menu "Controlled Copy Request" -> "Print Request" -
-- lebih menggambarkan alur sekarang (izin cetak, bukan distribusi fisik
-- oleh QMS).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] DROP COLUMN IF EXISTS ACCEPTED_FLAG;
ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] DROP COLUMN IF EXISTS ACCEPTED_BY;
ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] DROP COLUMN IF EXISTS ACCEPTED_DT;
GO

DELETE FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-ACCEPT';
DELETE FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-ACCEPT';

-- Definisi lengkap diarsipkan di SQL/StoredProcedures/_dropped/ untuk
-- referensi historis - lihat README di folder itu.
DROP PROCEDURE IF EXISTS [dbo].[sp_CopyRequest_Accept];

UPDATE [dbo].[TB_M_MENU] SET MENU_NAME = 'Print Request', CHANGED_DT = GETDATE() WHERE MENU_ID = 'M00006-08';
GO
