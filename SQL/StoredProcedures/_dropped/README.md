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

## sp_CopyRequest_Accept (dropped 2026-08-15)

Dulu dipanggil requester untuk konfirmasi sudah menerima copy fisik
dokumen setelah QMS Approve (`ACCEPTED_FLAG` di `TB_R_COPY_REQUEST_H`,
sekarang juga sudah di-drop). Jadi tidak relevan sejak requester
mencetak sendiri dokumennya lewat PrintTrack begitu status Approved -
tidak ada lagi pihak ketiga yang menyerahkan fisik untuk dikonfirmasi.
Kode C# terkait (`CopyRequestController.Accept`,
`CopyRequestRepo.Accept`, kolom `ACCEPTED_*` di model `CopyRequest.cs`,
kolom Acceptance & tombol Accepted di `Views/CopyRequest/*`) sudah
dihapus di commit yang sama. Lihat
`SQL/CopyRequest_RemoveAcceptStep.sql` untuk detail lengkap.

## sp_DocumentControlDashboard_Search, sp_DocumentControlDashboard_SendDocument (dropped 2026-08-16)

DocumentControlDashboard resmi ditetapkan sebagai menu overview
read-only (master register semua dokumen teregister P4D + status +
history). `sp_DocumentControlDashboard_Search` sudah tidak pernah
dipanggil dari kode C# manapun sejak `DocumentControlDashboardRepo.Search()`
dialihkan (2026-08-11) memanggil `sp_P4DMaintenance_Search` langsung.
`sp_DocumentControlDashboard_SendDocument` juga yatim - fitur "Send"
tidak pernah punya tombol di UI, tergantikan oleh alur approval
berjenjang di Document Maintenance. Kode C# terkait (action
`GetByKey`, `AddEditAsync`, `GetDepartmentCode`, `GetDocumentCode`,
`GetDataByDocumentNo`, `GetDepartmentByDivision`,
`GetDocumentByDepartment`, `SendDocument`, `SendApproveRejectEmail` di
`DocumentControlDashboardController.cs`; method-method serupa di
`DocumentControlDashboardRepo.cs`) sudah dihapus di commit yang sama.
Lihat `SQL/DocumentControlDashboard_RemoveDeadCode.sql` untuk detail
lengkap.
