-- ============================================================
-- sp_ExternalDocument_MarkReviewReminded
-- ============================================================
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_ExternalDocument_MarkReviewReminded]
    @EXTERNAL_DOCUMENT_ID INT
AS
BEGIN
    UPDATE [dbo].[TB_M_EXTERNAL_DOCUMENT]
    SET LAST_REVIEW_REMINDER_DT = GETDATE()
    WHERE EXTERNAL_DOCUMENT_ID = @EXTERNAL_DOCUMENT_ID
END
GO
