-- =====================================================================
-- Konsistensi penamaan: "IAD" adalah singkatan divisi "Internal Audit
-- Div" (kode divisi), tapi modul ini fungsinya generik "Document
-- Control" (menu-nya sendiri sudah benar berlabel "Document Control" -
-- lihat TB_M_MENU M00006-04). Nama internal (controller, route,
-- permission key, template key) masih pakai singkatan divisi lama,
-- membingungkan siapa pun yang baca kode tanpa tahu sejarahnya.
--
-- Fix: rename semua identifier "IAD.../IADDashboard/IAD_..." jadi
-- "DocumentControlDashboard/DOCUMENTCONTROL...". Sisi C# (controller,
-- repo, DbContext, view folder) sudah di-rename terpisah di kode -
-- script ini menyamakan sisi database-nya:
-- 1) 2 stored procedure: sp_IADDashboard_Search, sp_IADDashboard_SendDocument
-- 2) TB_M_MENU.MENU_URL: /IADDashboard/Index -> /DocumentControlDashboard/Index
-- 3) TB_M_FUNCTION.FUNCTION_ID + TB_M_AUTH_FUNCTION (5 role grant): IADDASHBOARD-SEND
-- 4) TB_M_SYSTEM: 4 SYSTEM_TYPE (IAD_APPROVAL_REQUEST_EMAIL_TEMPLATE, dst) +
--    2 BUTTON_LINK/URL yang isinya link /IADDashboard/... + teks body
--    email yang literally menyebut "Divisi IAD" (harusnya generik
--    "Document Control", bukan divisi tertentu)
-- 5) TB_R_NOTIFICATION: 3 baris notifikasi lama yang link-nya masih
--    /IADDashboard/... (supaya tidak 404 kalau notifikasi lama diklik)
--
-- Tidak ada FK constraint di semua tabel yang disentuh (sudah dicek
-- sys.foreign_keys) jadi aman di-UPDATE langsung tanpa cascade drama.
--
-- Idempotent - aman dijalankan ulang (kalau sudah pernah jalan, semua
-- WHERE clause di bawah tidak akan match apa pun lagi).
-- Jalankan di database DMS_NEW
-- =====================================================================

-- 1) Rename stored procedure (isi/definisi tetap sama persis)
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_IADDashboard_Search')
BEGIN
    EXEC sp_rename 'sp_IADDashboard_Search', 'sp_DocumentControlDashboard_Search';
END

IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'sp_IADDashboard_SendDocument')
BEGIN
    EXEC sp_rename 'sp_IADDashboard_SendDocument', 'sp_DocumentControlDashboard_SendDocument';
END

-- 2) Menu URL
UPDATE [dbo].[TB_M_MENU]
SET MENU_URL = '/DocumentControlDashboard/Index'
WHERE MENU_URL = '/IADDashboard/Index';

-- 3) Function permission key (UPDATE di TB_M_AUTH_FUNCTION dulu supaya tidak
-- kehilangan referensi row TB_M_FUNCTION lama di tengah proses)
UPDATE [dbo].[TB_M_AUTH_FUNCTION]
SET FUNCTION_ID = 'DOCUMENTCONTROLDASHBOARD-SEND'
WHERE FUNCTION_ID = 'IADDASHBOARD-SEND';

UPDATE [dbo].[TB_M_FUNCTION]
SET FUNCTION_ID = 'DOCUMENTCONTROLDASHBOARD-SEND'
WHERE FUNCTION_ID = 'IADDASHBOARD-SEND';

-- 4a) Rename 4 SYSTEM_TYPE template key
UPDATE [dbo].[TB_M_SYSTEM]
SET SYSTEM_TYPE = 'DOCUMENTCONTROL_APPROVAL_REQUEST_EMAIL_TEMPLATE'
WHERE SYSTEM_TYPE = 'IAD_APPROVAL_REQUEST_EMAIL_TEMPLATE';

UPDATE [dbo].[TB_M_SYSTEM]
SET SYSTEM_TYPE = 'DOCUMENTCONTROL_APPROVAL_REQUEST_NOTIFICATION'
WHERE SYSTEM_TYPE = 'IAD_APPROVAL_REQUEST_NOTIFICATION';

UPDATE [dbo].[TB_M_SYSTEM]
SET SYSTEM_TYPE = 'DOCUMENTCONTROL_APPROVE_REJECT_EMAIL_TEMPLATE'
WHERE SYSTEM_TYPE = 'IAD_APPROVE_REJECT_EMAIL_TEMPLATE';

UPDATE [dbo].[TB_M_SYSTEM]
SET SYSTEM_TYPE = 'DOCUMENTCONTROL_APPROVE_REJECT_NOTIFICATION'
WHERE SYSTEM_TYPE = 'IAD_APPROVE_REJECT_NOTIFICATION';

-- 4b) Perbaiki isi link yang masih hardcode /IADDashboard/
UPDATE [dbo].[TB_M_SYSTEM]
SET SYSTEM_VALUE = REPLACE(SYSTEM_VALUE, '/IADDashboard/', '/DocumentControlDashboard/')
WHERE SYSTEM_VALUE LIKE '%/IADDashboard/%';

-- 4c) Perbaiki teks body email yang literally menyebut "Divisi IAD" -
-- fungsinya generik document control, bukan cuma divisi Internal Audit
UPDATE [dbo].[TB_M_SYSTEM]
SET SYSTEM_VALUE = REPLACE(SYSTEM_VALUE, 'Divisi IAD', 'Document Control')
WHERE SYSTEM_TYPE = 'DOCUMENTCONTROL_APPROVAL_REQUEST_EMAIL_TEMPLATE'
  AND SYSTEM_CODE = 'BODY'
  AND SYSTEM_VALUE LIKE '%Divisi IAD%';

-- 5) Notifikasi lama yang masih nyimpen link /IADDashboard/
UPDATE [dbo].[TB_R_NOTIFICATION]
SET NOTIFICATION_URL = REPLACE(NOTIFICATION_URL, '/IADDashboard/', '/DocumentControlDashboard/')
WHERE NOTIFICATION_URL LIKE '%/IADDashboard/%';

-- 6) Ditemukan saat verifikasi: template email P4D (beda modul, SYSTEM_TYPE
-- 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE', dikirim dari P4DMaintenanceController)
-- juga literally menyebut "Divisi IAD" di body-nya, padahal penerima aslinya
-- department mana pun yang punya DOCUMENT_CONTROL_ACCESS=1 (saat ini QPD,
-- bukan IAD) - salah sebut, bukan cuma soal gaya penamaan.
UPDATE [dbo].[TB_M_SYSTEM]
SET SYSTEM_VALUE = REPLACE(SYSTEM_VALUE, 'Divisi IAD', 'Document Control')
WHERE SYSTEM_TYPE = 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE'
  AND SYSTEM_CODE = 'BODY'
  AND SYSTEM_VALUE LIKE '%Divisi IAD%';
