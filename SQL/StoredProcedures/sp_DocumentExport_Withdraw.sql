SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Mark a controlled copy as withdrawn (Copy Register, 2026-11-11). Once only: a copy that is
-- already withdrawn, or an export that is not a controlled copy, is refused.
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentExport_Withdraw]
	@EXPORT_ID  INT,
	@NOTE       NVARCHAR(500) = NULL,
	@USERNAME   VARCHAR(50)
AS
BEGIN
	SET NOCOUNT ON;
	SET @NOTE = NULLIF(LTRIM(RTRIM(@NOTE)), '');

	UPDATE [dbo].[TB_R_DOCUMENT_EXPORT]
	SET WITHDRAWN_BY = @USERNAME,
		WITHDRAWN_DT = GETDATE(),
		WITHDRAW_NOTE = @NOTE
	WHERE EXPORT_ID = @EXPORT_ID
	  AND PURPOSE = 'CONTROLLED'
	  AND WITHDRAWN_DT IS NULL;

	IF @@ROWCOUNT = 0
		THROW 50003, 'This copy was not found or is already withdrawn.', 1;
END
GO
