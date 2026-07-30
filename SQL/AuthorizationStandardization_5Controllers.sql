-- =====================================================================
-- Standarisasi otorisasi: audit menemukan 24 controller sudah punya
-- guard menu-level (Index() cek session "menuURL", 403 kalau tidak
-- match), tapi hanya 15/24 yang JUGA punya gating per-tombol (Add/Edit/
-- Delete via ViewData + functionList, pola sudah ada sejak lama di
-- DepartmentMaster/SectionMaster/dst).
--
-- 9 controller tanpa gating per-tombol dicek satu-satu: 4 di antaranya
-- (DocumentReport, DocumentMasterlist, LogMonitoring,
-- DocumentReceiptReport) memang view-only, tidak ada aksi mutasi jadi
-- memang tidak perlu gating. 5 sisanya PUNYA aksi mutasi (insert/
-- update/delete) tanpa kontrol granular sama sekali - siapa pun yang
-- bisa buka menunya otomatis bisa klik semua tombol:
--   - RoleController   : justru pengatur seluruh sistem otorisasi
--   - MenuController   : sama, pengatur struktur menu & function
--   - UserController   : kelola akun user
--   - LegacyDocumentImportController : commit import dokumen ke DB
--   - ReassignController : reassign approver dokumen
--
-- Fix: tambah ViewData["Add"/"Edit"/dst] + gating @if di 5 controller
-- itu (kode sudah diubah terpisah), mengikuti pola persis yang sudah
-- established di 15 controller lain. Script ini mendaftarkan 15
-- Function baru + grant ke DMS-ADMIN supaya bisa langsung dites, lalu
-- di-share ke role lain lewat halaman Role Authorization.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

DECLARE @Functions TABLE (FUNCTION_ID VARCHAR(50), MENU_ID VARCHAR(50), FUNCTION_NAME VARCHAR(255), FUNCTION_DESC VARCHAR(255));

INSERT INTO @Functions (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC)
VALUES
	('ROLE-ADD', 'M00003-03', 'Add Role', 'Add Function'),
	('ROLE-EDIT', 'M00003-03', 'Edit Role', 'Edit Function'),
	('ROLE-DELETE', 'M00003-03', 'Delete Role', 'Delete Function'),
	('ROLE-AUTHORIZATION', 'M00003-03', 'Set Role Authorization', 'Authorization Function'),

	('MENU-ADD', 'M00003-02', 'Add Menu', 'Add Function'),
	('MENU-EDIT', 'M00003-02', 'Edit Menu', 'Edit Function'),
	('MENU-DELETE', 'M00003-02', 'Delete Menu', 'Delete Function'),
	('MENU-FUNCTION', 'M00003-02', 'Manage Function', 'Function Management'),

	('USER-ADD', 'M00003-01', 'Add User', 'Add Function'),
	('USER-EDIT', 'M00003-01', 'Edit User', 'Edit Function'),
	('USER-DELETE', 'M00003-01', 'Delete User', 'Delete Function'),
	('USER-POSITION', 'M00003-01', 'Manage User Position', 'Position Function'),
	('USER-RESTORE', 'M00003-01', 'Restore Deleted User', 'Restore Function'),

	('LEGACYDOCUMENTIMPORT-IMPORT', 'M00006-05', 'Commit Legacy Document Import', 'Import Function'),

	('REASSIGN-EXECUTE', 'M00003-04', 'Reassign Approver', 'Reassign Function');

INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
SELECT f.FUNCTION_ID, f.MENU_ID, f.FUNCTION_NAME, f.FUNCTION_DESC, 'dms.admin', GETDATE()
FROM @Functions f
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_FUNCTION] existing WHERE existing.FUNCTION_ID = f.FUNCTION_ID
);

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT 'DMS-ADMIN', f.FUNCTION_ID, 'dms.admin', GETDATE()
FROM @Functions f
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] a WHERE a.ROLE_ID = 'DMS-ADMIN' AND a.FUNCTION_ID = f.FUNCTION_ID
);
