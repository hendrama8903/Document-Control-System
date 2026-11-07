-- =============================================
-- Redesign email P4D / Document Registration (2026-11-05)
--
-- 1. P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE (ke tim Document Control/QAD saat P4D dibuat)
--    Format kartu "Document Information" seperti email Document Preparation.
--    Placeholder (P4DMaintenanceController.SendApprovalEmailAsync):
--      {DOCUMENT_CODE} {DOCUMENT_NAME} {DOCUMENT_TYPE} {CLASSIFICATION} {REVISION}
--      {DOCUMENT_DATE} {CREATOR} {DIVISION} {DEPARTMENT}
--
-- 2. P4D_APPROVE_REJECT_EMAIL_TEMPLATE (ke pendaftar P4D di tiap tahap QAD:
--    Receive, Reject, Send Distribution) - sekarang email progres, bukan cuma
--    hasil reject/terima. Placeholder (P4DMaintenanceController.SendP4DStatusEmail):
--      {SUBJECT_STATUS} {HEADING} {MESSAGE} {FULL_NAME} {DOCUMENT_CODE} {DOCUMENT_NAME}
--      {DOCUMENT_TYPE} {CLASSIFICATION} {DEPARTMENT} {REVISION} {DOCUMENT_DATE}
--      {ACTION_LABEL} {ACTION_BY} {NEXT_STEP} {REMARK} {STATUS} {STATUS_COLOR}
--      {P4D_FLOW} (kartu tahapan registrasi, dibangun di BuildP4DFlowHtml)
--
-- 3. P4D_APPROVE_REJECT_NOTIFICATION.TEXT - perbaiki tag <strong> yang tidak ditutup.
--
-- Frame (header/tombol/footer) ada di wwwroot/Document/EmailTemplate/EmailTemplate.html.
-- CAST NVARCHAR(MAX) di literal pertama: gabungan N'' biasa terpotong di 4000 karakter.
-- =============================================

-- ---------- 1. Approval request ke Document Control ----------
UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Document Registration Request - {DOCUMENT_CODE}'
WHERE SYSTEM_TYPE = 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Document Registration Request'
WHERE SYSTEM_TYPE = 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = CAST(N'<div style="font-size:24px;line-height:32px;font-weight:600;color:#1F2A37;padding:4px 0 8px 0">Registration Request</div>' AS NVARCHAR(MAX))
    + N'<p style="margin:0 0 12px 0">Dear Document Control Team,</p>'
    + N'<p style="margin:0 0 16px 0">A new document has been registered by <strong>{CREATOR}</strong> and is waiting to be received and checked.</p>'
    + N'<table width="100%" cellspacing="0" cellpadding="0" border="0" role="presentation" style="border-collapse:separate;border:1px solid #E3E7ED;border-radius:10px">'
    + N'<tr><td bgcolor="#F7F9FB" style="background-color:#F7F9FB;padding:16px 16px 0 16px;border-radius:10px 10px 0 0">'
    +   N'<div style="font-size:12px;line-height:16px;font-weight:600;letter-spacing:0.3px;color:#4B5563;padding-bottom:12px">&#9776;&nbsp; DOCUMENT INFORMATION</div>'
    +   N'<div style="font-size:11px;line-height:14px;letter-spacing:0.3px;color:#6B7280">DOCUMENT NAME</div>'
    +   N'<div style="font-size:20px;line-height:28px;font-weight:600;color:#1F2A37;padding:6px 0 14px 0;border-bottom:1px solid #E3E7ED">{DOCUMENT_NAME}</div>'
    +   N'<div style="font-size:11px;line-height:14px;letter-spacing:0.3px;color:#6B7280;padding-top:14px">DOCUMENT CODE</div>'
    +   N'<div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding:8px 0 16px 0">{DOCUMENT_CODE}</div>'
    + N'</td></tr>'
    + N'<tr><td style="padding:0 16px">'
    +   N'<table width="100%" cellspacing="0" cellpadding="0" border="0" role="presentation" style="border-collapse:collapse">'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Document Type</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DOCUMENT_TYPE}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Document Classification</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{CLASSIFICATION}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Division</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DIVISION}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Department</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DEPARTMENT}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Registered By</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{CREATOR}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Revision</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{REVISION}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td valign="middle" style="padding:14px 0;font-size:15px;line-height:20px;color:#6B7280">Document Date</td>'
    +     N'<td align="right" valign="middle" style="padding:14px 0;font-size:15px;line-height:20px;font-weight:600;color:#1F2A37">{DOCUMENT_DATE}</td>'
    +   N'</tr>'
    +   N'</table>'
    + N'</td></tr>'
    + N'</table>'
    + N'<p style="margin:16px 0 0 0">Please open the Document Registration menu to receive and check this document.</p>'
WHERE SYSTEM_TYPE = 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';

