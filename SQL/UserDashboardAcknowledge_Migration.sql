-- Acknowledge per receiving department (bug found by Hendra 2026-11-10).
--
-- Before: a document has one registration row in TB_R_CTRL_DOCUMENT (the creator's
-- department). My Documents sent that row's DEPARTMENT_ID when a user clicked Acknowledge,
-- so every acknowledgement was recorded for the creator's department, whoever clicked.
-- Other target departments could never be recorded, so the document never reached
-- Published (Effective), and one click showed "Accepted" to every recipient.
--
-- Now the department is decided on the server: the sent distribution targets
-- (TB_R_DOCUMENT_DISTRIBUTION.STATUS = 1) that the logged-in user belongs to. Same rule as
-- the visibility in sp_UserDashboard_Search: the user's departments in TB_M_USER_POS, or,
-- for a user with only a division, the departments of that division. A user without a
-- position sees distributed documents but is not a recipient and cannot acknowledge.
--
-- Run this file, then SQL/StoredProcedures/sp_UserDashboard_Acknowledge.sql and
-- SQL/StoredProcedures/sp_UserDashboard_Search.sql.
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER FUNCTION [dbo].[fn_User_RecipientDepartments]
(
	@USERNAME VARCHAR(255),
	@DOCUMENT_TRANSACTION_ID INT
)
RETURNS TABLE
AS
RETURN
	SELECT DD.DEPARTMENT_ID,
		CAST(CASE WHEN EXISTS (
			SELECT 1 FROM [dbo].[TB_R_PUBLISH_HISTORY] PH
			JOIN [dbo].[TB_R_CTRL_DOCUMENT] CT ON CT.DOCUMENT_CTRL_ID = PH.DOCUMENT_CTRL_ID
			WHERE CT.DOCUMENT_TRANSACTION_ID = DD.DOCUMENT_TRANSACTION_ID
			AND PH.DEPARTMENT_ID = DD.DEPARTMENT_ID
		) THEN 1 ELSE 0 END AS BIT) AS ACKNOWLEDGED,
		(SELECT MAX(PH.CREATED_DT) FROM [dbo].[TB_R_PUBLISH_HISTORY] PH
			JOIN [dbo].[TB_R_CTRL_DOCUMENT] CT ON CT.DOCUMENT_CTRL_ID = PH.DOCUMENT_CTRL_ID
			WHERE CT.DOCUMENT_TRANSACTION_ID = DD.DOCUMENT_TRANSACTION_ID
			AND PH.DEPARTMENT_ID = DD.DEPARTMENT_ID) AS ACKNOWLEDGE_DT
	FROM [dbo].[TB_R_DOCUMENT_DISTRIBUTION] DD
	WHERE DD.DOCUMENT_TRANSACTION_ID = @DOCUMENT_TRANSACTION_ID
	AND DD.STATUS = 1
	AND (
		DD.DEPARTMENT_ID IN (
			SELECT P.DEPARTMENT_ID FROM [dbo].[TB_M_USER_POS] P
			WHERE P.USERNAME = @USERNAME AND P.DEPARTMENT_ID IS NOT NULL
		)
		OR (
			NOT EXISTS (
				SELECT 1 FROM [dbo].[TB_M_USER_POS] P
				WHERE P.USERNAME = @USERNAME AND P.DEPARTMENT_ID IS NOT NULL
			)
			AND DD.DEPARTMENT_ID IN (
				SELECT D.DEPARTMENT_ID FROM [dbo].[TB_M_DEPARTMENT] D
				WHERE D.DIVISION IN (
					SELECT P.DIVISION FROM [dbo].[TB_M_USER_POS] P
					WHERE P.USERNAME = @USERNAME AND ISNULL(P.DIVISION, '') <> ''
				)
			)
		)
	)
GO
