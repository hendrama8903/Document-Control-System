-- =====================================================================
-- Add: menu "Notification Settings" (NotificationSettingController) di
-- bawah Administration - toggle kirim-email ya/tidak per jenis
-- notifikasi (TB_M_NOTIFICATION_SETTING, lihat
-- NotificationSetting_CreateTable.sql).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW (setelah NotificationSetting_CreateTable.sql)
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00003-05')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00003-05', 'M00003', 'Notification Settings', 'fa-envelope-o', '/NotificationSetting/Index', 7, 'dms.admin', GETDATE(), 0);
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'NOTIFICATIONSETTING-TOGGLE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('NOTIFICATIONSETTING-TOGGLE', 'M00003-05', 'Toggle Email', 'Notification Settings Toggle Send-Email Function', 'dms.admin', GETDATE());
END

-- Grant ke role yang sama dengan Menu & Function (admin-level roles) -
-- ini pengaturan sistem, bukan sesuatu yang perlu diakses staff biasa.
INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT ROLE_ID, 'M00003-05', 'dms.admin', GETDATE()
FROM [dbo].[TB_M_AUTH_MENU]
WHERE MENU_ID = 'M00003-02' -- Menu & Function
	AND ROLE_ID NOT IN (SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_MENU] WHERE MENU_ID = 'M00003-05');

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT ROLE_ID, 'NOTIFICATIONSETTING-TOGGLE', 'dms.admin', GETDATE()
FROM [dbo].[TB_M_AUTH_MENU]
WHERE MENU_ID = 'M00003-05'
	AND ROLE_ID NOT IN (SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE FUNCTION_ID = 'NOTIFICATIONSETTING-TOGGLE');
GO
