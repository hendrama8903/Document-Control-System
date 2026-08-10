-- =====================================================================
-- Maintenance: reset semua data transaksi + nomor urut dokumen ke awal,
-- untuk mulai testing/UAT dari kondisi bersih (2026-08-10).
--
-- Backup diambil dulu (SELECT INTO ..._BACKUP_20260810) sebelum
-- TRUNCATE, supaya masih bisa dipulihkan kalau ternyata perlu.
-- TB_R_APPROVAL_D_BACKUP_20260720 (backup lama) TIDAK disentuh.
--
-- Urutan TRUNCATE memperhatikan FK: TB_R_DOC_SUBMISSION_FORM_D sebelum
-- _H (FK_DSF_D_SUBMISSION). Selain itu antar tabel TB_R_ tidak ada FK
-- constraint (cuma referensi longgar lewat DOCUMENT_CODE/ID) - dicek
-- lewat sys.foreign_keys sebelum jalan.
--
-- TB_M_EXTERNAL_DOCUMENT ikut dibersihkan meski prefix "TB_M_" - isinya
-- record dokumen eksternal (transaksional), bukan data master/lookup.
--
-- Semua tabel di sini pakai IDENTITY, jadi TRUNCATE otomatis reset
-- counter ID-nya balik ke awal juga.
--
-- Skrip sekali-jalan - jangan dijalankan ulang tanpa sadar.
-- Jalankan di database DMS_NEW
-- =====================================================================

-- ---------------------------------------------------------------------
-- Bagian 1: Backup
-- ---------------------------------------------------------------------
SELECT * INTO TB_R_DOCUMENT_BACKUP_20260810 FROM TB_R_DOCUMENT;
SELECT * INTO TB_R_CTRL_DOCUMENT_BACKUP_20260810 FROM TB_R_CTRL_DOCUMENT;
SELECT * INTO TB_R_DOCUMENT_DISTRIBUTION_BACKUP_20260810 FROM TB_R_DOCUMENT_DISTRIBUTION;
SELECT * INTO TB_R_DOCUMENT_LOG_BACKUP_20260810 FROM TB_R_DOCUMENT_LOG;
SELECT * INTO TB_R_DOCUMENT_HISTORY_BACKUP_20260810 FROM TB_R_DOCUMENT_HISTORY;
SELECT * INTO TB_R_PUBLISH_HISTORY_BACKUP_20260810 FROM TB_R_PUBLISH_HISTORY;
SELECT * INTO TB_R_APPROVAL_H_BACKUP_20260810 FROM TB_R_APPROVAL_H;
SELECT * INTO TB_R_APPROVAL_D_BACKUP_20260810 FROM TB_R_APPROVAL_D;
SELECT * INTO TB_R_APPROVAL_REASSIGN_LOG_BACKUP_20260810 FROM TB_R_APPROVAL_REASSIGN_LOG;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_H_BACKUP_20260810 FROM TB_R_DOC_SUBMISSION_FORM_H;
SELECT * INTO TB_R_DOC_SUBMISSION_FORM_D_BACKUP_20260810 FROM TB_R_DOC_SUBMISSION_FORM_D;
SELECT * INTO TB_R_NOTIFICATION_BACKUP_20260810 FROM TB_R_NOTIFICATION;
SELECT * INTO TB_R_LOG_H_BACKUP_20260810 FROM TB_R_LOG_H;
SELECT * INTO TB_R_LOG_D_BACKUP_20260810 FROM TB_R_LOG_D;
SELECT * INTO TB_M_EXTERNAL_DOCUMENT_BACKUP_20260810 FROM TB_M_EXTERNAL_DOCUMENT;
GO

-- ---------------------------------------------------------------------
-- Bagian 2: Truncate (child sebelum parent utk yang ada FK)
--
-- Catatan: TB_R_DOC_SUBMISSION_FORM_H TIDAK BISA di-TRUNCATE meski
-- child-nya (_D) sudah kosong - SQL Server menolak TRUNCATE pada tabel
-- manapun yang masih PUNYA FK constraint mengarah ke dirinya (FK
-- constraint-nya sendiri, bukan isi datanya, yang dicek). Makanya pakai
-- DELETE + DBCC CHECKIDENT manual untuk tabel itu saja.
-- ---------------------------------------------------------------------
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
-- Bagian 3: Reset nomor urut dokumen (TB_M_SEQUENCE) balik ke 0
-- ---------------------------------------------------------------------
UPDATE TB_M_SEQUENCE
SET SEQ_NO = 0,
	CHANGED_BY = 'dms.admin',
	CHANGED_DT = GETDATE()
WHERE SEQ_NO <> 0;
GO
