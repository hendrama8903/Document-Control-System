SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- "Creator" choices in Document Preparation for a department: active users with a
-- position in it, plus the login user when they may prepare documents for it
-- (a PIC from another department signs "Dibuat Oleh" themselves). 2026-11-17.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentOrg_Creators]
	@LOGIN_USER    VARCHAR(50),
	@DEPARTMENT_ID INT,
	@Q             VARCHAR(100) = NULL
AS
BEGIN
	SET NOCOUNT ON;
	SELECT U.USERNAME AS id, U.FULL_NAME AS text
	FROM dbo.TB_M_USER U
	WHERE ISNULL(U.DELETE_FLAG, '0') = '0'
	  AND (EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS P WHERE P.USERNAME = U.USERNAME AND P.DEPARTMENT_ID = @DEPARTMENT_ID)
	       OR (U.USERNAME = @LOGIN_USER
	           AND EXISTS (SELECT 1 FROM dbo.fn_User_DocumentDepartments(@LOGIN_USER) A WHERE A.DEPARTMENT_ID = @DEPARTMENT_ID)))
	  AND (@Q IS NULL OR @Q = '' OR U.FULL_NAME LIKE '%' + @Q + '%' OR U.USERNAME LIKE '%' + @Q + '%')
	ORDER BY CASE WHEN U.USERNAME = @LOGIN_USER THEN 0 ELSE 1 END, U.FULL_NAME;
END
GO
