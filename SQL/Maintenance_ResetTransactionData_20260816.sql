-- =====================================================================
-- Maintenance: reset semua data transaksi + nomor urut dokumen ke awal,
-- untuk mulai testing dari kondisi bersih (2026-08-16).
--
-- Sama seperti Maintenance_ResetTransactionData_20260812.sql, tapi
-- ditambah TB_R_COPY_REQUEST_PRINT_LOG (modul print tracking PrintTrack,
-- dibuat setelah reset 2026-08-12 jadi belum ada di skrip lama). Tabel ini
-- sekarang punya FK ke TB_R_COPY_REQUEST_D (FK_CPR_PRINTLOG_REQUEST_DETAIL)
-- - tanpa di-truncate LEBIH DULU, TRUNCATE TB_R_COPY_REQUEST_D di skrip
-- lama akan gagal (SQL Server menolak TRUNCATE tabel yang masih direferensi
-- FK dari tabel lain, walau ON DELETE CASCADE).
--
-- Backup diambil dulu (SELECT INTO ..._BACKUP_20260816) sebelum
-- TRUNCATE/DELETE, supaya masih bisa dipulihkan kalau perlu. Backup
-- lama (_BACKUP_20260810, _BACKUP_20260812) TIDAK disentuh.
--
-- TB_R_DOC_SUBMISSION_FORM_H dan TB_R_COPY_REQUEST_H TIDAK BISA
-- di-TRUNCATE meski child-nya (_D) sudah kosong - SQL Server menolak
-- TRUNCATE pada tabel manapun yang masih PUNYA FK constraint mengarah ke
-- dirinya (FK_DSF_D_SUBMISSION, FK_CPR_D_REQUEST) - makanya pakai
-- DELETE + DBCC CHECKIDENT manual untuk dua tabel itu.
--
-- Skrip sekali-jalan - jangan dijalankan ulang tanpa sadar.
-- Jalankan di database DMS_NEW
-- =====================================================================

-- ---------------------------------------------------------------------
-- Bagian 1: Backup
-- ---------------------------------------------------------------------
SELECT * INTO TB_R_DOCUMENT_BACKUP_20260816 FROM TB_R_DOCUMENT;
SELECT * INTO TB_R_CTRL_DOCUMENT_BACKUP_20260816 FROM TB_R_CTRL_DOCUMENT;
SELECT * INTO TB_R_DOCUMENT_DISTRIBUTION_BACKUP_20260816 FROM TB_R_DOCUMENT_DISTRIBUTION;
SELECT * INTO TB_R_DOCUMENT_LOG_BACKUP_20260816 FROM TB_R_DOCUMENT_LOG;
SELECT * INTO TB_R_DOCUMENT_HISTORY_BACKUP_20260816 FROM TB_R_DOCUMENT_HISTORY;
SELECT * INTO TB_R_PUBLISH_HISTORY_BACKUP_20260816 FROM TB_R_PUBLISH_HISTORY;
SELECT * INTO TB_R_APPROVAL_H_BACKUP_20260816 FROM TB_R_APPROVAL_H;
SELECT * INTO TB_R_APPROVAL_D_BACKUP_20260816 FROM TB_R_APPROVAL_D;
SELECT * INTO TB_R_APPROVAL_REASSIGN_LOG_BACKUP_20260816 FROM TB_R_APPROVAL_REASSIGN_LOG;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_H_BACKUP_20260816 FROM TB_R_DOC_SUBMISSION_FORM_H;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_D_BACKUP_20260816 FROM TB_R_DOC_SUBMISSION_FORM_D;
SELECT * INTO TB_R_COPY_REQUEST_H_BACKUP_20260816 FROM TB_R_COPY_REQUEST_H;
SELECT * INTO TB_R_COPY_REQUEST_D_BACKUP_20260816 FROM TB_R_COPY_REQUEST_D;
SELECT * INTO TB_R_COPY_REQUEST_PRINT_LOG_BACKUP_20260816 FROM TB_R_COPY_REQUEST_PRINT_LOG;
SELECT * INTO TB_R_NOTIFICATION_BACKUP_20260816 FROM TB_R_NOTIFICATION;
SELECT * INTO TB_R_LOG_H_BACKUP_20260816 FROM TB_R_LOG_H;
SELECT * INTO TB_R_LOG_D_BACKUP_20260816 FROM TB_R_LOG_D;
SELECT * INTO TB_M_EXTERNAL_DOCUMENT_BACKUP_20260816 FROM TB_M_EXTERNAL_DOCUMENT;
GO

-- ---------------------------------------------------------------------
-- Bagian 2: Truncate/Delete (child sebelum parent utk yang ada FK)
-- ---------------------------------------------------------------------
-- Wajib eksplisit - sesi tanpa ini gagal dengan Msg 1934 saat DELETE FROM
-- TB_R_DOC_SUBMISSION_FORM_H (ketemu saat eksekusi 2026-08-16).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

TRUNCATE TABLE TB_R_DOC_SUBMISSION_FORM_D;
DELETE FROM TB_R_DOC_SUBMISSION_FORM_H;
DBCC CHECKIDENT ('TB_R_DOC_SUBMISSION_FORM_H', RESEED, 0);

-- TB_R_COPY_REQUEST_D SEKARANG JUGA tidak bisa di-TRUNCATE (baru ketahuan
-- saat eksekusi 2026-08-16) - FK_CPR_PRINTLOG_REQUEST_DETAIL tetap
-- memblokir TRUNCATE walau TB_R_COPY_REQUEST_PRINT_LOG sudah kosong duluan;
-- aturan SQL Server-nya "ada FK constraint mengarah ke tabel ini" bukan
-- "ada BARIS yang mengarah ke tabel ini". Pakai DELETE + CHECKIDENT, sama
-- seperti pola _H di bawahnya.
TRUNCATE TABLE TB_R_COPY_REQUEST_PRINT_LOG;
DELETE FROM TB_R_COPY_REQUEST_D;
DBCC CHECKIDENT ('TB_R_COPY_REQUEST_D', RESEED, 0);
DELETE FROM TB_R_COPY_REQUEST_H;
DBCC CHECKIDENT ('TB_R_COPY_REQUEST_H', RESEED, 0);

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
TRUNCATE TABLE TB_R_DOCUMENT;
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
