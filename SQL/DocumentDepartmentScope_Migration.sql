-- =====================================================================
-- Document PIC and approval by the owning department (request Hendra 2026-11-17).
--
-- Best practice agreed with Hendra (ISO 9001 7.5.2c):
--   - The department on a document is its OWNER; its managers approve it, not the
--     managers of whoever typed it (before: approvers were searched from the
--     creator's position, so an ITD/APP PIC writing an INF document got APP approvers).
--   - A user may prepare documents for the department of their own position. A PIC who
--     prepares documents for other departments is named explicitly: User Management >
--     User Position > "Departments Handled" (TB_M_USER_DEPT_SCOPE).
--
-- 1. TB_M_USER_DEPT_SCOPE                  departments a PIC handles besides their own.
-- 2. fn_User_DocumentDepartments(@USER)    departments a user may put on a document:
--      every department of the user's own division (changed 2026-11-17, before: only
--      the own department, whole division only for Div Head / EO); every department
--      for a position without division (President, Director) and for DMS-ADMIN; plus
--      TB_M_USER_DEPT_SCOPE (departments of other divisions).
-- 3. fn_WorkflowDoc_Steps gets the document's division / department / section:
--      approvers are searched in the DOCUMENT's organisation (falls back to the
--      creator's when not given). The creator approves a step of their own position
--      only inside their own department, or as the first step ("Dibuat Oleh").
--
-- Then run (SQL/StoredProcedures): sp_UserDeptScope_Get, sp_UserDeptScope_Save,
-- sp_DocumentOrg_Divisions, sp_DocumentOrg_Departments, sp_DocumentOrg_Creators,
-- sp_WorkflowDoc_Create, sp_WorkflowDoc_Preview, sp_DocumentMaintenance_Insert,
-- sp_DocumentMaintenance_Update.
--
-- Idempotent. Run on database DMS_NEW.
-- =====================================================================
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.TB_M_USER_DEPT_SCOPE', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.TB_M_USER_DEPT_SCOPE (
        USERNAME      VARCHAR(50)  NOT NULL,
        DEPARTMENT_ID INT          NOT NULL,
        CREATED_BY    VARCHAR(50)  NULL,
        CREATED_DT    DATETIME     NULL,
        CONSTRAINT PK_TB_M_USER_DEPT_SCOPE PRIMARY KEY (USERNAME, DEPARTMENT_ID)
    );
END
GO

CREATE OR ALTER FUNCTION dbo.fn_User_DocumentDepartments (@USERNAME VARCHAR(50))
RETURNS TABLE
AS
RETURN
    SELECT D.DEPARTMENT_ID
    FROM dbo.TB_M_DEPARTMENT D
    WHERE ISNULL(D.DELETE_FLAG, 0) = 0
      AND (
            EXISTS (SELECT 1 FROM dbo.TB_M_USER U WHERE U.USERNAME = @USERNAME AND U.ROLE_ID = 'DMS-ADMIN')
         OR EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS P WHERE P.USERNAME = @USERNAME AND P.DEPARTMENT_ID = D.DEPARTMENT_ID)
         -- any department of the user's own division (Hendra 2026-11-17: a user may prepare
         -- documents for another department of the same division; its managers approve)
         OR EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS P WHERE P.USERNAME = @USERNAME AND P.DIVISION = D.DIVISION)
         OR EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS P WHERE P.USERNAME = @USERNAME AND P.DEPARTMENT_ID IS NULL AND P.DIVISION IS NULL)
         OR EXISTS (SELECT 1 FROM dbo.TB_M_USER_DEPT_SCOPE S WHERE S.USERNAME = @USERNAME AND S.DEPARTMENT_ID = D.DEPARTMENT_ID)
      );
GO

-- SUPERSEDED by SQL/WorkflowApprovalMatrix_2026_Migration.sql (first step by creator level,
-- merged duplicate approvers). Kept for history; run that script after this one.
-- Steps of a workflow with the approver found for each, in the document's organisation.
-- @DIVISION / @DEPARTMENT_ID / @SECTION_ID NULL = the creator's (behaviour before 2026-11-17).
CREATE OR ALTER FUNCTION dbo.fn_WorkflowDoc_Steps (
    @WORKFLOW_DOC_ID INT, @DOCUMENT_CREATOR VARCHAR(50),
    @DIVISION VARCHAR(50), @DEPARTMENT_ID INT, @SECTION_ID INT)
RETURNS TABLE
AS
RETURN
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
			CASE WHEN @DEPARTMENT_ID IS NULL OR @DEPARTMENT_ID = C.DEPARTMENT_ID THEN 1 ELSE 0 END AS SAME_DEPT
	) O
	OUTER APPLY (
		SELECT TOP 1 X.USERNAME
		FROM (
			-- The creator, for a step of their own position: within their own department,
			-- or as the first step of the flow ("Dibuat Oleh", also for a PIC).
			SELECT C.USERNAME, 0 AS ORD
			WHERE D.POSITION_ID = C.POSITION_ID
			  AND (O.SAME_DEPT = 1
			       OR D.WORKFLOW_SEQ = (SELECT MIN(F.WORKFLOW_SEQ) FROM dbo.TB_M_WORKFLOW_DOC_D F WHERE F.WORKFLOW_DOC_ID = @WORKFLOW_DOC_ID)
			       OR P.APPROVER_SCOPE = 'COMPANY'
			       OR (P.APPROVER_SCOPE = 'DIVISION' AND C.DIVISION = O.DIVISION))
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
	WHERE D.WORKFLOW_DOC_ID = @WORKFLOW_DOC_ID;
GO
