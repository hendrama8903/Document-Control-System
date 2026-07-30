-- =====================================================================
-- Fix: modul External Document Control (ExternalDocumentController +
-- Views/ExternalDocument) sudah ada di kode sejak commit "Add external
-- document control module", tapi menu-nya tidak pernah didaftarkan ke
-- TB_M_MENU. Akibatnya screen ini tidak muncul di sidebar sama sekali
-- dan tidak bisa diakses oleh role manapun, walau ini satu-satunya
-- layar yang punya kolom "Next Review Date" untuk memantau dokumen
-- eksternal yang perlu direview berkala.
--
-- Fix: tambahkan menu "External Document" di bawah "Documents"
-- (M00006), plus 5 Function yang memang dicek oleh
-- ExternalDocumentController (EXTERNALDOCUMENT-ADD/EDIT/DELETE/
-- DELETE-FILEPATH/DOWNLOAD-FILEPATH), dan grant awal ke role Admin
-- (DMS-ADMIN) supaya bisa langsung dites lalu di-share ke role lain
-- lewat halaman Role Authorization.
--
-- Catatan penting: menu di sidebar dan Function (tombol Add/Edit/dst)
-- diatur oleh DUA tabel otorisasi terpisah - TB_M_AUTH_MENU (menu apa
-- yang tampil di sidebar per role) dan TB_M_AUTH_FUNCTION (tombol apa
-- yang aktif per role). Menu baru harus di-grant ke KEDUANYA, kalau
-- cuma salah satu, menu bisa hilang dari sidebar walau Function-nya
-- sudah ada (kejadian nyata - baru ketahuan saat login sebagai
-- dms.admin, menu External Document tidak muncul karena baris
-- TB_M_AUTH_MENU-nya lupa ditambahkan).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00006-07')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT)
	VALUES ('M00006-07', 'M00006', 'External Document', 'fa-external-link', '/ExternalDocument/Index', 5, 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'EXTERNALDOCUMENT-ADD')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('EXTERNALDOCUMENT-ADD', 'M00006-07', 'Add', 'External Document Add Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'EXTERNALDOCUMENT-EDIT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('EXTERNALDOCUMENT-EDIT', 'M00006-07', 'Edit', 'External Document Edit Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'EXTERNALDOCUMENT-DELETE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('EXTERNALDOCUMENT-DELETE', 'M00006-07', 'Delete', 'External Document Delete Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'EXTERNALDOCUMENT-DELETE-FILEPATH')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('EXTERNALDOCUMENT-DELETE-FILEPATH', 'M00006-07', 'Delete Attachment', 'External Document Delete Attachment Function', 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'EXTERNALDOCUMENT-DOWNLOAD-FILEPATH')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('EXTERNALDOCUMENT-DOWNLOAD-FILEPATH', 'M00006-07', 'Download Attachment', 'External Document Download Attachment Function', 'dms.admin', GETDATE());
END

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT 'DMS-ADMIN', f.FUNCTION_ID, 'dms.admin', GETDATE()
FROM (VALUES
	('EXTERNALDOCUMENT-ADD'),
	('EXTERNALDOCUMENT-EDIT'),
	('EXTERNALDOCUMENT-DELETE'),
	('EXTERNALDOCUMENT-DELETE-FILEPATH'),
	('EXTERNALDOCUMENT-DOWNLOAD-FILEPATH')
) AS f(FUNCTION_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] a
	WHERE a.ROLE_ID = 'DMS-ADMIN' AND a.FUNCTION_ID = f.FUNCTION_ID
);

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] WHERE ROLE_ID = 'DMS-ADMIN' AND MENU_ID = 'M00006-07')
BEGIN
	INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'M00006-07', 'dms.admin', GETDATE());
END
