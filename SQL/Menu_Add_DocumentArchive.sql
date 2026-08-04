-- =====================================================================
-- Tambah menu "Document Archive (ISO)" - arsip khusus staff ISO/QMS
-- untuk dokumen yang siklus distribusinya sudah selesai (P4D distribution
-- STATUS=2/RECEIVED). Read-only, reuse DocumentArchiveController yang
-- pada gilirannya reuse P4DMaintenanceRepo.Search dengan STATUS dipaksa
-- ke '2' di server (lihat DocumentArchiveController.Search) - tidak ada
-- tabel/SP baru.
--
-- Tidak ada Function baru karena controller-nya murni read-only (tidak
-- ada Add/Edit/Delete/Send/Receive), jadi cukup TB_M_MENU + TB_M_AUTH_MENU.
--
-- Data lintas departemen (tidak di-scope, sesuai kebutuhan arsip pusat
-- ISO) - lihat DocumentArchiveController.Search yang memanggil
-- P4DMaintenanceRepo.Search dengan loginUser = null, sama seperti
-- DocumentControlDashboardController.
--
-- Role di-grant sama seperti menu "Document Registration" (M00006-02)
-- saat ini punya.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00006-08')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT)
	VALUES ('M00006-08', 'M00006', 'Document Archive (ISO)', 'fa-archive', '/DocumentArchive/Index', 6, 'dms.admin', GETDATE());
END

INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT a.ROLE_ID, 'M00006-08', 'dms.admin', GETDATE()
FROM [dbo].[TB_M_AUTH_MENU] a
WHERE a.MENU_ID = 'M00006-02'
AND NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] b
	WHERE b.ROLE_ID = a.ROLE_ID AND b.MENU_ID = 'M00006-08'
);