-- ---------- 2. Email progres ke pendaftar ----------
UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'{SUBJECT_STATUS} - {DOCUMENT_CODE}'
WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Document Registration Update'
WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = CAST(N'<div style="font-size:24px;line-height:32px;font-weight:600;color:#1F2A37;padding:4px 0 8px 0">{HEADING}</div>' AS NVARCHAR(MAX))
    + N'<p style="margin:0 0 12px 0">Dear {FULL_NAME},</p>'
    + N'<p style="margin:0 0 16px 0">{MESSAGE}</p>'
    + N'<table width="100%" cellspacing="0" cellpadding="0" border="0" role="presentation" style="border-collapse:separate;border:1px solid #E3E7ED;border-radius:10px">'
    + N'<tr><td bgcolor="#F7F9FB" style="background-color:#F7F9FB;padding:16px 16px 0 16px;border-radius:10px 10px 0 0">'
    +   N'<div style="font-size:12px;line-height:16px;font-weight:600;letter-spacing:0.3px;color:#4B5563;padding-bottom:12px">&#9776;&nbsp; DOCUMENT INFORMATION</div>'
    +   N'<div style="font-size:11px;line-height:14px;letter-spacing:0.3px;color:#6B7280">DOCUMENT NAME</div>'
    +   N'<div style="font-size:20px;line-height:28px;font-weight:600;color:#1F2A37;padding:6px 0 14px 0;border-bottom:1px solid #E3E7ED">{DOCUMENT_NAME}</div>'
    +   N'<div style="font-size:11px;line-height:14px;letter-spacing:0.3px;color:#6B7280;padding-top:14px">DOCUMENT CODE</div>'
    +   N'<div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding:8px 0 16px 0">{DOCUMENT_CODE}</div>'
    + N'</td></tr>'
    + N'<tr><td style="padding:0 16px">'
    +   N'<table width="100%" cellspacing="0" cellpadding="0" border="0" role="presentation" style="border-collapse:collapse">'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Document Type</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DOCUMENT_TYPE}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Department</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DEPARTMENT}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">{ACTION_LABEL}</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{ACTION_BY}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Next Step</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{NEXT_STEP}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td colspan="2" valign="top" style="padding:14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Remark</div><div style="font-size:15px;line-height:20px;color:#1F2A37;padding-top:2px">{REMARK}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td valign="middle" style="padding:14px 0;font-size:15px;line-height:20px;color:#6B7280">Registration Status</td>'
    +     N'<td align="right" valign="middle" style="padding:14px 0;font-size:15px;line-height:20px;font-weight:600;color:{STATUS_COLOR}">{STATUS}</td>'
    +   N'</tr>'
    +   N'</table>'
    + N'</td></tr>'
    + N'</table>'
    + N'{P4D_FLOW}'
    + N'<p style="margin:16px 0 0 0">You can view the registration status through the system.</p>'
WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';

-- ---------- 3. Notifikasi in-app ----------
UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Dokumen P4D {DOCUMENT_CODE} yang Anda daftarkan telah <strong>{JUDGEMENT}</strong>'
WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_NOTIFICATION' AND SYSTEM_CODE = 'TEXT';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Update Registrasi Dokumen P4D {DOCUMENT_CODE}'
WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_NOTIFICATION' AND SYSTEM_CODE = 'TITLE';

-- =============================================
-- ROLLBACK (nilai sebelum 2026-11-05)
-- Catatan: controller lama memakai SendApproveRejectEmail ({JUDGEMENT} di body);
-- kalau rollback template, rollback juga P4DMaintenanceController.
-- =============================================
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Permohonan Persetujuan Dokumen P4D {DOCUMENT_CODE}' WHERE SYSTEM_TYPE = 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Permohonan Persetujuan Dokumen P4D' WHERE SYSTEM_TYPE = 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Kepada seluruh anggota <strong>Document Control</strong>, ada dokumen P4D baru yang menunggu persetujuan dengan detail sebagai berikut:<br><br><strong>&emsp;Kode Dokumen : {DOCUMENT_CODE}<br>&emsp;Nama Dokumen : {DOCUMENT_NAME}<br>&emsp;Tanggal Dokumen : {DOCUMENT_DATE}<br>&emsp;Revisi : {REVISION}<br>&emsp;Pembuat : {CREATOR}<br>&emsp;Divisi : {DIVISION}<br>&emsp;Department : {DEPARTMENT}</strong><br><br>Klik tombol dibawah untuk melihat dokumen yang disebutkan diatas.' WHERE SYSTEM_TYPE = 'P4D_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Keputusan Persetujuan Dokumen P4D {DOCUMENT_CODE}' WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Keputusan Persetujuan Dokumen P4D' WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Kepada Yth.<br>{FULL_NAME}<br><br><br>Dokumen yang sudah Anda inputkan sebelumnya telah dinyatakan <strong>{JUDGEMENT}</strong>, detail dokumen:<br><br><strong>&emsp;Kode Dokumen : {DOCUMENT_CODE}<br>&emsp;Nama Dokumen : {DOCUMENT_NAME}<br>&emsp;Tanggal Dokumen : {DOCUMENT_DATE}<br>&emsp;Revisi : {REVISION}<br>&emsp;Alasan : {REMARK}</strong><br><br>Klik tombol dibawah untuk melihat dokumen yang disebutkan diatas.' WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Dokumen P4D yang sudah Anda inputkan sebelumnya telah dinyatakan <strong>{JUDGEMENT}<strong>' WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_NOTIFICATION' AND SYSTEM_CODE = 'TEXT';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Keputusan Persetujuan Dokumen P4D {DOCUMENT_CODE}' WHERE SYSTEM_TYPE = 'P4D_APPROVE_REJECT_NOTIFICATION' AND SYSTEM_CODE = 'TITLE';
