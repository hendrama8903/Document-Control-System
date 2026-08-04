-- =====================================================================
-- Rename label Document Type di form Add Document Preparation (Jul 2026)
-- supaya bahasanya Inggris konsisten dengan sisa form: "Baru" -> "New",
-- "Revisi" -> "Revision". Kode internal ('01'/'02') tidak berubah.
--
-- Idempotent. Jalankan di database DMS_NEW.
-- =====================================================================

UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = 'New' WHERE SYSTEM_TYPE = 'DOCUMENT_TYPE' AND SYSTEM_CODE = '01';
UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = 'Revision' WHERE SYSTEM_TYPE = 'DOCUMENT_TYPE' AND SYSTEM_CODE = '02';
