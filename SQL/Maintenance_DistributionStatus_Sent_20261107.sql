-- =====================================================================
-- Perbaikan tata bahasa label distribusi: Not Send -> Not Sent, Send -> Sent.
-- Request Hendra 2026-11-07.
--
-- Hanya teks tampilan yang berubah; SYSTEM_CODE tetap. Tidak ada kode yang
-- bergantung pada teks ini. Aman dijalankan ulang. Jalankan di database DMS_NEW.
-- =====================================================================
SET NOCOUNT ON;

UPDATE dbo.TB_M_SYSTEM SET SYSTEM_VALUE = 'Not Sent'
WHERE SYSTEM_TYPE = 'DISTRIBUTION_STATUS' AND SYSTEM_CODE = '0' AND SYSTEM_VALUE = 'Not Send';

UPDATE dbo.TB_M_SYSTEM SET SYSTEM_VALUE = 'Sent'
WHERE SYSTEM_TYPE = 'DISTRIBUTION_STATUS' AND SYSTEM_CODE = '1' AND SYSTEM_VALUE = 'Send';
GO
