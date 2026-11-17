-- =====================================================================
-- Approval workflows from the QMS approval matrix (request Hendra 2026-11-17,
-- DokumenMigrasi/list approval.xlsx, sheet "Approval Level Dokumen").
--
-- Decisions (Hendra 2026-11-17):
--   - The 7 Level III types of the matrix missing in Document Category (QCPC, WI, WS,
--     SWS, SIK, QIS, AIS) are created, Level 3, without a template (upload it in
--     Document Category). SQAM and HSM have no approval yet in the matrix: skipped.
--   - Anyone may create a document; the flow is the same. The creator fills the
--     "Dibuat" step (PIC), the next steps follow the matrix. One header per creator
--     level 1-5 per category (the system picks the header by the creator's level).
--   - TS = Staff (position 1).
--   - Level IV (CS, FR, SM, RE): no approval in the matrix yet, left as it is.
--
-- Matrix (after "Dibuat: PIC"):
--   PDM           Dibuat: Dept Head | Diperiksa: Div Head, EO, Direktur | Disetujui: Presdir
--   PRO, SPR      Dibuat: Dept Head | Diperiksa: Div Head               | Disetujui: EO
--   QCPC          Diperiksa: Sec Head, Dept Head                        | Disetujui: Div Head
--   SOP, IK, EIS, SOE, OPL, ACU, WI, WS, SWS, SIK, QIS, AIS
--                 Diperiksa: Sec Head                                   | Disetujui: Dept Head
--   The "Disetujui" step is mandatory.
--
-- fn_WorkflowDoc_Steps (supersedes the one in DocumentDepartmentScope_Migration.sql):
--   - The first step ("Dibuat") is the creator when the step's position has the
--     creator's level (Section Head / Coordinator fill a Supervisor step, TS fills Staff).
--   - A step whose approver already signs an earlier step is merged (IS_DUPLICATE = 1,
--     no approver, not mandatory), e.g. a Dept Head creating a PRO signs "Dibuat" once.
--   Then run sp_WorkflowDoc_Preview (status SAME for a merged step).
--
-- Replaces the trial workflows of PDM (default Level 1), PRO and SOP. Keeps Level 9
-- defaults and CS. Backup: BAK_TB_M_WORKFLOW_DOC_H/D_20261117. Idempotent. DMS_NEW.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER FUNCTION dbo.fn_WorkflowDoc_Steps (
    @WORKFLOW_DOC_ID INT, @DOCUMENT_CREATOR VARCHAR(50),
    @DIVISION VARCHAR(50), @DEPARTMENT_ID INT, @SECTION_ID INT)
RETURNS TABLE
AS
RETURN
	WITH B AS (
		SELECT
			D.WORKFLOW_SEQ,
			D.POSITION_ID,
			P.POSITION_NAME,
			P.APPROVER_SCOPE,
			D.LABEL,
			D.IS_MANDATORY,
			A.USERNAME AS APPROVER
		FROM dbo.TB_M_WORKFLOW_DOC_D D
		LEFT JOIN dbo.TB_M_POSITION P ON P.POSITION_ID = D.POSITION_ID
		OUTER APPLY dbo.fn_WorkflowDoc_Creator(@DOCUMENT_CREATOR) C
		OUTER APPLY (
			SELECT
				COALESCE(@DIVISION, C.DIVISION) AS DIVISION,
				COALESCE(@DEPARTMENT_ID, C.DEPARTMENT_ID) AS DEPARTMENT_ID,
				CASE WHEN @SECTION_ID IS NOT NULL THEN @SECTION_ID
				     WHEN @DEPARTMENT_ID IS NULL OR @DEPARTMENT_ID = C.DEPARTMENT_ID THEN C.SECTION_ID END AS SECTION_ID,
				CASE WHEN @DEPARTMENT_ID IS NULL OR @DEPARTMENT_ID = C.DEPARTMENT_ID THEN 1 ELSE 0 END AS SAME_DEPT,
				CASE WHEN D.WORKFLOW_SEQ = (SELECT MIN(F.WORKFLOW_SEQ) FROM dbo.TB_M_WORKFLOW_DOC_D F WHERE F.WORKFLOW_DOC_ID = @WORKFLOW_DOC_ID)
				     THEN 1 ELSE 0 END AS FIRST_STEP
		) O
		OUTER APPLY (
			SELECT TOP 1 X.USERNAME
			FROM (
				-- The creator: the first step ("Dibuat", PIC) when the position has the
				-- creator's level; any step of their own position inside their own
				-- department (or company / own division scope).
				SELECT C.USERNAME, 0 AS ORD
				WHERE (O.FIRST_STEP = 1 AND P.POSITION_LEVEL = C.POSITION_LEVEL)
				   OR (D.POSITION_ID = C.POSITION_ID
				       AND (O.SAME_DEPT = 1
				            OR O.FIRST_STEP = 1
				            OR P.APPROVER_SCOPE = 'COMPANY'
				            OR (P.APPROVER_SCOPE = 'DIVISION' AND C.DIVISION = O.DIVISION)))
				UNION ALL
				SELECT UP.USERNAME, 1 AS ORD
				FROM dbo.TB_M_USER_POS UP
				WHERE UP.POSITION_ID = D.POSITION_ID
				AND (
					P.APPROVER_SCOPE = 'COMPANY'
					OR (P.APPROVER_SCOPE = 'DIVISION'   AND UP.DIVISION = O.DIVISION)
					OR (P.APPROVER_SCOPE = 'DEPARTMENT' AND UP.DIVISION = O.DIVISION AND UP.DEPARTMENT_ID = O.DEPARTMENT_ID)
					OR (P.APPROVER_SCOPE = 'SECTION'    AND UP.DIVISION = O.DIVISION AND UP.DEPARTMENT_ID = O.DEPARTMENT_ID
					    AND (UP.SECTION_ID = O.SECTION_ID OR (O.SECTION_ID IS NULL AND O.SAME_DEPT = 0)))
				)
			) X
			ORDER BY X.ORD, X.USERNAME
		) A
		WHERE D.WORKFLOW_DOC_ID = @WORKFLOW_DOC_ID
	)
	SELECT
		B.WORKFLOW_SEQ,
		B.POSITION_ID,
		B.POSITION_NAME,
		B.APPROVER_SCOPE,
		B.LABEL,
		CAST(CASE WHEN DUP.X = 1 THEN 0 ELSE B.IS_MANDATORY END AS BIT) AS IS_MANDATORY,
		CASE WHEN DUP.X = 1 THEN NULL ELSE B.APPROVER END AS APPROVER,
		CAST(ISNULL(DUP.X, 0) AS BIT) AS IS_DUPLICATE
	FROM B
	OUTER APPLY (
		SELECT TOP 1 1 AS X FROM B E
		WHERE E.WORKFLOW_SEQ < B.WORKFLOW_SEQ AND E.APPROVER = B.APPROVER
	) DUP;
GO

-- ---------------------------------------------------------------------
-- Data
-- ---------------------------------------------------------------------
BEGIN TRAN;

IF OBJECT_ID('dbo.BAK_TB_M_WORKFLOW_DOC_H_20261117', 'U') IS NULL
	SELECT * INTO dbo.BAK_TB_M_WORKFLOW_DOC_H_20261117 FROM dbo.TB_M_WORKFLOW_DOC_H;
IF OBJECT_ID('dbo.BAK_TB_M_WORKFLOW_DOC_D_20261117', 'U') IS NULL
	SELECT * INTO dbo.BAK_TB_M_WORKFLOW_DOC_D_20261117 FROM dbo.TB_M_WORKFLOW_DOC_D;

-- 1. Missing Level III categories
DECLARE @NEWCAT TABLE (CODE VARCHAR(5), NAME VARCHAR(255));
INSERT INTO @NEWCAT VALUES
	('QCPC', 'Quality Control Process Chart'),
	('WI',   'Welding Instruction'),
	('WS',   'Welding Specification'),
	('SWS',  'Safety Work Standard'),
	('SIK',  'Standard Instruksi Kerja'),
	('QIS',  'Quality Inspection Standard'),
	('AIS',  'Assembly Instruction Sheet');

INSERT INTO dbo.TB_M_DOCUMENT (DOCUMENT_CODE, DOCUMENT_NAME, [LEVEL], FILE_PATH, DELETE_FLAG, CREATED_DT, CREATED_BY, CHANGED_DT, CHANGED_BY)
SELECT N.CODE, N.NAME, 3, NULL, 0, GETDATE(), 'dms.admin', GETDATE(), 'dms.admin'
FROM @NEWCAT N
WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_DOCUMENT D WHERE D.DOCUMENT_CODE = N.CODE AND ISNULL(D.DELETE_FLAG, 0) = 0);

