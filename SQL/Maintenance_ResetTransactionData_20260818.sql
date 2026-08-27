-- =====================================================================
-- Maintenance: reset semua data transaksi + nomor urut dokumen/CCR ke
-- awal, untuk mulai testing/UAT dari kondisi bersih (2026-08-18).
--
-- Versi lanjutan dari Maintenance_ResetTransactionData_20260810.sql -
-- ditambah modul Copy Request/Print Request (TB_R_COPY_REQUEST_H/D,
-- TB_R_COPY_REQUEST_PRINT_LOG) yang belum ada waktu script lama dibuat.
--
-- Backup diambil dulu (SELECT INTO ..._BACKUP_20260818) sebelum
-- TRUNCATE/DELETE, supaya masih bisa dipulihkan kalau ternyata perlu.
-- Tabel _BACKUP_* lama (20260810, 20260816, dst) TIDAK disentuh.
--
-- Urutan memperhatikan FK (lihat sys.foreign_keys):
--   FK_DSF_D_SUBMISSION: TB_R_DOC_SUBMISSION_FORM_D -> _H
--   FK_CPR_D_REQUEST: TB_R_COPY_REQUEST_D -> TB_R_COPY_REQUEST_H
--   FK_CPR_PRINTLOG_REQUEST_DETAIL: TB_R_COPY_REQUEST_PRINT_LOG -> TB_R_COPY_REQUEST_D
-- SQL Server menolak TRUNCATE pada tabel manapun yang masih PUNYA FK
-- constraint mengarah ke dirinya (constraint-nya, bukan isi datanya,
-- yang dicek) - jadi TB_R_COPY_REQUEST_H dan _D (sama-sama masih jadi
-- parent) pakai DELETE + DBCC CHECKIDENT manual, cuma PRINT_LOG (leaf)
-- yang bisa TRUNCATE langsung. Selain itu antar tabel TB_R_ tidak ada
-- FK constraint (cuma referensi longgar lewat DOCUMENT_CODE/ID).
--
-- TB_M_EXTERNAL_DOCUMENT ikut dibersihkan meski prefix "TB_M_" - isinya
-- record dokumen eksternal (transaksional), bukan data master/lookup.
--
-- Skrip sekali-jalan - jangan dijalankan ulang tanpa sadar.
-- Jalankan di database DMS_NEW
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- ---------------------------------------------------------------------
-- Bagian 1: Backup
-- ---------------------------------------------------------------------
SELECT * INTO TB_R_DOCUMENT_BACKUP_20260818 FROM TB_R_DOCUMENT;
SELECT * INTO TB_R_CTRL_DOCUMENT_BACKUP_20260818 FROM TB_R_CTRL_DOCUMENT;
SELECT * INTO TB_R_DOCUMENT_DISTRIBUTION_BACKUP_20260818 FROM TB_R_DOCUMENT_DISTRIBUTION;
SELECT * INTO TB_R_DOCUMENT_LOG_BACKUP_20260818 FROM TB_R_DOCUMENT_LOG;
SELECT * INTO TB_R_DOCUMENT_HISTORY_BACKUP_20260818 FROM TB_R_DOCUMENT_HISTORY;
SELECT * INTO TB_R_PUBLISH_HISTORY_BACKUP_20260818 FROM TB_R_PUBLISH_HISTORY;
SELECT * INTO TB_R_APPROVAL_H_BACKUP_20260818 FROM TB_R_APPROVAL_H;
SELECT * INTO TB_R_APPROVAL_D_BACKUP_20260818 FROM TB_R_APPROVAL_D;
SELECT * INTO TB_R_APPROVAL_REASSIGN_LOG_BACKUP_20260818 FROM TB_R_APPROVAL_REASSIGN_LOG;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_H_BACKUP_20260818 FROM TB_R_DOC_SUBMISSION_FORM_H;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_D_BACKUP_20260818 FROM TB_R_DOC_SUBMISSION_FORM_D;
SELECT * INTO TB_R_COPY_REQUEST_H_BACKUP_20260818 FROM TB_R_COPY_REQUEST_H;
SELECT * INTO TB_R_COPY_REQUEST_D_BACKUP_20260818 FROM TB_R_COPY_REQUEST_D;
SELECT * INTO TB_R_COPY_REQUEST_PRINT_LOG_BACKUP_20260818 FROM TB_R_COPY_REQUEST_PRINT_LOG;
SELECT * INTO TB_R_NOTIFICATION_BACKUP_20260818 FROM TB_R_NOTIFICATION;
SELECT * INTO TB_R_LOG_H_BACKUP_20260818 FROM TB_R_LOG_H;
SELECT * INTO TB_R_LOG_D_BACKUP_20260818 FROM TB_R_LOG_D;
SELECT * INTO TB_M_EXTERNAL_DOCUMENT_BACKUP_20260818 FROM TB_M_EXTERNAL_DOCUMENT;
GO

-- ---------------------------------------------------------------------
-- Bagian 2: Truncate/Delete (child sebelum parent utk yang ada FK)
-- ---------------------------------------------------------------------
TRUNCATE TABLE TB_R_COPY_REQUEST_PRINT_LOG;

DELETE FROM TB_R_COPY_REQUEST_D;
DBCC CHECKIDENT ('TB_R_COPY_REQUEST_D', RESEED, 0);

DELETE FROM TB_R_COPY_REQUEST_H;
DBCC CHECKIDENT ('TB_R_COPY_REQUEST_H', RESEED, 0);

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
TRUNCATE TABLE TB_R_DOCUMENT;
TRUNCATE TABLE TB_R_NOTIFICATION;
TRUNCATE TABLE TB_M_EXTERNAL_DOCUMENT;
GO

-- ---------------------------------------------------------------------
-- Bagian 3: Reset nomor urut dokumen/CCR (TB_M_SEQUENCE) balik ke 0
-- ---------------------------------------------------------------------
UPDATE TB_M_SEQUENCE
SET SEQ_NO = 0,
	CHANGED_BY = 'dms.admin',
	CHANGED_DT = GETDATE()
WHERE SEQ_NO <> 0;
GO
