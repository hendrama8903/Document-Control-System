SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- "Needs Action" counts in Document Control that come from other menus (2026-11-15):
--   REGISTRATION_WAITING  - Document Registration requested or being checked (status 0, 1)
--   DISTRIBUTION_NOT_SENT - registration accepted / partially sent with a department still
--                           ticked but not sent (status 2, 6)
--   PRINT_REQUEST_WAITING - Print Request waiting for approval (status 1)
-- The counts taken from the document register itself (waiting acknowledgment, obsolete
-- copies, review due) are computed in the page from the grid rows.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentControlDashboard_ActionItems]
AS
BEGIN
	SET NOCOUNT ON;

	SELECT
		(SELECT COUNT(*) FROM [dbo].[TB_R_CTRL_DOCUMENT] S
		 WHERE S.OPERATION_TYPE = 1 AND ISNULL(S.DELETE_FLAG, 0) = 0 AND S.[STATUS] IN ('0', '1')) AS REGISTRATION_WAITING,
		(SELECT COUNT(*) FROM [dbo].[TB_R_CTRL_DOCUMENT] S
		 WHERE S.OPERATION_TYPE = 1 AND ISNULL(S.DELETE_FLAG, 0) = 0 AND S.[STATUS] IN ('2', '6')
		   AND EXISTS (SELECT 1 FROM [dbo].[TB_R_DOCUMENT_DISTRIBUTION] DD
		               WHERE DD.DOCUMENT_TRANSACTION_ID = S.DOCUMENT_TRANSACTION_ID AND DD.[STATUS] = 0)) AS DISTRIBUTION_NOT_SENT,
		(SELECT COUNT(*) FROM [dbo].[TB_R_COPY_REQUEST_H] H
		 WHERE ISNULL(H.DELETE_FLAG, 0) = 0 AND H.[STATUS] = '1') AS PRINT_REQUEST_WAITING;
END
GO