-- 2. Steps after "Dibuat: PIC", per category
DECLARE @FLOW TABLE (CODE VARCHAR(5), SEQ INT, POSITION_ID INT, LABEL VARCHAR(255), MANDATORY BIT);
INSERT INTO @FLOW VALUES
	('PDM', 2, 3, 'Dibuat Oleh', 0), ('PDM', 3, 4, 'Diperiksa Oleh', 0), ('PDM', 4, 5, 'Diperiksa Oleh', 0),
	('PDM', 5, 6, 'Diperiksa Oleh', 0), ('PDM', 6, 7, 'Disetujui Oleh', 1),
	('PRO', 2, 3, 'Dibuat Oleh', 0), ('PRO', 3, 4, 'Diperiksa Oleh', 0), ('PRO', 4, 5, 'Disetujui Oleh', 1),
	('SPR', 2, 3, 'Dibuat Oleh', 0), ('SPR', 3, 4, 'Diperiksa Oleh', 0), ('SPR', 4, 5, 'Disetujui Oleh', 1),
	('QCPC', 2, 2, 'Diperiksa Oleh', 0), ('QCPC', 3, 3, 'Diperiksa Oleh', 0), ('QCPC', 4, 4, 'Disetujui Oleh', 1);

INSERT INTO @FLOW
SELECT C.CODE, S.SEQ, S.POSITION_ID, S.LABEL, S.MANDATORY
FROM (VALUES ('SOP'), ('IK'), ('EIS'), ('SOE'), ('OPL'), ('ACU'), ('WI'), ('WS'), ('SWS'), ('SIK'), ('QIS'), ('AIS')) C(CODE)
CROSS JOIN (VALUES (2, 2, 'Diperiksa Oleh', CAST(0 AS BIT)), (3, 3, 'Disetujui Oleh', CAST(1 AS BIT))) S(SEQ, POSITION_ID, LABEL, MANDATORY);

