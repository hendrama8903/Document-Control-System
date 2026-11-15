-- Returns the full document folder tree (all rows, unpaged) for the
-- DocumentControlDashboard sidebar - mirrors sp_Menu_Tree's shape.
-- DOCUMENT_COUNT = master documents (STATUS Approved/Published) directly
-- assigned to that folder - NOT summed across subfolders, same "select a
-- folder, see only that folder's documents" behaviour as the mockup.
-- 2026-11-15 (binders, SQL/DocumentBinder_Migration.sql): binder attributes, holder
-- department, and for a controlled copy binder the copies in circulation filed in it
-- (COPY_COUNT) and how many of those belong to a superseded revision (OBSOLETE_COPY_COUNT).
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentFolder_Tree]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		F.FOLDER_ID,
		F.PARENT_ID,
		B.FOLDER_NAME AS PARENT_NAME,
		F.FOLDER_NAME,
		ISNULL((
			SELECT COUNT(*) FROM [dbo].[TB_R_DOCUMENT] D
			WHERE D.FOLDER_ID = F.FOLDER_ID
			AND D.STATUS IN ('1', '5')
			AND ISNULL(D.DELETE_FLAG, 0) = 0
		), 0) AS DOCUMENT_COUNT,
		ISNULL(F.DELETE_FLAG, 0) AS DELETE_FLAG,
		F.CREATED_DT,
		F.CREATED_BY,
		F.CHANGED_DT,
		F.CHANGED_BY,
		F.FOLDER_TYPE,
		F.FOLDER_CODE,
		F.DEPARTMENT_ID,
		DEP.DEPARTMENT_CODE,
		DEP.DEPARTMENT_NAME,
		F.[LOCATION],
		F.REMARKS,
		ISNULL((
			SELECT COUNT(*) FROM [dbo].[TB_R_DOCUMENT_EXPORT] E
			WHERE E.FOLDER_ID = F.FOLDER_ID AND E.PURPOSE = 'CONTROLLED' AND E.WITHDRAWN_DT IS NULL
		), 0) AS COPY_COUNT,
		ISNULL((
			SELECT COUNT(*) FROM [dbo].[TB_R_DOCUMENT_EXPORT] E
			WHERE E.FOLDER_ID = F.FOLDER_ID AND E.PURPOSE = 'CONTROLLED' AND E.WITHDRAWN_DT IS NULL
			AND NOT EXISTS (SELECT 1 FROM [dbo].[TB_R_DOCUMENT] X WHERE X.DOCUMENT_TRANSACTION_ID = E.DOCUMENT_TRANSACTION_ID)
		), 0) AS OBSOLETE_COPY_COUNT
	FROM [dbo].[TB_M_DOCUMENT_FOLDER] F
	LEFT JOIN [dbo].[TB_M_DOCUMENT_FOLDER] B ON F.PARENT_ID = B.FOLDER_ID
	LEFT JOIN [dbo].[TB_M_DEPARTMENT] DEP ON DEP.DEPARTMENT_ID = F.DEPARTMENT_ID
	WHERE ISNULL(F.DELETE_FLAG, 0) = 0
	ORDER BY F.PARENT_ID ASC, F.FOLDER_NAME ASC
END
GO
