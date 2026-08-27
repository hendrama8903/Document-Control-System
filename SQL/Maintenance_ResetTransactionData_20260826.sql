-- =====================================================================
-- Maintenance: reset semua data transaksi + nomor urut dokumen ke awal,
-- untuk mulai testing/UAT dari kondisi bersih (2026-08-26).
--
-- Versi diperbarui dari Maintenance_ResetTransactionData_20260810.sql -
-- script lama itu BELUM mencakup tabel yang ditambahkan setelah
-- 2026-08-10: TB_R_COPY_REQUEST_H/D/PRINT_LOG (fitur Copy Request) dan
-- TB_R_DOCUMENT_RELATED_DIVISION (fitur Divisi Terkait/Mengetahui).
--
-- Backup diambil dulu (SELECT INTO ..._BACKUP_20260826) sebelum
-- TRUNCATE/DELETE, supaya masih bisa dipulihkan kalau ternyata perlu.
-- Backup lama (_BACKUP_20260810/16/16b/16c/18/19) TIDAK disentuh.
--
-- Urutan memperhatikan FK (dicek lewat sys.foreign_keys sebelum jalan -
-- ada 4 FK constraint nyata di antara tabel TB_R_ per 2026-08-26):
--   FK_DSF_D_SUBMISSION            : DOC_SUBMISSION_FORM_D -> _H
--   FK_CPR_D_REQUEST               : COPY_REQUEST_D -> COPY_REQUEST_H
--   FK_CPR_PRINTLOG_REQUEST_DETAIL : COPY_REQUEST_PRINT_LOG -> COPY_REQUEST_D
--   FK_DOC_RELATED_DIVISION_DOCUMENT: DOCUMENT_RELATED_DIVISION -> DOCUMENT
--
-- SQL Server menolak TRUNCATE pada tabel manapun yang masih PUNYA FK
-- constraint mengarah ke dirinya (constraint-nya sendiri, bukan isi
-- datanya, yang dicek) - jadi tabel yang jadi sisi "referenced" di atas
-- (DOC_SUBMISSION_FORM_H, COPY_REQUEST_H, COPY_REQUEST_D, TB_R_DOCUMENT)
-- pakai DELETE + DBCC CHECKIDENT manual, bukan TRUNCATE. TB_R_DOCUMENT
-- BARU butuh perlakuan ini mulai sekarang - FK_DOC_RELATED_DIVISION_DOCUMENT
-- belum ada saat script 20260810 ditulis, jadi TRUNCATE TB_R_DOCUMENT masih
-- berhasil waktu itu.
--
-- TB_M_EXTERNAL_DOCUMENT ikut dibersihkan meski prefix "TB_M_" - isinya
-- record dokumen eksternal (transaksional), bukan data master/lookup.
--
-- Skrip sekali-jalan - jangan dijalankan ulang tanpa sadar.
-- Jalankan di database DMS_NEW
-- =====================================================================

-- ---------------------------------------------------------------------
-- Bagian 1: Backup
-- ---------------------------------------------------------------------
SELECT * INTO TB_R_DOCUMENT_BACKUP_20260826 FROM TB_R_DOCUMENT;
SELECT * INTO TB_R_CTRL_DOCUMENT_BACKUP_20260826 FROM TB_R_CTRL_DOCUMENT;
SELECT * INTO TB_R_DOCUMENT_DISTRIBUTION_BACKUP_20260826 FROM TB_R_DOCUMENT_DISTRIBUTION;
SELECT * INTO TB_R_DOCUMENT_LOG_BACKUP_20260826 FROM TB_R_DOCUMENT_LOG;
SELECT * INTO TB_R_DOCUMENT_HISTORY_BACKUP_20260826 FROM TB_R_DOCUMENT_HISTORY;
SELECT * INTO TB_R_PUBLISH_HISTORY_BACKUP_20260826 FROM TB_R_PUBLISH_HISTORY;
SELECT * INTO TB_R_APPROVAL_H_BACKUP_20260826 FROM TB_R_APPROVAL_H;
SELECT * INTO TB_R_APPROVAL_D_BACKUP_20260826 FROM TB_R_APPROVAL_D;
SELECT * INTO TB_R_APPROVAL_REASSIGN_LOG_BACKUP_20260826 FROM TB_R_APPROVAL_REASSIGN_LOG;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_H_BACKUP_20260826 FROM TB_R_DOC_SUBMISSION_FORM_H;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_D_BACKUP_20260826 FROM TB_R_DOC_SUBMISSION_FORM_D;
SELECT * INTO TB_R_NOTIFICATION_BACKUP_20260826 FROM TB_R_NOTIFICATION;
SELECT * INTO TB_R_LOG_H_BACKUP_20260826 FROM TB_R_LOG_H;
SELECT * INTO TB_R_LOG_D_BACKUP_20260826 FROM TB_R_LOG_D;
SELECT * INTO TB_M_EXTERNAL_DOCUMENT_BACKUP_20260826 FROM TB_M_EXTERNAL_DOCUMENT;
SELECT * INTO TB_R_COPY_REQUEST_H_BACKUP_20260826 FROM TB_R_COPY_REQUEST_H;
SELECT * INTO TB_R_COPY_REQUEST_D_BACKUP_20260826 FROM TB_R_COPY_REQUEST_D;
SELECT * INTO TB_R_COPY_REQUEST_PRINT_LOG_BACKUP_20260826 FROM TB_R_COPY_REQUEST_PRINT_LOG;
SELECT * INTO TB_R_DOCUMENT_RELATED_DIVISION_BACKUP_20260826 FROM TB_R_DOCUMENT_RELATED_DIVISION;
GO

