-- =====================================================================
-- Maintenance: reset semua data transaksi + nomor urut dokumen ke awal,
-- untuk mulai testing dari kondisi bersih (2026-08-12).
--
-- Sama seperti Maintenance_ResetTransactionData_20260810.sql, tapi
-- ditambah TB_R_COPY_REQUEST_H/_D (modul Controlled Copy Request, dibuat
-- setelah reset 2026-08-10 jadi belum ada di skrip lama).
--
-- Backup diambil dulu (SELECT INTO ..._BACKUP_20260812) sebelum
-- TRUNCATE/DELETE, supaya masih bisa dipulihkan kalau perlu. Backup
-- lama (_BACKUP_20260810 dst) TIDAK disentuh.
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
SELECT * INTO TB_R_DOCUMENT_BACKUP_20260812 FROM TB_R_DOCUMENT;
SELECT * INTO TB_R_CTRL_DOCUMENT_BACKUP_20260812 FROM TB_R_CTRL_DOCUMENT;
SELECT * INTO TB_R_DOCUMENT_DISTRIBUTION_BACKUP_20260812 FROM TB_R_DOCUMENT_DISTRIBUTION;
SELECT * INTO TB_R_DOCUMENT_LOG_BACKUP_20260812 FROM TB_R_DOCUMENT_LOG;
SELECT * INTO TB_R_DOCUMENT_HISTORY_BACKUP_20260812 FROM TB_R_DOCUMENT_HISTORY;
SELECT * INTO TB_R_PUBLISH_HISTORY_BACKUP_20260812 FROM TB_R_PUBLISH_HISTORY;
SELECT * INTO TB_R_APPROVAL_H_BACKUP_20260812 FROM TB_R_APPROVAL_H;
SELECT * INTO TB_R_APPROVAL_D_BACKUP_20260812 FROM TB_R_APPROVAL_D;
SELECT * INTO TB_R_APPROVAL_REASSIGN_LOG_BACKUP_20260812 FROM TB_R_APPROVAL_REASSIGN_LOG;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_H_BACKUP_20260812 FROM TB_R_DOC_SUBMISSION_FORM_H;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_D_BACKUP_20260812 FROM TB_R_DOC_SUBMISSION_FORM_D;
SELECT * INTO TB_R_COPY_REQUEST_H_BACKUP_20260812 FROM TB_R_COPY_REQUEST_H;
SELECT * INTO TB_R_COPY_REQUEST_D_BACKUP_20260812 FROM TB_R_COPY_REQUEST_D;
SELECT * INTO TB_R_NOTIFICATION_BACKUP_20260812 FROM TB_R_NOTIFICATION;
SELECT * INTO TB_R_LOG_H_BACKUP_20260812 FROM TB_R_LOG_H;
SELECT * INTO TB_R_LOG_D_BACKUP_20260812 FROM TB_R_LOG_D;
SELECT * INTO TB_M_EXTERNAL_DOCUMENT_BACKUP_20260812 FROM TB_M_EXTERNAL_DOCUMENT;
GO

-- ---------------------------------------------------------------------
-- Bagian 2: Truncate/Delete (child sebelum parent utk yang ada FK)
-- ---------------------------------------------------------------------
TRUNCATE TABLE TB_R_DOC_SUBMISSION_FORM_D;
DELETE FROM TB_R_DOC_SUBMISSION_FORM_H;
DBCC CHECKIDENT ('TB_R_DOC_SUBMISSION_FORM_H', RESEED, 0);

TRUNCATE TABLE TB_R_COPY_REQUEST_D;
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
