SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Departments a user may put on a document (dbo.fn_User_DocumentDepartments), in one
-- division. @DEPARTMENT_ID: check a single department (returns it only if allowed).
-- 2026-11-17, see SQL/DocumentDepartmentScope_Migration.sql.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentOrg_Departments]
	@USERNAME      VARCHAR(50),
	@DIVISION      VARCHAR(50) = NULL,
	@Q             VARCHAR(100) = NULL,
	@DEPARTMENT_ID INT = NULL
AS
BEGIN
	SET NOCOUNT ON;
	SELECT CAST(D.DEPARTMENT_ID AS VARCHAR(20)) AS id, D.DEPARTMENT_CODE + ' - ' + D.DEPARTMENT_NAME AS text
	FROM dbo.fn_User_DocumentDepartments(@USERNAME) A
	JOIN dbo.TB_M_DEPARTMENT D ON D.DEPARTMENT_ID = A.DEPARTMENT_ID
	WHERE (@DIVISION IS NULL OR @DIVISION = '' OR D.DIVISION = @DIVISION)
	  AND (@DEPARTMENT_ID IS NULL OR D.DEPARTMENT_ID = @DEPARTMENT_ID)
	  AND (@Q IS NULL OR @Q = '' OR D.DEPARTMENT_CODE + ' - ' + D.DEPARTMENT_NAME LIKE '%' + @Q + '%')
	ORDER BY D.DEPARTMENT_CODE;
END
GO