-- "Dibuat: PIC" = a position of the creator's level
DECLARE @PIC TABLE (CREATOR_LEVEL INT, POSITION_ID INT);
INSERT INTO @PIC VALUES (1, 1), (2, 9), (3, 3), (4, 4), (5, 5);

DECLARE @CAT TABLE (DOCUMENT_ID INT, CODE VARCHAR(5), [LEVEL] INT);
INSERT INTO @CAT
SELECT D.DOCUMENT_ID, D.DOCUMENT_CODE, D.[LEVEL]
FROM dbo.TB_M_DOCUMENT D
WHERE ISNULL(D.DELETE_FLAG, 0) = 0 AND D.DOCUMENT_CODE IN (SELECT CODE FROM @FLOW);

-- 3. Remove the trial / earlier workflows of these categories and the trial Level 1 default
DELETE D FROM dbo.TB_M_WORKFLOW_DOC_D D
JOIN dbo.TB_M_WORKFLOW_DOC_H H ON H.WORKFLOW_DOC_ID = D.WORKFLOW_DOC_ID
WHERE H.DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @CAT) OR (H.DOCUMENT_ID IS NULL AND H.DOCUMENT_LEVEL = 1);

DELETE FROM dbo.TB_M_WORKFLOW_DOC_H
WHERE DOCUMENT_ID IN (SELECT DOCUMENT_ID FROM @CAT) OR (DOCUMENT_ID IS NULL AND DOCUMENT_LEVEL = 1);

-- 4. One header per category x creator level
DECLARE @MAP TABLE (WORKFLOW_DOC_ID INT, DOCUMENT_ID INT, CREATOR_LEVEL INT);

MERGE dbo.TB_M_WORKFLOW_DOC_H AS T
USING (SELECT C.DOCUMENT_ID, C.[LEVEL], P.CREATOR_LEVEL FROM @CAT C CROSS JOIN @PIC P) AS S
ON 1 = 0
WHEN NOT MATCHED THEN
	INSERT (DOCUMENT_LEVEL, CREATOR_LEVEL, DOCUMENT_ID, IS_ACTIVE, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT)
	VALUES (S.[LEVEL], S.CREATOR_LEVEL, S.DOCUMENT_ID, 1, 'dms.admin', GETDATE(), 'dms.admin', GETDATE())
OUTPUT inserted.WORKFLOW_DOC_ID, S.DOCUMENT_ID, S.CREATOR_LEVEL INTO @MAP;

INSERT INTO dbo.TB_M_WORKFLOW_DOC_D (WORKFLOW_DOC_ID, WORKFLOW_SEQ, POSITION_ID, LABEL, IS_MANDATORY, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT)
SELECT M.WORKFLOW_DOC_ID, 1, P.POSITION_ID, 'Dibuat Oleh', 0, 'dms.admin', GETDATE(), 'dms.admin', GETDATE()
FROM @MAP M JOIN @PIC P ON P.CREATOR_LEVEL = M.CREATOR_LEVEL
UNION ALL
SELECT M.WORKFLOW_DOC_ID, F.SEQ, F.POSITION_ID, F.LABEL, F.MANDATORY, 'dms.admin', GETDATE(), 'dms.admin', GETDATE()
FROM @MAP M JOIN @CAT C ON C.DOCUMENT_ID = M.DOCUMENT_ID JOIN @FLOW F ON F.CODE = C.CODE;

COMMIT;

SELECT C.CODE, COUNT(DISTINCT M.WORKFLOW_DOC_ID) AS HEADERS, COUNT(*) AS STEPS
FROM @MAP M JOIN @CAT C ON C.DOCUMENT_ID = M.DOCUMENT_ID
JOIN dbo.TB_M_WORKFLOW_DOC_D D ON D.WORKFLOW_DOC_ID = M.WORKFLOW_DOC_ID
GROUP BY C.CODE ORDER BY C.CODE;
GO
