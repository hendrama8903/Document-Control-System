-- =====================================================================
-- Master setting metode tanda tangan (request Hendra 2026-11-09).
--
-- Satu metode global untuk seluruh sistem, dipilih admin di
-- Administration > Signature Settings:
--   TULIS  = digambar / diketik setiap tanda tangan (perilaku sejak 2026-11-06)
--   QR     = kode QR berisi tautan verifikasi (wajib login untuk membukanya)
--   UPLOAD = gambar tanda tangan dari profil user (disalin ke TB_R_SIGNATURE saat
--            menandatangani, jadi mengganti gambar profil tidak mengubah dokumen lama)
--
-- Aman dijalankan ulang. Jalankan di database DMS_NEW, lalu pasang
-- SQL/StoredProcedures/sp_Signature_Save.sql (menerima metode QR/UPLOAD).
-- =====================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- TB_R_SIGNATURE.SIGN_METHOD dulu hanya DRAW/TYPE.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_TB_R_SIGNATURE_METHOD' AND parent_object_id = OBJECT_ID('dbo.TB_R_SIGNATURE'))
	ALTER TABLE dbo.TB_R_SIGNATURE DROP CONSTRAINT CK_TB_R_SIGNATURE_METHOD;
ALTER TABLE dbo.TB_R_SIGNATURE ADD CONSTRAINT CK_TB_R_SIGNATURE_METHOD
	CHECK (SIGN_METHOD IN ('DRAW', 'TYPE', 'QR', 'UPLOAD'));

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_SYSTEM WHERE SYSTEM_TYPE = 'SIGNATURE_SETTING' AND SYSTEM_CODE = 'METHOD')
	INSERT INTO dbo.TB_M_SYSTEM (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('SIGNATURE_SETTING', 'METHOD', 'TULIS', 1, 'dms.admin', GETDATE());

-- Alamat server untuk tautan di kode QR (mis. http://dcs.hmmi.co.id). Kosong = alamat
-- yang sedang dipakai saat dokumen dibuka.
IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_SYSTEM WHERE SYSTEM_TYPE = 'SIGNATURE_SETTING' AND SYSTEM_CODE = 'VERIFY_BASE_URL')
	INSERT INTO dbo.TB_M_SYSTEM (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
	VALUES ('SIGNATURE_SETTING', 'VERIFY_BASE_URL', '', 1, 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00003-07')
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00003-07', 'M00003', 'Signature Settings', 'fa-pencil-square-o', '/SignatureSetting/Index', 8, 'dms.admin', GETDATE(), 0);

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_MENU WHERE ROLE_ID = 'DMS-ADMIN' AND MENU_ID = 'M00003-07')
	INSERT INTO dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'M00003-07', 'dms.admin', GETDATE());

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION WHERE FUNCTION_ID = 'SIGNATURESETTING-EDIT')
	INSERT INTO dbo.TB_M_FUNCTION (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('SIGNATURESETTING-EDIT', 'M00003-07', 'Edit', 'Signature Settings Change Signing Method Function', 'dms.admin', GETDATE(), 0);

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_FUNCTION WHERE ROLE_ID = 'DMS-ADMIN' AND FUNCTION_ID = 'SIGNATURESETTING-EDIT')
	INSERT INTO dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'SIGNATURESETTING-EDIT', 'dms.admin', GETDATE());

COMMIT TRANSACTION;
GO
