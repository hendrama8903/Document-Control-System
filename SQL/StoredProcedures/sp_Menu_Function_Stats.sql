-- Global stat-card numbers for the Menu & Function page header. Kept as a dedicated
-- single-row query rather than derived client-side from sp_Menu_Tree, because
-- "distinct roles across all menus/functions" and "inactive menus+functions combined"
-- can't be correctly computed from per-row counts without double counting.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_Menu_Function_Stats]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		(SELECT COUNT(*) FROM [dbo].[TB_M_MENU]) AS TOTAL_MENUS,
		(SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION]) AS TOTAL_FUNCTIONS,
		(SELECT COUNT(DISTINCT ROLE_ID) FROM (
			SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_MENU]
			UNION
			SELECT ROLE_ID FROM [dbo].[TB_M_AUTH_FUNCTION]
		) R) AS USED_BY_ROLES,
		(
			(SELECT COUNT(*) FROM [dbo].[TB_M_MENU] WHERE ISNULL(DELETE_FLAG, 0) = 1) +
			(SELECT COUNT(*) FROM [dbo].[TB_M_FUNCTION] WHERE ISNULL(DELETE_FLAG, 0) = 1)
		) AS INACTIVE_COUNT
END
GO
