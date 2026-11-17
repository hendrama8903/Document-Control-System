SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Divisions a user may choose in Document Preparation: those of the departments in
-- dbo.fn_User_DocumentDepartments (own position + Departments Handled). 2026-11-17.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentOrg_Divisions]
	@USERNAME VARCHAR(50),
	@Q        VARCHAR(100) = NULL
AS
BEGIN
	SET NOCOUNT ON;
	SELECT V.DIVISION_CODE AS id, V.DIVISION_CODE + ' - ' + V.DIVISION_NAME AS text
	FROM dbo.TB_M_DIVISION V
	WHERE ISNULL(V.DELETE_FLAG, 0) = 0
	  AND EXISTS (SELECT 1 FROM dbo.fn_User_DocumentDepartments(@USERNAME) A
	              JOIN dbo.TB_M_DEPARTMENT D ON D.DEPARTMENT_ID = A.DEPARTMENT_ID
	              WHERE D.DIVISION = V.DIVISION_CODE)
	  AND (@Q IS NULL OR @Q = '' OR V.DIVISION_CODE + ' - ' + V.DIVISION_NAME LIKE '%' + @Q + '%')
	ORDER BY V.DIVISION_CODE;
END
GO
