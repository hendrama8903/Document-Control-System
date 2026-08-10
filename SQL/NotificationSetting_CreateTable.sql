-- =====================================================================
-- Notification Settings - menu admin untuk toggle "kirim email?" per
-- jenis notifikasi, tanpa perlu ubah kode. Notifikasi in-app (Inbox/
-- lonceng) TIDAK dipengaruhi toggle ini - selalu jalan seperti biasa,
-- cuma pengiriman email-nya yang bisa dimatikan per jenis.
--
-- NOTIFICATION_TYPE di sini adalah prefix SYSTEM_TYPE yang sudah dipakai
-- di TB_M_SYSTEM untuk template (mis. "P4D_APPROVAL_REQUEST" untuk
-- P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE + P4D_APPROVAL_REQUEST_NOTIFICATION).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TB_M_NOTIFICATION_SETTING')
BEGIN
	CREATE TABLE [dbo].[TB_M_NOTIFICATION_SETTING] (
		[NOTIFICATION_TYPE]  VARCHAR(100)  NOT NULL PRIMARY KEY,
		[NOTIFICATION_LABEL] VARCHAR(200)  NOT NULL,
		[MODULE_NAME]        VARCHAR(100)  NULL,
		[SEND_EMAIL]         BIT           NOT NULL DEFAULT 1,
		[CREATED_BY]         VARCHAR(50)   NULL,
		[CREATED_DT]         DATETIME      NULL,
		[CHANGED_BY]         VARCHAR(50)   NULL,
		[CHANGED_DT]         DATETIME      NULL
	);
END
GO

-- Seed 9 jenis notifikasi yang saat ini ada di kode (lihat pemanggil
-- EmailService.SendEmailAsync di DocumentMaintenanceController,
-- P4DMaintenanceController, DocumentControlDashboardController,
-- UserDashboardController, ReassignController, DocumentReviewReminderService,
-- ExternalDocumentReviewReminderService). Default SEND_EMAIL = 1 supaya
-- perilaku sistem TIDAK berubah sampai admin sengaja matikan salah satunya.
MERGE INTO [dbo].[TB_M_NOTIFICATION_SETTING] AS target
USING (VALUES
	('DOCUMENT_APPROVAL_REQUEST',      'Document Preparation - Approval Request',   'Document Preparation'),
	('DOCUMENT_APPROVE_REJECT',        'Document Preparation - Approve/Reject Result', 'Document Preparation'),
	('DOCUMENT_REVIEW_REMINDER',       'Document Preparation - Periodic Review Reminder', 'Document Preparation'),
	('P4D_APPROVAL_REQUEST',           'Document Registration - Approval Request',  'Document Registration'),
	('P4D_APPROVE_REJECT',             'Document Registration - Approve/Reject Result', 'Document Registration'),
	('DISTRIBUTION',                   'Document Registration - Distribution Sent', 'Document Registration'),
	('DOCUMENTCONTROL_APPROVAL_REQUEST','Document Control - Approval Request',      'Document Control'),
	('DOCUMENTCONTROL_APPROVE_REJECT', 'Document Control - Approve/Reject Result',  'Document Control'),
	('EXTERNAL_DOC_REVIEW_REMINDER',   'External Document - Periodic Review Reminder', 'External Document')
) AS source (NOTIFICATION_TYPE, NOTIFICATION_LABEL, MODULE_NAME)
ON target.NOTIFICATION_TYPE = source.NOTIFICATION_TYPE
WHEN NOT MATCHED THEN
	INSERT (NOTIFICATION_TYPE, NOTIFICATION_LABEL, MODULE_NAME, SEND_EMAIL, CREATED_BY, CREATED_DT)
	VALUES (source.NOTIFICATION_TYPE, source.NOTIFICATION_LABEL, source.MODULE_NAME, 1, 'dms.admin', GETDATE());
GO
