-- =====================================================================
-- Organization Change, tahap 1 (2026-11-19, keputusan QMS via Hendra)
--
-- Register of organisation changes announced by HR (rename, merger, split,
-- move to another division, department becomes a section, new unit, unit
-- closed): effective date, HR reference + memo file, old code -> new code.
-- Old codes are deactivated with VALID_TO, never deleted again. The
-- register replaces System Parameters DEPT_SUCCESSOR (Master List import
-- reads the register) and feeds the report "Organization Change History"
-- (bukti audit, jawaban QMS no. 8).
--
--   Master Data > Organization Change         M00002-12  (register)
--   Reports     > Organization Change History M00008-05  (report)
--
-- Idempotent - safe to run again. Run on DMS_NEW.
-- =====================================================================

IF OBJECT_ID('dbo.TB_R_ORG_CHANGE', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_ORG_CHANGE (
		ORG_CHANGE_ID    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_ORG_CHANGE PRIMARY KEY,
		CHANGE_TYPE      VARCHAR(20)   NOT NULL,   -- RENAME, MERGE, SPLIT, MOVE, DOWNGRADE, NEW, CLOSE
		ORG_LEVEL        VARCHAR(20)   NOT NULL,   -- DIVISION, DEPARTMENT, SECTION (level of the old unit)
		EFFECTIVE_DATE   DATE          NULL,       -- NULL only for rows migrated without a known date
		HR_REFERENCE     VARCHAR(200)  NULL,       -- memo / SK number from HR
		DESCRIPTION      VARCHAR(1000) NULL,
		ATTACHMENT_PATH  VARCHAR(500)  NULL,       -- /Upload/ATTACHMENT/ORG_CHANGE/..., served only by the controller
		ATTACHMENT_NAME  VARCHAR(255)  NULL,
		[STATUS]         TINYINT       NOT NULL CONSTRAINT DF_TB_R_ORG_CHANGE_STATUS DEFAULT (1),  -- 1 = valid, 0 = cancelled
		CANCEL_REASON    VARCHAR(500)  NULL,
		CREATED_BY       VARCHAR(255)  NOT NULL,
		CREATED_DT       DATETIME      NOT NULL,
		CHANGED_BY       VARCHAR(255)  NULL,
		CHANGED_DT       DATETIME      NULL
	);
END
GO

IF OBJECT_ID('dbo.TB_R_ORG_CHANGE_MAP', 'U') IS NULL
BEGIN
	-- one row per old -> new pair; OLD_CODE NULL = new unit, NEW_CODE NULL = unit closed.
	-- Codes and names are snapshots: the master row may be renamed or deactivated later.
	CREATE TABLE dbo.TB_R_ORG_CHANGE_MAP (
		MAP_ID           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_ORG_CHANGE_MAP PRIMARY KEY,
		ORG_CHANGE_ID    INT           NOT NULL CONSTRAINT FK_ORG_CHANGE_MAP_CHANGE REFERENCES dbo.TB_R_ORG_CHANGE (ORG_CHANGE_ID),
		SEQ              INT           NOT NULL,
		OLD_CODE         VARCHAR(50)   NULL,
		OLD_NAME         VARCHAR(255)  NULL,
		OLD_PARENT       VARCHAR(50)   NULL,       -- division of a department, department of a section
		NEW_LEVEL        VARCHAR(20)   NULL,       -- differs from ORG_LEVEL when a department becomes a section
		NEW_CODE         VARCHAR(50)   NULL,
		NEW_NAME         VARCHAR(255)  NULL,
		NEW_PARENT       VARCHAR(50)   NULL,
		IS_OWNER         BIT           NOT NULL CONSTRAINT DF_TB_R_ORG_CHANGE_MAP_OWNER DEFAULT (0)  -- split: the new unit that owns the documents by default
	);
	CREATE INDEX IX_ORG_CHANGE_MAP_CHANGE ON dbo.TB_R_ORG_CHANGE_MAP (ORG_CHANGE_ID);
	CREATE INDEX IX_ORG_CHANGE_MAP_OLD ON dbo.TB_R_ORG_CHANGE_MAP (OLD_CODE);
END
GO

IF OBJECT_ID('dbo.TB_R_ORG_CHANGE_LOG', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_ORG_CHANGE_LOG (
		LOG_ID           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_ORG_CHANGE_LOG PRIMARY KEY,
		ORG_CHANGE_ID    INT           NOT NULL,
		[ACTION]         VARCHAR(30)   NOT NULL,   -- CREATE, EDIT, CANCEL, DEACTIVATE, MIGRATE (tahap 2: MOVE_*)
		DETAIL           VARCHAR(2000) NULL,
		ACTION_BY        VARCHAR(255)  NOT NULL,
		ACTION_DT        DATETIME      NOT NULL
	);
	CREATE INDEX IX_ORG_CHANGE_LOG_CHANGE ON dbo.TB_R_ORG_CHANGE_LOG (ORG_CHANGE_ID);
END
GO

-- ---------------------------------------------------------------------
-- Move System Parameters DEPT_SUCCESSOR into the register: one change per
-- set of new codes (LAS + LPD -> LPA becomes one merger). Effective date and
-- HR reference are unknown: left empty for Document Control to complete.
-- The DEPT_SUCCESSOR rows are switched off (STATUS 0), not deleted.
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.TB_R_ORG_CHANGE_LOG WHERE [ACTION] = 'MIGRATE')
   AND EXISTS (SELECT 1 FROM dbo.TB_M_SYSTEM WHERE SYSTEM_TYPE = 'DEPT_SUCCESSOR' AND ISNULL([STATUS], 1) = 1)
BEGIN
	DECLARE @S TABLE (OLD_CODE VARCHAR(50), NEW_VALUE VARCHAR(255));
	INSERT INTO @S
	SELECT UPPER(LTRIM(RTRIM(SYSTEM_CODE))), UPPER(REPLACE(REPLACE(LTRIM(RTRIM(SYSTEM_VALUE)), ' ', ''), ';', ','))
	FROM dbo.TB_M_SYSTEM
	WHERE SYSTEM_TYPE = 'DEPT_SUCCESSOR' AND ISNULL([STATUS], 1) = 1
	  AND LTRIM(RTRIM(ISNULL(SYSTEM_CODE, ''))) <> '' AND LTRIM(RTRIM(ISNULL(SYSTEM_VALUE, ''))) <> '';

	DECLARE @NEW_VALUE VARCHAR(255), @ID INT, @OLD_COUNT INT, @NEW_COUNT INT;
	DECLARE C CURSOR LOCAL FAST_FORWARD FOR SELECT DISTINCT NEW_VALUE FROM @S;
	OPEN C;
	FETCH NEXT FROM C INTO @NEW_VALUE;
	WHILE @@FETCH_STATUS = 0
	BEGIN
		SELECT @OLD_COUNT = COUNT(*) FROM @S WHERE NEW_VALUE = @NEW_VALUE;
		SELECT @NEW_COUNT = COUNT(*) FROM STRING_SPLIT(@NEW_VALUE, ',') WHERE value <> '';

		INSERT INTO dbo.TB_R_ORG_CHANGE (CHANGE_TYPE, ORG_LEVEL, EFFECTIVE_DATE, HR_REFERENCE, DESCRIPTION, [STATUS], CREATED_BY, CREATED_DT)
		VALUES (CASE WHEN @NEW_COUNT > 1 THEN 'SPLIT' WHEN @OLD_COUNT > 1 THEN 'MERGE' ELSE 'RENAME' END,
			'DEPARTMENT', NULL, NULL,
			'Migrated from System Parameters DEPT_SUCCESSOR (reorganisation QMS 2026). Complete the effective date and HR reference.',
			1, 'dms.admin', GETDATE());
		SET @ID = SCOPE_IDENTITY();

		-- every old code x every new code; the first new code owns the documents
		INSERT INTO dbo.TB_R_ORG_CHANGE_MAP (ORG_CHANGE_ID, SEQ, OLD_CODE, OLD_NAME, NEW_LEVEL, NEW_CODE, NEW_NAME, NEW_PARENT, IS_OWNER)
		SELECT @ID,
			ROW_NUMBER() OVER (ORDER BY S.OLD_CODE, N.ORD),
			S.OLD_CODE, NULL, 'DEPARTMENT', N.CODE, D.DEPARTMENT_NAME, D.DIVISION,
			CASE WHEN N.ORD = 1 THEN 1 ELSE 0 END
		FROM @S S
		CROSS APPLY (SELECT value AS CODE, ROW_NUMBER() OVER (ORDER BY CHARINDEX(',' + value + ',', ',' + @NEW_VALUE + ',')) AS ORD
		             FROM STRING_SPLIT(@NEW_VALUE, ',') WHERE value <> '') N
		LEFT JOIN dbo.TB_M_DEPARTMENT D ON D.DEPARTMENT_CODE = N.CODE AND ISNULL(D.DELETE_FLAG, 0) <> 2
		WHERE S.NEW_VALUE = @NEW_VALUE;

		INSERT INTO dbo.TB_R_ORG_CHANGE_LOG (ORG_CHANGE_ID, [ACTION], DETAIL, ACTION_BY, ACTION_DT)
		SELECT @ID, 'MIGRATE', 'From DEPT_SUCCESSOR: ' + STRING_AGG(OLD_CODE, ', ') + ' -> ' + @NEW_VALUE, 'dms.admin', GETDATE()
		FROM @S WHERE NEW_VALUE = @NEW_VALUE;

		FETCH NEXT FROM C INTO @NEW_VALUE;
	END
	CLOSE C;
	DEALLOCATE C;

	UPDATE dbo.TB_M_SYSTEM
	SET [STATUS] = 0, CHANGED_BY = 'dms.admin', CHANGED_DT = GETDATE()
	WHERE SYSTEM_TYPE = 'DEPT_SUCCESSOR' AND ISNULL([STATUS], 1) = 1;
END
GO

-- ---------------------------------------------------------------------
-- Menus, functions, roles
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00002-12')
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00002-12', 'M00002', 'Organization Change', 'fa-sitemap', '/OrgChange/Index', 10, 'dms.admin', GETDATE(), 0);

IF NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU WHERE MENU_ID = 'M00008-05')
	INSERT INTO dbo.TB_M_MENU (MENU_ID, PARENT_ID, MENU_NAME, MENU_ICON, MENU_URL, MENU_SEQ, CREATED_BY, CREATED_DT, DELETE_FLAG)
	VALUES ('M00008-05', 'M00008', 'Organization Change History', 'fa-sitemap', '/OrgChangeReport/Index', 5, 'dms.admin', GETDATE(), 0);

