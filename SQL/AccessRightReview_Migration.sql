-- =====================================================================
-- Access Rights Review (request Hendra 2026-11-14)
--
-- Laporan audit hak akses: siapa punya role apa, role itu membuka menu dan
-- fungsi apa, kapan terakhir login, dan siapa yang sudah lama tidak memakai
-- aplikasi. Bisa diunduh ke Excel (dengan kolom keputusan reviewer) untuk
-- review hak akses berkala.
--
-- Login sebelumnya hanya tercatat di TB_R_LOG_H dengan user "admin", jadi
-- tidak bisa dipakai untuk "terakhir login". Tabel TB_R_USER_LOGIN_LOG baru
-- mencatat setiap login berhasil/gagal per username (diisi AuthController).
--
-- Menu: Administration > Access Rights Review (/AccessRight/Index), awalnya
-- hanya untuk DMS-ADMIN - role lain lewat Role Authorization.
--
-- Idempotent. Jalankan di DMS_NEW, lalu:
--   StoredProcedures/sp_UserLoginLog_Insert.sql
--   StoredProcedures/sp_AccessRight_Users.sql
--   StoredProcedures/sp_AccessRight_Matrix.sql
--   StoredProcedures/sp_AccessRight_LoginLog.sql
-- =====================================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID('dbo.TB_R_USER_LOGIN_LOG', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_USER_LOGIN_LOG (
		LOGIN_LOG_ID  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_USER_LOGIN_LOG PRIMARY KEY,
		LOGIN_DT      DATETIME      NOT NULL CONSTRAINT DF_TB_R_USER_LOGIN_LOG_DT DEFAULT (GETDATE()),
		USERNAME      VARCHAR(255)  NOT NULL,
		RESULT        VARCHAR(10)   NOT NULL,   -- SUCCESS / FAILED
		MESSAGE       VARCHAR(500)  NULL,
		CLIENT_IP     VARCHAR(64)   NULL,
		USER_AGENT    VARCHAR(500)  NULL,
		CONSTRAINT CK_TB_R_USER_LOGIN_LOG_RESULT CHECK (RESULT IN ('SUCCESS', 'FAILED'))
	);
	CREATE INDEX IX_TB_R_USER_LOGIN_LOG_USER ON dbo.TB_R_USER_LOGIN_LOG (USERNAME, LOGIN_DT DESC) INCLUDE (RESULT);
	CREATE INDEX IX_TB_R_USER_LOGIN_LOG_DT ON dbo.TB_R_USER_LOGIN_LOG (LOGIN_DT DESC);
END
GO

-- Menu: Administration > Access Rights Review (setelah Document Access Log)
IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00003-08')
BEGIN
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00003-08', 'M00003', 'Access Rights Review', 'fa-id-card-o', '/AccessRight/Index', 4, 'dms.admin', GETDATE(), 0);
END

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_MENU WHERE ROLE_ID = 'DMS-ADMIN' AND MENU_ID = 'M00003-08')
BEGIN
	INSERT INTO dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
	VALUES ('DMS-ADMIN', 'M00003-08', 'dms.admin', GETDATE());
END
GO
