-- Fix koordinat COL untuk 4 field header SPR (SIPOCOR) di TB_M_EXCEL_TEMPLATE:
-- DOCUMENT_CODE (Nomor Dokumen), DOCUMENT_DATE (Tanggal Dikeluarkan),
-- REVISION (Revisi), DOCUMENT_REVISION_0_DATE (Tanggal Revisi).
--
-- Ditemukan 2026-08-27 (dilaporkan user, hasil cetak SPR): COL=1 (kolom B)
-- ternyata masuk ke dalam merged cell label-nya sendiri (A:C, mis. "Nomor
-- Dokumen") di template SPR.xlsx saat ini - value yang ditulis sistem jadi
-- menimpa teks label itu (dikonfirmasi lewat Excel COM: MergeArea A4:C4
-- untuk label, D4 cuma ":", kolom E baru kosong/siap diisi value).
--
-- ROW sudah benar (baris 4-7 = 0-indexed 3-6), cuma COL yang salah - dipetakan
-- ulang ke kolom E (0-indexed 4), persis setelah karakter ":" di kolom D.
UPDATE TB_M_EXCEL_TEMPLATE
SET COL = 4
WHERE DOCUMENT_ID = 15
  AND FIELD_NAME IN ('DOCUMENT_CODE', 'DOCUMENT_DATE', 'REVISION', 'DOCUMENT_REVISION_0_DATE');

-- SPR (DOCUMENT_ID=15) juga sama sekali belum punya mapping untuk field JUDUL
-- (DOCUMENT_TRANSACTION_NAME) - sel "JUDUL" (merge A8:U11) tidak pernah ditimpa
-- value asli, tercetak literal "JUDUL" terus. Ditambahkan mengikuti pola TYPE=1
-- yang sudah dipakai tipe dokumen lain (lihat DOCUMENT_ID 1-8).
INSERT INTO TB_M_EXCEL_TEMPLATE (DOCUMENT_ID, FIELD_NAME, ROW, COL, TYPE, SHEET_ORIENTATION)
VALUES (15, 'DOCUMENT_TRANSACTION_NAME', 7, 0, 1, 1);
