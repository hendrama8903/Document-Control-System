SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Print Document (web) of an approved Print Request line (request Hendra 2026-11-10,
-- replaces the PrintTrack desktop app). Only Document Control prints; the caller checks that.
--
-- One print per line: the first print locks the line (TB_R_COPY_REQUEST_D.PRINT_STATUS = '1').
-- A reprint (paper jam, damaged copy) needs @REPRINT_REASON and is logged as 'Reprint'; the
-- line keeps its first PRINTED_BY / PRINTED_DT. Every print is a row in
-- TB_R_COPY_REQUEST_PRINT_LOG (COMPUTER_NAME 'WEB', COPY_COUNT = copies, ERROR_DETAIL = reason; TOTAL_SHEETS is computed).
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_WebPrint]
	@REQUEST_DETAIL_ID INT,
	@PAGE_COUNT        INT = NULL,
	@REPRINT_REASON    VARCHAR(500) = NULL,
	@LOGIN_USER        VARCHAR(50),
	@RETURN_MSG        VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	SET NOCOUNT ON;
	BEGIN TRANSACTION;

	DECLARE @HeaderStatus CHAR(1), @DetailPrintStatus CHAR(1), @Qty INT;

	SELECT @HeaderStatus = H.STATUS, @DetailPrintStatus = D.PRINT_STATUS, @Qty = D.COPY_QTY
	FROM [dbo].[TB_R_COPY_REQUEST_D] D WITH (UPDLOCK, HOLDLOCK)
	INNER JOIN [dbo].[TB_R_COPY_REQUEST_H] H ON H.REQUEST_ID = D.REQUEST_ID
	WHERE D.REQUEST_DETAIL_ID = @REQUEST_DETAIL_ID AND D.DELETE_FLAG = 0 AND H.DELETE_FLAG = 0;

	IF @HeaderStatus IS NULL
	BEGIN
		ROLLBACK TRANSACTION;
		SET @RETURN_MSG = 'ERROR: Request detail not found';
		RETURN 0;
	END

	IF @HeaderStatus <> '2'
	BEGIN
		ROLLBACK TRANSACTION;
		SET @RETURN_MSG = 'ERROR: The request is not approved';
		RETURN 0;
	END

	DECLARE @IsReprint BIT = CASE WHEN @DetailPrintStatus = '1' THEN 1 ELSE 0 END;
	IF @IsReprint = 1 AND LTRIM(RTRIM(ISNULL(@REPRINT_REASON, ''))) = ''
	BEGIN
		ROLLBACK TRANSACTION;
		SET @RETURN_MSG = 'ERROR: This document has already been printed. Enter a reason to reprint it.';
		RETURN 0;
	END

	INSERT INTO [dbo].[TB_R_COPY_REQUEST_PRINT_LOG]
		(REQUEST_DETAIL_ID, COMPUTER_NAME, PRINTER_NAME, PAGE_COUNT, COPY_COUNT, PRINT_JOB_ID, PRINT_STATUS, ERROR_DETAIL, PRINTED_BY, PRINTED_DT)
	VALUES
		(@REQUEST_DETAIL_ID, 'WEB', NULL, @PAGE_COUNT, @Qty, NULL,
		 CASE WHEN @IsReprint = 1 THEN 'Reprint' ELSE 'Success' END,
		 CASE WHEN @IsReprint = 1 THEN LTRIM(RTRIM(@REPRINT_REASON)) ELSE NULL END,
		 @LOGIN_USER, GETDATE());

	IF @IsReprint = 0
	BEGIN
		UPDATE [dbo].[TB_R_COPY_REQUEST_D]
		SET PRINT_STATUS = '1',
			PRINTED_BY = @LOGIN_USER,
			PRINTED_DT = GETDATE(),
			CHANGED_BY = @LOGIN_USER,
			CHANGED_DT = GETDATE()
		WHERE REQUEST_DETAIL_ID = @REQUEST_DETAIL_ID;
	END

	COMMIT TRANSACTION;
	SET @RETURN_MSG = CASE WHEN @IsReprint = 1 THEN 'Reprint recorded' ELSE 'Print recorded' END;
	RETURN 1;
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	RETURN 0;
END CATCH
GO