-- register: the master-data roles and Document Control; report: also the auditor
INSERT INTO dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, R.MENU_ID, 'dms.admin', GETDATE()
FROM (VALUES ('DMS-ADMIN', 'M00002'), ('SYS-ADMIN', 'M00002'), ('DOC-CONTROL', 'M00002'),
             ('DMS-ADMIN', 'M00002-12'), ('SYS-ADMIN', 'M00002-12'), ('DOC-CONTROL', 'M00002-12'),
             ('DMS-ADMIN', 'M00008-05'), ('DOC-CONTROL', 'M00008-05'), ('AUDITOR', 'M00008-05')) R(ROLE_ID, MENU_ID)
WHERE EXISTS (SELECT 1 FROM dbo.TB_M_ROLE X WHERE X.ROLE_ID = R.ROLE_ID)
  AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_MENU A WHERE A.ROLE_ID = R.ROLE_ID AND A.MENU_ID = R.MENU_ID);

INSERT INTO dbo.TB_M_FUNCTION (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT, DELETE_FLAG)
SELECT F.FUNCTION_ID, 'M00002-12', F.FUNCTION_NAME, F.FUNCTION_DESC, 'dms.admin', GETDATE(), 0
FROM (VALUES
	('ORGCHANGE-ADD',        'Add',        'Organization Change Add Function'),
	('ORGCHANGE-EDIT',       'Edit',       'Organization Change Edit Function'),
	('ORGCHANGE-CANCEL',     'Cancel',     'Organization Change Cancel Function'),
	('ORGCHANGE-DEACTIVATE', 'Deactivate', 'Organization Change Deactivate Old Codes Function')
) F(FUNCTION_ID, FUNCTION_NAME, FUNCTION_DESC)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION X WHERE X.FUNCTION_ID = F.FUNCTION_ID);

INSERT INTO dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, F.FUNCTION_ID, 'dms.admin', GETDATE()
FROM (VALUES ('DMS-ADMIN'), ('SYS-ADMIN'), ('DOC-CONTROL')) R(ROLE_ID)
CROSS JOIN (VALUES ('ORGCHANGE-ADD'), ('ORGCHANGE-EDIT'), ('ORGCHANGE-CANCEL'), ('ORGCHANGE-DEACTIVATE')) F(FUNCTION_ID)
WHERE EXISTS (SELECT 1 FROM dbo.TB_M_ROLE X WHERE X.ROLE_ID = R.ROLE_ID)
  AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = R.ROLE_ID AND A.FUNCTION_ID = F.FUNCTION_ID);
GO
