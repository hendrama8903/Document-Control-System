-- Tambah peran per Divisi Terkait (request Hendra 2026-08-20): dulu cuma ada
-- 1 kategori tersimpan ("Related", wajib Acknowledge) - Main PIC selalu
-- diturunkan otomatis dari TB_R_DOCUMENT.DIVISION (creator), tidak pernah
-- disimpan sendiri. Masalahnya: kadang divisi yang SEHARUSNYA jadi Main PIC
-- bukan divisi pembuat dokumen, dan sistem tidak punya cara merepresentasikan
-- itu. Sekarang tiap baris TB_R_DOCUMENT_RELATED_DIVISION punya peran sendiri
-- yang dipilih manual saat Add Document:
--   MAIN_PIC     - penanggung jawab utama, TIDAK wajib Acknowledge, boleh lebih
--                  dari satu divisi.
--   RELATED      - wajib Acknowledge ("Mengetahui"), sama seperti perilaku lama.
--   NOTE_RELATED - info saja, tidak wajib Acknowledge, tidak menahan status
--                  dokumen.
--
-- Data lama (sebelum kolom ini ada) SEMUANYA berarti "Related" (satu-satunya
-- kategori yang pernah disimpan di tabel ini), jadi default & backfill-nya
-- 'RELATED'.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('[dbo].[TB_R_DOCUMENT_RELATED_DIVISION]') AND name = 'DIVISION_ROLE'
)
BEGIN
    ALTER TABLE [dbo].[TB_R_DOCUMENT_RELATED_DIVISION]
        ADD [DIVISION_ROLE] VARCHAR(20) NOT NULL CONSTRAINT DF_DOC_RELATED_DIVISION_ROLE DEFAULT ('RELATED');
END
GO
