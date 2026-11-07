-- =====================================================================
-- Label status registrasi jadi Title Case, seragam dengan status lain.
-- Request Hendra 2026-11-07.
--
-- Hanya teks tampilan yang berubah; SYSTEM_CODE tetap. Stored procedure tidak
-- terpengaruh karena collation database case-insensitive.
--
-- Urutan deploy: aplikasi baru dulu (warna pill kini tidak peka huruf besar/kecil),
-- baru skrip ini. Aman dijalankan ulang. Jalankan di database DMS_NEW.
--
-- Mengembalikan: UPDATE dbo.TB_M_SYSTEM SET SYSTEM_VALUE = UPPER(SYSTEM_VALUE)
--                WHERE SYSTEM_TYPE IN ('DOC_CTRL_STATUS', 'DOC_REQUEST_STATUS');
-- =====================================================================
SET NOCOUNT ON;

UPDATE S
SET S.SYSTEM_VALUE = V.NEW_VALUE
FROM dbo.TB_M_SYSTEM S
JOIN (VALUES
	('DOC_CTRL_STATUS', '0', 'Draft'),
	('DOC_CTRL_STATUS', '1', 'Checking'),
	('DOC_CTRL_STATUS', '2', 'Accepted'),
	('DOC_CTRL_STATUS', '3', 'Rejected'),
	('DOC_CTRL_STATUS', '4', 'Deleted'),
	('DOC_CTRL_STATUS', '5', 'Sent'),
	('DOC_CTRL_STATUS', '6', 'Partially Sent'),
	('DOC_REQUEST_STATUS', '0', 'Requested')
) V (SYSTEM_TYPE, SYSTEM_CODE, NEW_VALUE)
	ON V.SYSTEM_TYPE = S.SYSTEM_TYPE AND V.SYSTEM_CODE = S.SYSTEM_CODE
WHERE S.SYSTEM_VALUE COLLATE Latin1_General_CS_AS <> V.NEW_VALUE;

SELECT @@ROWCOUNT AS UPDATED_ROWS;
GO
