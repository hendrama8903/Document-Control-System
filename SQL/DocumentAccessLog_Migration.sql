-- =====================================================================
-- Audit trail akses dokumen (request Hendra 2026-11-06, terinspirasi Folderit)
--
-- Mencatat SIAPA membuka (VIEW), mengunduh (DOWNLOAD), atau mencetak (PRINT)
-- dokumen APA, KAPAN, dari halaman mana, dan dari perangkat/IP mana. Dicatat
-- oleh Services/DocumentAccessLogService di setiap endpoint yang menyajikan
-- file dokumen; pencatatan gagal tidak pernah menggagalkan aksi user-nya.
--
-- Laporan: menu Administration > Document Access Log (/DocumentAccessLog/Index),
-- awalnya hanya untuk role Admin (DMS-ADMIN) - role lain lewat Role Authorization.
--
-- Idempotent. Jalankan di DMS_NEW, lalu sp_DocumentAccessLog_Insert.sql dan
-- sp_DocumentAccessLog_Search.sql.
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('dbo.TB_R_DOCUMENT_ACCESS_LOG', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_DOCUMENT_ACCESS_LOG (
		ACCESS_LOG_ID            BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_DOCUMENT_ACCESS_LOG PRIMARY KEY,
		ACCESS_DT                DATETIME      NOT NULL CONSTRAINT DF_TB_R_DOCUMENT_ACCESS_LOG_DT DEFAULT (GETDATE()),
		USERNAME                 VARCHAR(50)   NOT NULL,
		USER_FULL_NAME           VARCHAR(255)  NULL,
		ACTION                   VARCHAR(10)   NOT NULL,
		SOURCE_TYPE              VARCHAR(10)   NOT NULL,   -- INTERNAL (TB_R_DOCUMENT/HISTORY) / EXTERNAL (TB_M_EXTERNAL_DOCUMENT)
		DOCUMENT_TRANSACTION_ID  INT           NULL,
		EXTERNAL_DOCUMENT_ID     INT           NULL,
		DOCUMENT_CODE            VARCHAR(255)  NULL,
		DOCUMENT_TITLE           VARCHAR(500)  NULL,
		REVISION                 VARCHAR(50)   NULL,
		DEPARTMENT_NAME          VARCHAR(255)  NULL,
		FILE_PATH                VARCHAR(500)  NULL,
		COPIES                   INT           NULL,
		SOURCE_PAGE              VARCHAR(255)  NULL,
		CLIENT_IP                VARCHAR(64)   NULL,
		USER_AGENT               VARCHAR(500)  NULL,
		CONSTRAINT CK_TB_R_DOCUMENT_ACCESS_LOG_ACTION CHECK (ACTION IN ('VIEW', 'DOWNLOAD', 'PRINT'))
	);
	CREATE INDEX IX_TB_R_DOCUMENT_ACCESS_LOG_DT ON dbo.TB_R_DOCUMENT_ACCESS_LOG (ACCESS_DT DESC);
	CREATE INDEX IX_TB_R_DOCUMENT_ACCESS_LOG_DOC ON dbo.TB_R_DOCUMENT_ACCESS_LOG (DOCUMENT_CODE, ACCESS_DT DESC);
	CREATE INDEX IX_TB_R_DOCUMENT_ACCESS_LOG_USER ON dbo.TB_R_DOCUMENT_ACCESS_LOG (USERNAME, ACCESS_DT DESC);
END
GO

-- Menu: Administration > Document Access Log (di samping Activity Log)
IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00003-06')
BEGIN
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT)
	VALUES ('M00003-06', 'M00003', 'Document Access Log', 'fa-eye', '/DocumentAccessLog/Index', 4, 'dms.admin', GETDATE());
END

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_MENU WHERE ROLE_ID = 'DMS-ADMIN' AND MENU_ID = 'M00003-06')
BEGIN
	INSERT INTO dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'M00003-06', 'dms.admin', GETDATE());
END
GO
