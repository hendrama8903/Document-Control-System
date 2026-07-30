-- =====================================================================
-- Penyesuaian penamaan menu & function mengikuti best practice sistem
-- DMS ISO 9001 (klausul 7.5 - Control of Documented Information).
--
-- 1) MENU_NAME: grup "Documents" (M00006) ternyata 4 dari 5 anaknya
-- labelnya TIDAK SINKRON dengan judul halaman (ViewData["Title"]) yang
-- sebenarnya tampil ke user, dan M00006-02 malah bocor nama class
-- internal ("P4DMaintenance") langsung ke menu. Diperbaiki (menu label
-- + kode ViewData["Title"] disamakan, kode sudah diubah terpisah):
--   M00006-02 "P4DMaintenance"    -> "Document Distribution Request"
--   M00006-04 "Document Control"  -> "Distribution Approval"
--   M00006-06 "User Dashboard"    -> "My Documents"
--
-- 2) FUNCTION_NAME: gaya penamaan tidak konsisten di seluruh 65 baris -
-- ada yang bare verb tanpa objek ("Add", "Edit", "Delete" - jelas dari
-- konteks group di UI Role Authorization, tapi ambigu kalau dibaca di
-- luar konteks itu, mis. di audit log), ada yang verb+objek ringkas
-- ("Add User"), ada yang mengulang nama menu penuh secara redundan
-- ("Add Position Master" padahal sudah di bawah menu "Position
-- Master"). Diseragamkan semua jadi pola "Verb + Object" ringkas tanpa
-- mengulang nama menu.
--
-- SENGAJA TIDAK disentuh: WORKFLOW-ADD/EDIT/DELETE (menunjuk MENU_ID
-- M00002-06 yang tidak ada di TB_M_MENU - controller WorkflowController
-- sudah lama tidak terdaftar di menu manapun / tidak bisa diakses siapa
-- pun, kemungkinan besar sudah digantikan WorkflowDocController /
-- "Approval Workflow Setup". Di luar scope penamaan, dilaporkan
-- terpisah, tidak dihapus/diubah di sini supaya tidak ada perubahan
-- fungsional yang tidak diminta).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

-- 1) Rename menu
UPDATE [dbo].[TB_M_MENU] SET MENU_NAME = 'Document Distribution Request' WHERE MENU_ID = 'M00006-02';
UPDATE [dbo].[TB_M_MENU] SET MENU_NAME = 'Distribution Approval' WHERE MENU_ID = 'M00006-04';
UPDATE [dbo].[TB_M_MENU] SET MENU_NAME = 'My Documents' WHERE MENU_ID = 'M00006-06';

-- 2) Rapikan FUNCTION_NAME - hanya baris yang benar-benar berubah
DECLARE @Renames TABLE (FUNCTION_ID VARCHAR(50), FUNCTION_NAME VARCHAR(255));

INSERT INTO @Renames (FUNCTION_ID, FUNCTION_NAME) VALUES
	('DOCUMENTMASTER-ADD', 'Add Category'),
	('DOCUMENTMASTER-DELETE', 'Delete Category'),
	('DOCUMENTMASTER-DELETE-FILEPATH', 'Delete Template'),
	('DOCUMENTMASTER-DOWNLOAD-FILEPATH', 'Download Template'),
	('DOCUMENTMASTER-EDIT', 'Edit Category'),

	('DEPARTMENTMASTER-ADD', 'Add Department'),
	('DEPARTMENTMASTER-DELETE', 'Delete Department'),
	('DEPARTMENTMASTER-EDIT', 'Edit Department'),

	('SECTIONMASTER-ADD', 'Add Section'),
	('SECTIONMASTER-DELETE', 'Delete Section'),
	('SECTIONMASTER-EDIT', 'Edit Section'),

	('SYSTEMMASTER-ADD', 'Add Parameter'),
	('SYSTEMMASTER-DELETE', 'Delete Parameter'),
	('SYSTEMMASTER-EDIT', 'Edit Parameter'),

	('WORKFLOWDOC-ADD', 'Add Workflow'),
	('WORKFLOWDOC-DELETE', 'Delete Workflow'),
	('WORKFLOWDOC-EDIT', 'Edit Workflow'),

	('DIVISIONMASTER-ADD', 'Add Division'),
	('DIVISIONMASTER-DELETE', 'Delete Division'),
	('DIVISIONMASTER-EDIT', 'Edit Division'),

	('MSEQUENCE-EDIT', 'Edit Sequence'),
	('MSEQUENCE-RESET', 'Reset Sequence'),

	('POSITIONMASTER-ADD', 'Add Position'),
	('POSITIONMASTER-DELETE', 'Delete Position'),
	('POSITIONMASTER-EDIT', 'Edit Position'),

	('USER-RESTORE', 'Restore User'),

	('ROLE-AUTHORIZATION', 'Set Authorization'),

	('DOCUMENT-ADD', 'Add Document'),
	('DOCUMENT-APPROVE', 'Approve Document'),
	('DOCUMENT-DELETE', 'Delete Document'),
	('DOCUMENT-DOWNLOAD', 'Download Document'),
	('DOCUMENT-EDIT', 'Edit Document'),

	('P4D-ADD', 'Add Distribution Request'),
	('P4D-DELETE', 'Delete Distribution Request'),
	('P4D-DISTRIBUTION', 'Manage Distribution'),
	('P4D-DOWNLOAD', 'Download Document'),
	('P4D-EDIT', 'Edit Distribution Request'),
	('P4D-RECEIVE', 'Receive Document'),
	('P4D-REJECT', 'Reject Document'),
	('P4D-SEND', 'Send Document'),

	('DOCUMENTCONTROLDASHBOARD-SEND', 'Send Document'),

	('LEGACYDOCUMENTIMPORT-IMPORT', 'Import Document'),

	('USERDASHBOARD-DOWNLOAD', 'Download Document'),
	('USERDASHBOARD-REQUEST', 'Request Document'),

	('EXTERNALDOCUMENT-ADD', 'Add Document'),
	('EXTERNALDOCUMENT-DELETE', 'Delete Document'),
	('EXTERNALDOCUMENT-EDIT', 'Edit Document');

UPDATE f
SET f.FUNCTION_NAME = r.FUNCTION_NAME,
    f.CHANGED_BY = 'dms.admin',
    f.CHANGED_DT = GETDATE()
FROM [dbo].[TB_M_FUNCTION] f
JOIN @Renames r ON r.FUNCTION_ID = f.FUNCTION_ID;