-- ---------------------------------------------------------------------
-- Bagian 2: Kosongkan (child sebelum parent utk yang ada FK)
-- ---------------------------------------------------------------------

-- Copy Request: PRINT_LOG -> D -> H (D & H tetap "referenced" oleh FK
-- constraint-nya sendiri walau child-nya sudah kosong, jadi DELETE bukan
-- TRUNCATE)
TRUNCATE TABLE TB_R_COPY_REQUEST_PRINT_LOG;
DELETE FROM TB_R_COPY_REQUEST_D;
DBCC CHECKIDENT ('TB_R_COPY_REQUEST_D', RESEED, 0);
DELETE FROM TB_R_COPY_REQUEST_H;
DBCC CHECKIDENT ('TB_R_COPY_REQUEST_H', RESEED, 0);

-- Divisi Terkait - referensi ke TB_R_DOCUMENT, jadi harus kosong SEBELUM
-- TB_R_DOCUMENT sendiri dikosongkan di bawah
TRUNCATE TABLE TB_R_DOCUMENT_RELATED_DIVISION;

-- Document Submission Form: D -> H (H tetap "referenced" oleh FK-nya)
TRUNCATE TABLE TB_R_DOC_SUBMISSION_FORM_D;
DELETE FROM TB_R_DOC_SUBMISSION_FORM_H;
DBCC CHECKIDENT ('TB_R_DOC_SUBMISSION_FORM_H', RESEED, 0);

TRUNCATE TABLE TB_R_LOG_D;
TRUNCATE TABLE TB_R_LOG_H;
TRUNCATE TABLE TB_R_APPROVAL_D;
TRUNCATE TABLE TB_R_APPROVAL_H;
TRUNCATE TABLE TB_R_APPROVAL_REASSIGN_LOG;
TRUNCATE TABLE TB_R_DOCUMENT_DISTRIBUTION;
TRUNCATE TABLE TB_R_PUBLISH_HISTORY;
TRUNCATE TABLE TB_R_DOCUMENT_HISTORY;
TRUNCATE TABLE TB_R_DOCUMENT_LOG;
TRUNCATE TABLE TB_R_CTRL_DOCUMENT;

-- TB_R_DOCUMENT sekarang "referenced" oleh FK_DOC_RELATED_DIVISION_DOCUMENT
-- (constraint baru sejak 2026-08-20) - TRUNCATE akan ditolak walau
-- TB_R_DOCUMENT_RELATED_DIVISION sudah dikosongkan di atas, jadi pakai
-- DELETE + CHECKIDENT seperti tabel "referenced" lainnya.
DELETE FROM TB_R_DOCUMENT;
DBCC CHECKIDENT ('TB_R_DOCUMENT', RESEED, 0);

TRUNCATE TABLE TB_R_NOTIFICATION;
TRUNCATE TABLE TB_M_EXTERNAL_DOCUMENT;
GO

-- ---------------------------------------------------------------------
-- Bagian 3: Reset nomor urut dokumen (TB_M_SEQUENCE) balik ke 0
-- ---------------------------------------------------------------------
UPDATE TB_M_SEQUENCE
SET SEQ_NO = 0,
	CHANGED_BY = 'dms.admin',
	CHANGED_DT = GETDATE()
WHERE SEQ_NO <> 0;
GO
