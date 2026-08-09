-- =====================================================================
-- Fix: Menu_Add_DocumentSubmissionForm.sql cuma grant menu M00006-09 +
-- function DOCSUBMISSION-* ke role DMS-ADMIN. Padahal untuk benar-benar
-- jalanin alur Submit -> Approve 3-tingkat (Staff -> Section Head -> Dept
-- Head), user aslinya (mis. it04/sechead.itd/depthead.itd) pakai role
-- operasional biasa (GENERAL-STAFF/GENERAL-SECHEAD/GENERAL-DEPTHEAD/dst),
-- bukan DMS-ADMIN - jadi mereka tidak bisa lihat menunya sama sekali.
--
-- Fix: grant ke role yang sama seperti "Document Preparation" (M00006-01),
-- karena kelompok user targetnya identik (siapa saja yang bisa
-- create/approve dokumen).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

INSERT INTO [dbo].[TB_M_AUTH_MENU] (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT ROLE_ID, 'M00006-09', 'dms.admin', GETDATE()
FROM [dbo].[TB_M_AUTH_MENU]
WHERE MENU_ID = 'M00006-01'
AND ROLE_ID NOT IN (SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_MENU] WHERE MENU_ID = 'M00006-09');

INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, F.FUNCTION_ID, 'dms.admin', GETDATE()
FROM (SELECT DISTINCT ROLE_ID FROM [dbo].[TB_M_AUTH_MENU] WHERE MENU_ID = 'M00006-01') R
CROSS JOIN (VALUES
	('DOCSUBMISSION-ADD'),
	('DOCSUBMISSION-EDIT'),
	('DOCSUBMISSION-DELETE'),
	('DOCSUBMISSION-SUBMIT'),
	('DOCSUBMISSION-PRINT'),
	('DOCSUBMISSION-APPROVE')
) AS F(FUNCTION_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] A
	WHERE A.ROLE_ID = R.ROLE_ID AND A.FUNCTION_ID = F.FUNCTION_ID
);
GO
