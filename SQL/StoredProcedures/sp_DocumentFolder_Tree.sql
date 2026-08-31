-- Returns the full document folder tree (all rows, unpaged) for the
-- DocumentControlDashboard sidebar - mirrors sp_Menu_Tree's shape.
-- DOCUMENT_COUNT = master documents (STATUS Approved/Published) directly
-- assigned to that folder - NOT summed across subfolders, same "select a
-- folder, see only that folder's documents" behaviour as the mockup.
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
		F.CHANGED_BY
	FROM [dbo].[TB_M_DOCUMENT_FOLDER] F
	LEFT JOIN [dbo].[TB_M_DOCUMENT_FOLDER] B ON F.PARENT_ID = B.FOLDER_ID
	WHERE ISNULL(F.DELETE_FLAG, 0) = 0
	ORDER BY F.PARENT_ID ASC, F.FOLDER_NAME ASC
END
GO
