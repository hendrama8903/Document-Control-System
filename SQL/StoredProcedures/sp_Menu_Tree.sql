-- Returns the full menu tree (all rows, unpaged) for the redesigned Menu & Function page:
-- left tree panel needs every menu at once (not server-paged) plus FUNCTION_COUNT
-- (own count for a child menu, summed across children for a top-level menu),
-- USED_BY_ROLES (distinct roles with this MENU_ID in TB_M_AUTH_MENU) and DELETE_FLAG.
-- Read-only, does not affect sp_Menu_Search (still used by existing typeahead endpoints).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Menu_Tree]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		M.MENU_ID,
		M.PARENT_ID,
		B.MENU_NAME AS PARENT_NAME,
		M.MENU_NAME,
		M.MENU_ICON,
		M.MENU_URL,
		M.MENU_SEQ,
		ISNULL(M.DELETE_FLAG, 0) AS DELETE_FLAG,
		CASE
			WHEN M.PARENT_ID IS NULL THEN ISNULL((
				SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION] F
				JOIN [dbo].[TB_M_MENU] C ON C.MENU_ID = F.MENU_ID
				WHERE C.PARENT_ID = M.MENU_ID
			), 0)
			ELSE ISNULL((SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION] F WHERE F.MENU_ID = M.MENU_ID), 0)
		END AS FUNCTION_COUNT,
		ISNULL((SELECT COUNT(DISTINCT ROLE_ID) FROM [dbo].[TB_M_AUTH_MENU] A WHERE A.MENU_ID = M.MENU_ID), 0) AS USED_BY_ROLES,
		M.CREATED_DT,
		M.CREATED_BY,
		M.CHANGED_DT,
		M.CHANGED_BY
	FROM [dbo].[TB_M_MENU] M
	LEFT JOIN [dbo].[TB_M_MENU] B ON M.PARENT_ID = B.MENU_ID
	ORDER BY M.PARENT_ID ASC, M.MENU_SEQ ASC
END
GO
