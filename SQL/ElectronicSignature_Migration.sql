-- =====================================================================
-- Tanda tangan elektronik (e-sign) seperti Folderit - request Hendra 2026-11-06
--
-- Gambar tanda tangan upload (TB_M_USER.SIGNATURE_PATH) tidak dipakai lagi di
-- lembar pengesahan. Setiap kali menandatangani (pembuat saat Save, approver
-- saat Approve, Div Head saat Mengetahui), user menggambar tanda tangan dengan
-- mouse/jari (DRAW) atau mengetik nama (TYPE). Yang disimpan BUKAN file gambar:
--   DRAW -> SIGN_DATA = goresan (JSON koordinat), dirender jadi gambar saat PDF dibuat
--   TYPE -> SIGN_DATA = teks nama, SIGN_FONT = font tulisan tangan yang dipilih
-- Setiap baris terikat ke SATU tanda tangan (dokumen + langkah), tidak bisa dipakai
-- ulang di dokumen lain, dan mencatat waktu, IP, browser, serta hash file dokumen
-- yang ditandatangani sebagai jejak audit.
--
--   KIND = 'CREATOR'  - pembuat dokumen             REF_KEY = ''
--   KIND = 'APPROVAL' - langkah approval            REF_KEY = APPROVAL_DETAIL_ID
--   KIND = 'ACK'      - "Mengetahui" divisi terkait REF_KEY = DIVISION_CODE
--
-- Idempotent. Jalankan di DMS_NEW, lalu sp_Signature_Save.sql & sp_Signature_Get.sql.
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('dbo.TB_R_SIGNATURE', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_SIGNATURE (
		SIGNATURE_ID             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_SIGNATURE PRIMARY KEY,
		KIND                     VARCHAR(10)   NOT NULL,
		DOCUMENT_TRANSACTION_ID  INT           NOT NULL,
		REF_KEY                  VARCHAR(50)   NOT NULL,
		SIGNER                   VARCHAR(50)   NOT NULL,
		SIGNER_NAME              VARCHAR(255)  NULL,
		SIGNER_POSITION          VARCHAR(255)  NULL,
		SIGN_METHOD              VARCHAR(10)   NOT NULL,
		SIGN_DATA                NVARCHAR(MAX) NOT NULL,
		SIGN_FONT                VARCHAR(50)   NULL,
		SIGNED_DT                DATETIME      NOT NULL CONSTRAINT DF_TB_R_SIGNATURE_SIGNED_DT DEFAULT (GETDATE()),
		CLIENT_IP                VARCHAR(64)   NULL,
		USER_AGENT               VARCHAR(500)  NULL,
		DOCUMENT_FILE_HASH       VARCHAR(64)   NULL,
		CONSTRAINT CK_TB_R_SIGNATURE_KIND CHECK (KIND IN ('CREATOR', 'APPROVAL', 'ACK')),
		CONSTRAINT CK_TB_R_SIGNATURE_METHOD CHECK (SIGN_METHOD IN ('DRAW', 'TYPE'))
	);
	CREATE UNIQUE INDEX UX_TB_R_SIGNATURE_REF ON dbo.TB_R_SIGNATURE (KIND, DOCUMENT_TRANSACTION_ID, REF_KEY);
END
GO
