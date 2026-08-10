-- =====================================================================
-- Add: function "Un-Receive" untuk P4D Maintenance / Document Registration
-- (M00006-02, P4DMaintenanceController.UnReceive) - kebalikan dari fungsi
-- Receive (P4D-RECEIVE) yang sudah ada. Dipakai kalau QMS salah approve
-- registrasi dan perlu membatalkannya kembali ke DRAFT.
--
-- Digrant ke role yang sama dengan P4D-RECEIVE, supaya siapa pun yang boleh
-- Receive juga boleh membatalkannya.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'P4D-UNRECEIVE')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('P4D-UNRECEIVE', 'M00006-02', 'Un-Receive Document', 'P4D Maintenance Un-Receive Function', 'dms.admin', GETDATE());
END

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT ROLE_ID, 'P4D-UNRECEIVE', 'dms.admin', GETDATE()
FROM [dbo].[TB_M_AUTH_FUNCTION]
WHERE FUNCTION_ID = 'P4D-RECEIVE'
	AND ROLE_ID NOT IN (
		SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE FUNCTION_ID = 'P4D-UNRECEIVE'
	);
GO
