-- =====================================================================
-- Management System, tahap 1 (2026-11-19): DMS becomes an Integrated Management
-- System. Every document belongs to one management system:
--   QMS   Quality (ISO 9001)            active   - numbers as before
--   K3L   K3 + Lingkungan (45001+14001)  active   - Level 1-2 numbers carry "K3L"
--   SMK3  SMK3 (PP 50/2012)              archive  - "SMK3", existing documents only
--   EMS   Environment (ISO 14001)        archive  - "ENV",  existing documents only
-- NUMBER_CODE replaces the process code (Level 2) / the year (Level 1) in the number,
-- e.g. HMMI/PRO/K3L/006 (Master List PROSEDUR.xlsx). NULL = the QMS pattern.
-- TB_M_MGMT_SYSTEM_USER: who may create documents of a system (CREATOR; none listed =
-- everyone) and its Document Control (CONTROLLER, used from tahap 2).
-- Existing documents become QMS. Idempotent - safe to run again. Run on DMS_NEW.
-- =====================================================================
IF OBJECT_ID('dbo.TB_M_MGMT_SYSTEM', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_M_MGMT_SYSTEM (
		MGMT_SYSTEM_CODE  VARCHAR(10)   NOT NULL CONSTRAINT PK_TB_M_MGMT_SYSTEM PRIMARY KEY,
		MGMT_SYSTEM_NAME  VARCHAR(200)  NOT NULL,
		STANDARD_REF      VARCHAR(200)  NULL,      -- e.g. ISO 9001:2015
		NUMBER_CODE       VARCHAR(10)   NULL,      -- in Level 1-2 document numbers; NULL = QMS pattern
		SORT_NO           INT           NOT NULL CONSTRAINT DF_TB_M_MGMT_SYSTEM_SORT DEFAULT (0),
		IS_ACTIVE         BIT           NOT NULL CONSTRAINT DF_TB_M_MGMT_SYSTEM_ACTIVE DEFAULT (1),  -- 0 = archive: no new documents
		CREATED_BY        VARCHAR(255)  NOT NULL,
		CREATED_DT        DATETIME      NOT NULL,
		CHANGED_BY        VARCHAR(255)  NULL,
		CHANGED_DT        DATETIME      NULL
	);
END
GO

IF OBJECT_ID('dbo.TB_M_MGMT_SYSTEM_USER', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_M_MGMT_SYSTEM_USER (
		MGMT_SYSTEM_CODE  VARCHAR(10)   NOT NULL,
		USERNAME          VARCHAR(255)  NOT NULL,
		USER_ROLE         VARCHAR(20)   NOT NULL,   -- CREATOR / CONTROLLER
		CREATED_BY        VARCHAR(255)  NOT NULL,
		CREATED_DT        DATETIME      NOT NULL,
		CONSTRAINT PK_TB_M_MGMT_SYSTEM_USER PRIMARY KEY (MGMT_SYSTEM_CODE, USERNAME, USER_ROLE)
	);
END
GO

MERGE dbo.TB_M_MGMT_SYSTEM AS T
USING (VALUES
	('QMS',  'Quality Management System',                 'ISO 9001',              NULL,   1, 1),
	('K3L',  'K3 & Lingkungan (Safety, Health, Environment)', 'ISO 45001 + ISO 14001', 'K3L',  2, 1),
	('SMK3', 'Sistem Manajemen K3',                       'PP 50/2012',            'SMK3', 3, 0),
	('EMS',  'Environmental Management System',           'ISO 14001',             'ENV',  4, 0)
) AS S (CODE, NAME, STD, NUM, SORT, ACTIVE)
ON T.MGMT_SYSTEM_CODE = S.CODE
WHEN NOT MATCHED THEN
	INSERT (MGMT_SYSTEM_CODE, MGMT_SYSTEM_NAME, STANDARD_REF, NUMBER_CODE, SORT_NO, IS_ACTIVE, CREATED_BY, CREATED_DT)
	VALUES (S.CODE, S.NAME, S.STD, S.NUM, S.SORT, S.ACTIVE, 'dms.admin', GETDATE());
GO

IF COL_LENGTH('dbo.TB_R_DOCUMENT', 'MGMT_SYSTEM') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT ADD MGMT_SYSTEM VARCHAR(10) NULL;
IF COL_LENGTH('dbo.TB_R_DOCUMENT_HISTORY', 'MGMT_SYSTEM') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT_HISTORY ADD MGMT_SYSTEM VARCHAR(10) NULL;
GO
UPDATE dbo.TB_R_DOCUMENT SET MGMT_SYSTEM = 'QMS' WHERE MGMT_SYSTEM IS NULL;
UPDATE dbo.TB_R_DOCUMENT_HISTORY SET MGMT_SYSTEM = 'QMS' WHERE MGMT_SYSTEM IS NULL;
GO

-- Menu: Master Data > Management System
IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00002-13')
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00002-13', 'M00002', 'Management System', 'fa-cubes', '/ManagementSystem/Index', 11, 'dms.admin', GETDATE(), 0);

INSERT INTO dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, 'M00002-13', 'dms.admin', GETDATE()
FROM (VALUES ('DMS-ADMIN'), ('SYS-ADMIN')) R(ROLE_ID)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_MENU A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = 'M00002-13');

INSERT INTO dbo.TB_M_FUNCTION (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT, DELETE_FLAG)
SELECT F.ID, 'M00002-13', F.NAME, F.DESCR, 'dms.admin', GETDATE(), 0
FROM (VALUES ('MGMTSYSTEM-ADD', 'Add', 'Management System Add Function'),
             ('MGMTSYSTEM-EDIT', 'Edit', 'Management System Edit Function')) F(ID, NAME, DESCR)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION X WHERE X.FUNCTION_ID = F.ID);

INSERT INTO dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, F.ID, 'dms.admin', GETDATE()
FROM (VALUES ('DMS-ADMIN'), ('SYS-ADMIN')) R(ROLE_ID)
CROSS JOIN (VALUES ('MGMTSYSTEM-ADD'), ('MGMTSYSTEM-EDIT')) F(ID)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = R.ROLE_ID AND A.FUNCTION_ID = F.ID);
GO
