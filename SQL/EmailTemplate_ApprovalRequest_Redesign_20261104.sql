-- =============================================
-- Redesign email "Document Approval Request" (2026-11-04)
--
-- Body email dibuat berformat kartu "Document Information" sesuai mockup,
-- ditambah field baru:
--   {DOCUMENT_TYPE} : kategori dokumen (TB_M_DOCUMENT, mis. "PRO - Prosedur")
--   {PROPOSER}      : nama lengkap pengusul (DOCUMENT_CREATOR -> TB_M_USER.FULL_NAME)
-- Kedua placeholder diisi di DocumentMaintenanceController.SendApprovalEmailAsync
-- dan ReassignController (keduanya memakai template ini).
--
-- Frame (header/label/tombol/footer) ada di wwwroot/Document/EmailTemplate/EmailTemplate.html;
-- TITLE tampil sebagai label kecil di atas body.
-- =============================================

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Document Approval Request - {DOCUMENT_CODE}'
WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'Document Approval Request'
WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';

UPDATE TB_M_SYSTEM
SET SYSTEM_VALUE = N'<div style="font-size:24px;line-height:32px;font-weight:600;color:#1F2A37;padding:4px 0 8px 0">Approval Request</div>'
    + N'<p style="margin:0 0 12px 0">Dear {FULL_NAME},</p>'
    + N'<p style="margin:0 0 16px 0">A document has been submitted and requires your review and approval.</p>'
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
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Revision</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{REVISION}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 8px 14px 0;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Document Date</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{DOCUMENT_DATE}</div></td>'
    +     N'<td class="dcs-col" width="50%" valign="top" style="padding:14px 0 14px 8px;border-bottom:1px solid #E3E7ED"><div style="font-size:12px;line-height:16px;color:#6B7280">Proposed By</div><div style="font-size:15px;line-height:20px;font-weight:600;color:#1F2A37;padding-top:2px">{PROPOSER}</div></td>'
    +   N'</tr>'
    +   N'<tr>'
    +     N'<td valign="middle" style="padding:12px 0;font-size:12px;line-height:16px;color:#6B7280">Approval Status</td>'
    +     N'<td align="right" valign="middle" style="padding:12px 0"><span style="display:inline-block;padding:3px 10px;border-radius:12px;background-color:#FDE8DA;color:#C2410C;font-size:12px;line-height:16px;font-weight:600">Pending Approval</span></td>'
    +   N'</tr>'
    +   N'</table>'
    + N'</td></tr>'
    + N'</table>'
    + N'<p style="margin:16px 0 0 0">Please review the document and submit your approval decision through the system.</p>'
WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';

-- =============================================
-- ROLLBACK (nilai sebelum 2026-11-04)
-- =============================================
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Permohonan Persetujuan Dokumen {DOCUMENT_CODE}'
--     WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'SUBJECT';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Permohonan Persetujuan Dokumen'
--     WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'TITLE';
-- UPDATE TB_M_SYSTEM SET SYSTEM_VALUE = N'Kepada Yth.<br>{FULL_NAME}<br><br><br>Mohon kesediaan Anda untuk memberikan persetujuan atau penolakan terhadap dokumen:<br><br><strong>&emsp;Kode Dokumen : {DOCUMENT_CODE}<br>&emsp;Nama Dokumen : {DOCUMENT_NAME}<br>&emsp;Tanggal Dokumen : {DOCUMENT_DATE}<br>&emsp;Revisi : {REVISION}</strong><br><br>Klik tombol dibawah untuk melihat dokumen yang disebutkan diatas.'
--     WHERE SYSTEM_TYPE = 'DOCUMENT_APPROVAL_REQUEST_EMAIL_TEMPLATE' AND SYSTEM_CODE = 'BODY';
