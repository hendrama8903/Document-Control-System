-- =====================================================================
-- Copy Request: tambah langkah "Accepted" oleh requester setelah QMS
-- Approve - mirror pola Send Distribution (P4D) + Accepted (UserDashboard):
-- P4D/UserDashboard pakai 2 modul terpisah karena distribusi multi-department,
-- Copy Request cukup 1 flag di header karena satu request = satu requester/
-- department saja (request Hendra 2026-08-13).
--
-- Sebelum ini: STATUS Approved (2) sudah final dari sisi requester - tidak
-- ada langkah konfirmasi "saya sudah terima copy fisiknya". Sekarang
-- ditambah ACCEPTED_FLAG terpisah dari STATUS (STATUS tetap Approved,
-- tidak di-renumber) supaya kompatibel dengan data lama & query STATUS yang
-- sudah ada.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TB_R_COPY_REQUEST_H') AND name = 'ACCEPTED_FLAG')
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] ADD ACCEPTED_FLAG INT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TB_R_COPY_REQUEST_H') AND name = 'ACCEPTED_BY')
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] ADD ACCEPTED_BY VARCHAR(255) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TB_R_COPY_REQUEST_H') AND name = 'ACCEPTED_DT')
	ALTER TABLE [dbo].[TB_R_COPY_REQUEST_H] ADD ACCEPTED_DT DATETIME NULL;
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID = 'COPYREQUEST-ACCEPT')
BEGIN
	INSERT INTO [dbo].[TB_M_FUNCTION] (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT)
	VALUES ('COPYREQUEST-ACCEPT', 'M00006-08', 'Accepted', 'Controlled Copy Request Accept (requester confirms receipt) Function', 'dms.admin', GETDATE());
END

-- Digrant ke role set requester yang sama dengan ADD/EDIT/SUBMIT/PRINT -
-- ini konfirmasi dari SISI REQUESTER, bukan tugas QMS (beda dengan
-- COPYREQUEST-APPROVE yang khusus role QPD).
INSERT INTO [dbo].[TB_M_AUTH_FUNCTION] (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT r.ROLE_ID, 'COPYREQUEST-ACCEPT', 'dms.admin', GETDATE()
FROM (VALUES
	('DeptHead'), ('DMS-ADMIN'), ('DMS-ADMIN-DEPT'), ('GENERAL-DEPTHEAD'),
	('GENERAL-DIVHEAD'), ('GENERAL-EO'), ('GENERAL-SECHEAD'), ('GENERAL-STAFF'),
	('PIC'), ('SecHead'), ('Staff')
) AS r(ROLE_ID)
WHERE NOT EXISTS (
	SELECT 1 FROM [dbo].[TB_M_AUTH_FUNCTION] a
	WHERE a.ROLE_ID = r.ROLE_ID AND a.FUNCTION_ID = 'COPYREQUEST-ACCEPT'
);
GO
