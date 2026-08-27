-- =====================================================================
-- Tambah TB_M_EXCEL_TEMPLATE.TARGET_POSITION_ID (2026-08-18)
--
-- Kotak tanda tangan (TYPE=2/DIGITAL_SIGN) selama ini dipetakan ke
-- approver murni berdasarkan URUTAN approval (WORKFLOW_SEQ) - cocok
-- untuk template gaya IK/SOP/OPL/ACU/EIS/SOE yang box-nya cuma
-- "approver ke-2", "approver ke-3" tanpa peduli jabatan aslinya apa.
--
-- Template PDM/PRO (Pedoman/Prosedur) beda: box-nya punya caption
-- jabatan SPESIFIK yang tercetak di file ("DEPT. HEAD", "MIN. DIV.
-- HEAD", "MIN. EXECUTIVE OFFICER") - jadi box mana yang harus diisi
-- approver mana TIDAK BOLEH cuma ikut urutan, tapi harus dicocokkan ke
-- POSITION_ID approver yang sebenarnya (request Hendra 2026-08-18,
-- kasus divhead.itd approve sebagai langkah pertama Level 1 tapi
-- posisinya "Div. Head", bukan "Dept. Head" - harus masuk kotak Div.
-- Head, bukan ikut-ikutan jadi kotak pertama).
--
-- TARGET_POSITION_ID NULL (default, semua baris lama TIDAK berubah)
-- = tetap pakai logika urutan lama, tidak ada perubahan perilaku.
-- TARGET_POSITION_ID = POSITION_ID asli (lihat TB_M_POSITION) = box
-- ini HANYA diisi approver yang POSITION_ID user-nya persis cocok.
-- TARGET_POSITION_ID = -1 (sentinel) = box ini diisi approver TERAKHIR
-- dalam chain (WORKFLOW_SEQ tertinggi), apapun jabatannya - dipakai
-- untuk kotak "Disetujui Oleh" paling akhir yang captionnya generik
-- (mis. "MIN. EXECUTIVE OFFICER") tapi sebenarnya harus diisi siapapun
-- approver puncak (bisa EO, bisa Direktur, dst - tergantung chain).
--
-- Jalankan di database DMS_NEW
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('TB_M_EXCEL_TEMPLATE') AND name = 'TARGET_POSITION_ID')
BEGIN
	ALTER TABLE TB_M_EXCEL_TEMPLATE ADD TARGET_POSITION_ID INT NULL;
END
GO

-- Isi untuk PDM (DOCUMENT_ID=1) & PRO (DOCUMENT_ID=2), sheet "LEMBAR
-- PENGESAHAN" (SHEET_POSITION=1) - 3 box approver saja (box "PIC"
-- untuk pembuat dokumen tidak perlu, itu selalu unconditional):
--   box "DEPT. HEAD"            -> POSITION_ID 3 (Dept. Head)
--   box "MIN. DIV. HEAD"        -> POSITION_ID 4 (Div. Head)
--   box "MIN. EXECUTIVE OFFICER"-> -1 (approver terakhir dalam chain)
UPDATE TB_M_EXCEL_TEMPLATE
SET TARGET_POSITION_ID = 3
WHERE DOCUMENT_ID IN (1, 2) AND FIELD_NAME = 'DIGITAL_SIGN' AND SHEET_POSITION = 1 AND [ROW] = 17 AND COL = 22;

UPDATE TB_M_EXCEL_TEMPLATE
SET TARGET_POSITION_ID = 4
WHERE DOCUMENT_ID IN (1, 2) AND FIELD_NAME = 'DIGITAL_SIGN' AND SHEET_POSITION = 1 AND [ROW] = 27 AND COL = 22;

UPDATE TB_M_EXCEL_TEMPLATE
SET TARGET_POSITION_ID = -1
WHERE DOCUMENT_ID IN (1, 2) AND FIELD_NAME = 'DIGITAL_SIGN' AND SHEET_POSITION = 1 AND [ROW] = 38 AND COL = 22;
GO
