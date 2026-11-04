-- =============================================
-- Redesign email "Document Approve/Reject Result" (2026-11-04)
--
-- Email ini sekarang dikirim ke pengusul di SETIAP tahap approval (bukan cuma
-- final), lihat DocumentMaintenanceController.SendApproveRejectEmail.
-- Format kartu "Document Information" sama dengan Approval Request.
--
-- Placeholder (diisi di controller):
--   {SUBJECT_STATUS}  : "Document Approved (Step 1 of 3)" / "Document Approved" / "Document Rejected"
--   {HEADING}         : "Approval Progress" / "Document Approved" / "Document Rejected"
--   {MESSAGE}         : kalimat pembuka sesuai hasil (sudah HTML)
--   {ACTION_LABEL}    : "Approved By" / "Rejected By" / "Acknowledged By"
--   {ACTION_BY}       : nama + posisi approver yang memproses
--   {STEP_INFO}       : "Step 1 of 3" / "Completed"
--   {NEXT_APPROVER}   : approver berikutnya, atau "-"
--   {REMARK}          : catatan approver, atau "-"
--   {STATUS}, {STATUS_COLOR} : status dokumen + warnanya
--   {APPROVAL_FLOW}   : kartu daftar semua tahap approval + statusnya (HTML, dibangun
--                       di controller BuildApprovalFlowHtml)
--   {FULL_NAME}, {DOCUMENT_CODE}, {DOCUMENT_NAME}, {DOCUMENT_TYPE}, {DOCUMENT_CATEGORY},
--   {CLASSIFICATION}, {REVISION}, {DOCUMENT_DATE}
-- =============================================

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'{SUBJECT_STATUS} - {DOCUMENT_CODE}'
WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Document Approval Update'
WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';

-- CAST ke NVARCHAR(MAX) di literal pertama: gabungan literal N'' biasa dibatasi
-- 4000 karakter dan body ini lebih panjang dari itu (sempat terpotong).
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
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Document Category</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DOCUMENT_CATEGORY}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Document Classification</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{CLASSIFICATION}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Revision</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{REVISION}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">{ACTION_LABEL}</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{ACTION_BY}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Approval Step</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{STEP_INFO}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Next Approver</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{NEXT_APPROVER}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Document Date</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DOCUMENT_DATE}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td colspan="2" valign="top" style="padding:14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Remark</div><div style="font-size:15px;line-height:20px;color:#1F2A37;padding-top:2px">{REMARK}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td valign="middle" style="padding:14px 0;font-size:15px;line-height:20px;color:#6B7280">Approval Status</td>'
    +     N'<td align="right" valign="middle" style="padding:14px 0;font-size:15px;line-height:20px;font-weight:600;color:{STATUS_COLOR}">{STATUS}</td>'
    +   N'</tr>'
    +   N'</table>'
    + N'</td></tr>'
    + N'</table>'
    + N'{APPROVAL_FLOW}'
    + N'<p style="margin:16px 0 0 0">You can view the document and its approval history through the system.</p>'
WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';

-- =============================================
-- ROLLBACK (nilai sebelum 2026-11-04)
-- Catatan: controller lama mengisi {JUDGEMENT} = DITERIMA/DITOLAK; kalau rollback
-- template, rollback juga SendApproveRejectEmail di controller.
-- =============================================
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Keputusan Persetujuan Dokumen {DOCUMENT_CODE}'
--     WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Keputusan Persetujuan Dokumen'
--     WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Kepada Yth.<br>{FULL_NAME}<br><br><br>Dokumen yang sudah Anda inputkan sebelumnya telah dinyatakan <strong>{JUDGEMENT}</strong>, detail dokumen:<br><br><strong>&emsp;Kode Dokumen : {DOCUMENT_CODE}<br>&emsp;Nama Dokumen : {DOCUMENT_NAME}<br>&emsp;Tanggal Dokumen : {DOCUMENT_DATE}<br>&emsp;Revisi : {REVISION}<br>&emsp;Alasan : {REMARK}</strong><br><br>Klik tombol dibawah untuk melihat dokumen yang disebutkan diatas.'
--     WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVE_REJECT_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';
