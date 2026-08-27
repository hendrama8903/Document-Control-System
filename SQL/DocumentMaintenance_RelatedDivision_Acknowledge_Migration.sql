-- Pisahkan approval "Mengetahui" Divisi Terkait (SPR Level 2) dari chain
-- approval dokumen utama (request Hendra 2026-08-20).
--
-- SEBELUM: langkah "Mengetahui" di-append sebagai WORKFLOW_SEQ tambahan di
-- TB_R_APPROVAL_D yang sama dengan approval berjenjang biasa - masalahnya,
-- approval biasa punya wewenang reject/mundurkan dokumen, sedangkan
-- "Mengetahui" cuma acknowledgment (Div Head sekadar tahu, bukan
-- menyetujui isi). Reject di langkah "Mengetahui" jadi ikut memicu ulang
-- SELURUH proses approval yang sebenarnya tidak relevan, dan progress
-- dashboard (CURRENT_APPROVAL_SEQ/TOTAL_APPROVAL_SEQ) jadi campur antara
-- approval asli & acknowledgment.
--
-- SESUDAH: approval dokumen tetap jalan sendiri lewat TB_R_APPROVAL_H/D
-- (tidak ada langkah "Mengetahui" lagi di situ). Status "Waiting
-- Acknowledgment" (kode 6) baru dipakai KHUSUS untuk kondisi: approval asli
-- sudah selesai, tapi masih menunggu satu/lebih Related Division
-- "Mengetahui" - dokumen baru naik ke Approved (STATUS=1) begitu SEMUANYA
-- selesai. Div Head mengonfirmasi lewat aksi "Acknowledge" tersendiri
-- (sp_DocumentMaintenance_AcknowledgeRelatedDivision) - tanpa opsi reject.

-- 1) Kolom acknowledgment di tabel Related Division (dulu cuma dipakai utk
--    tandai divisi mana yg dipilih, sekarang juga melacak status Mengetahui-nya
--    sendiri, independen dari TB_R_APPROVAL_D).
ALTER TABLE [dbo].[TB_R_DOCUMENT_RELATED_DIVISION]
    ADD [ACKNOWLEDGED_FLAG] BIT NOT NULL CONSTRAINT DF_DOC_RELATED_DIVISION_ACK DEFAULT (0),
        [ACKNOWLEDGED_BY]   VARCHAR(50) NULL,
        [ACKNOWLEDGED_DT]   DATETIME NULL;
GO

-- 2) Status dokumen baru: "Waiting Acknowledgment" (DOC_STATUS = 6).
IF NOT EXISTS (SELECT 1 FROM [dbo].[TB_M_SYSTEM] WHERE SYSTEM_TYPE = 'DOC_STATUS' AND SYSTEM_CODE = '6')
BEGIN
    INSERT INTO [dbo].[TB_M_SYSTEM] (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
    VALUES ('DOC_STATUS', '6', 'Waiting Acknowledgment', 1, 'dms.admin', GETDATE());
END
GO
