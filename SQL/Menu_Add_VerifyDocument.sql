-- =====================================================================
-- Add: menu "Verify Document" (VerifyController.Index, request Hendra 2026-11-10).
-- Reads the QR code of a signature box from a computer (upload an image or PDF, webcam, or
-- paste the link / code) and opens /Verify/Signature/<code>. Every logged-in user may verify,
-- so the menu is granted to every role, together with its parent Transactions (M00006).
--
-- M00006-09 is taken by the deactivated "Document Request" menu, hence M00006-10.
-- Idempotent - safe to run again.
-- Run on database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_MENU] WHERE MENU_ID = 'M00006-10')
BEGIN
	INSERT INTO [dbo].[TB_M_MENU] (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00006-10', 'M00006', 'Verify Document', 'fa-qrcode', '/Verify/Index', 7, 'dms.admin', GETDATE(), 0);
END

INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, M.MENU_ID, 'dms.admin', GETDATE()
FROM [dbo].[TB_M_ROLE] R
CROSS JOIN (VALUES ('M00006'), ('M00006-10')) M(MENU_ID)
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_AUTH_MENU] A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = M.MENU_ID);
