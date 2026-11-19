-- =====================================================================
-- Organization Change, tahap 2 (2026-11-19): moving what still hangs on an
-- old unit to the new one (document owner, copy holders + their receipts,
-- binders, PIC scopes, Reference Access, departments of a division). Run by
-- Document Control (decision Hendra). Users are only listed.
-- Every moved row is kept with its old and new value in TB_R_ORG_CHANGE_MOVE
-- (audit, and the way back if a move was wrong).
-- Idempotent - safe to run again. Run on DMS_NEW after OrgChange_Migration.sql.
-- =====================================================================
IF OBJECT_ID('dbo.TB_R_ORG_CHANGE_MOVE', 'U') IS NULL
BEGIN
	CREATE TABLE dbo.TB_R_ORG_CHANGE_MOVE (
		MOVE_ID          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TB_R_ORG_CHANGE_MOVE PRIMARY KEY,
		ORG_CHANGE_ID    INT           NOT NULL,
		BATCH_NO         INT           NOT NULL,     -- one Apply = one batch
		ITEM_KIND        VARCHAR(30)   NOT NULL,     -- DOCUMENT, EXTDOC, COPY, RECEIPT, BINDER, PICSCOPE, REFACCESS, DEPARTMENT, MASTER, P4D
		TABLE_NAME       VARCHAR(100)  NOT NULL,
		ROW_KEY          VARCHAR(100)  NOT NULL,
		ITEM_LABEL       VARCHAR(500)  NULL,
		OLD_VALUE        VARCHAR(1000) NULL,         -- the changed columns before, "COL=value; ..."
		NEW_VALUE        VARCHAR(1000) NULL,
		MOVED_BY         VARCHAR(255)  NOT NULL,
		MOVED_DT         DATETIME      NOT NULL
	);
	CREATE INDEX IX_ORG_CHANGE_MOVE_CHANGE ON dbo.TB_R_ORG_CHANGE_MOVE (ORG_CHANGE_ID, BATCH_NO);
END
GO

INSERT INTO dbo.TB_M_FUNCTION (FUNCTION_ID, MENU_ID, FUNCTION_NAME, FUNCTION_DESC, CREATED_BY, CREATED_DT, DELETE_FLAG)
SELECT 'ORGCHANGE-TRANSFER', 'M00002-12', 'Transfer', 'Organization Change Transfer (move documents, copies, binders) Function', 'dms.admin', GETDATE(), 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION WHERE FUNCTION_ID = 'ORGCHANGE-TRANSFER');

-- Document Control runs the transfer (decision 2026-11-19); DMS-ADMIN as the super user
INSERT INTO dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT R.ROLE_ID, 'ORGCHANGE-TRANSFER', 'dms.admin', GETDATE()
FROM (VALUES ('DOC-CONTROL'), ('DMS-ADMIN')) R(ROLE_ID)
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_AUTH_FUNCTION A WHERE A.ROLE_ID = R.ROLE_ID AND A.FUNCTION_ID = 'ORGCHANGE-TRANSFER');
GO
