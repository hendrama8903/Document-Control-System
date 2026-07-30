# Stored Procedures — Dropped (Archive)

File di folder ini **sudah tidak ada di database** — disimpan cuma untuk
referensi historis, bukan bagian dari snapshot kondisi terkini (beda
dari folder induk `SQL/StoredProcedures/`).

## sp_Workflow_* (dropped 2026-07-30)

7 SP ini (`Create`, `Delete`, `GetByCode`, `GetByName`, `InsertDetail`,
`InsertHeader`, `Search`) dulu dipakai `WorkflowRepo.cs` /
`WorkflowController.cs` — modul "Workflow Master" yang ternyata sudah
lama tidak terdaftar di menu manapun (tidak bisa diakses siapa pun) dan
sudah digantikan total oleh `WorkflowDoc` ("Approval Workflow Setup",
`sp_Workflow_Doc*` / `sp_WorkflowDoc_Create` — itu yang masih aktif
dipakai untuk routing approval, JANGAN disamakan dengan grup ini).

Kode C# terkait (`WorkflowController.cs`, `Views/Workflow/*`,
`WorkflowRepo.cs`, model `Workflow.cs`) sudah dihapus dari repo di
commit yang sama. Lihat `SQL/WorkflowController_RemoveDeadCode.sql`
untuk detail lengkap alasan & scope pembersihannya.
