-- =====================================================================
-- Tindak lanjut temuan: WorkflowController (/Workflow/Index) tidak
-- pernah terdaftar di TB_M_MENU sama sekali, jadi halamannya sudah lama
-- tidak bisa diakses role manapun. Dikonfirmasi 100% kode mati - sudah
-- digantikan total oleh WorkflowDoc/TB_M_WORKFLOW_DOC_H/_D ("Approval
-- Workflow Setup", dipakai nyata oleh sp_WorkflowDoc_Create untuk
-- routing approval):
--   - Dropdown "Workflow" di form Department Master & Division Master
--     sudah lama di-comment out, tidak aktif
--   - DocumentMaintenanceController deklarasi field workflowRepo tapi
--     tidak pernah dipakai
--   - WorkflowDocController punya method GetWorkflowName/GetByName
--     duplikat yang juga tidak dipanggil dari view manapun
--   - TB_M_AUTH_FUNCTION tidak ada satu pun grant untuk WORKFLOW-*
--     (tidak pernah ke-assign ke role manapun, konsisten dengan
--     menu yang tidak pernah reachable)
--
-- Sudah dihapus dari kode (terpisah): WorkflowController.cs,
-- Views/Workflow/*, WorkflowRepo.cs, model Workflow.cs, DbSet<Workflow>
-- di DBContext, referensi mati di DepartmentMaster/DivisionMaster/
-- DocumentMaintenance/WorkflowDocController.
--
-- Script ini membersihkan sisi database: 3 Function permission
-- WORKFLOW-ADD/EDIT/DELETE (menunjuk MENU_ID M00002-06 yang memang
-- tidak pernah ada di TB_M_MENU).
--
-- SENGAJA TIDAK dihapus: tabel TB_M_WORKFLOW_H/_D (cuma 8+14 baris data
-- lama, dibiarkan sebagai arsip - keputusan hapus tabel/data histori
-- perlu persetujuan terpisah, di luar scope pembersihan kode mati ini).
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

DELETE FROM [dbo].[TB_M_AUTH_FUNCTION] WHERE FUNCTION_ID IN ('WORKFLOW-ADD', 'WORKFLOW-EDIT', 'WORKFLOW-DELETE');
DELETE FROM [dbo].[TB_M_FUNCTION] WHERE FUNCTION_ID IN ('WORKFLOW-ADD', 'WORKFLOW-EDIT', 'WORKFLOW-DELETE');

-- 7 SP "Workflow" mentah (bukan "WorkflowDoc") juga yatim - tidak ada kode
-- C# yang manggil lagi setelah WorkflowRepo.cs dihapus. Definisi lengkapnya
-- diarsipkan di SQL/StoredProcedures/_dropped/ untuk referensi historis.
DROP PROCEDURE IF EXISTS [dbo].[sp_Workflow_Create];
DROP PROCEDURE IF EXISTS [dbo].[sp_Workflow_Delete];
DROP PROCEDURE IF EXISTS [dbo].[sp_Workflow_GetByCode];
DROP PROCEDURE IF EXISTS [dbo].[sp_Workflow_GetByName];
DROP PROCEDURE IF EXISTS [dbo].[sp_Workflow_InsertDetail];
DROP PROCEDURE IF EXISTS [dbo].[sp_Workflow_InsertHeader];
DROP PROCEDURE IF EXISTS [dbo].[sp_Workflow_Search];
