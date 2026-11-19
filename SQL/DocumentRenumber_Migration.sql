-- =====================================================================
-- Ganti nomor dokumen saat revisi (Organization Change tahap 3, 2026-11-19)
--
-- QMS: after a reorganisation the number does not have to change; it MAY
-- change at a revision, with a reference to the old number. The revision
-- number continues (Rev. 2 -> Rev. 3), only the number changes.
--
-- PREVIOUS_DOCUMENT_CODE = the number the document had before (the reference).
-- A document can be renumbered more than once, so readers follow the chain:
--   fn_Document_CodeLineage(code)  -> the code and every older number of it
--   fn_Document_LatestCode(code)   -> the current number an old number became
-- Used by the revision history, Rev. 0 date, Master List, search, copy
-- register, binders and Needs Action (copies of old revisions to withdraw).
-- Idempotent - safe to run again. Run on DMS_NEW.
-- =====================================================================
IF COL_LENGTH('dbo.TB_R_DOCUMENT', 'PREVIOUS_DOCUMENT_CODE') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT ADD PREVIOUS_DOCUMENT_CODE VARCHAR(50) NULL;
IF COL_LENGTH('dbo.TB_R_DOCUMENT_HISTORY', 'PREVIOUS_DOCUMENT_CODE') IS NULL
	ALTER TABLE dbo.TB_R_DOCUMENT_HISTORY ADD PREVIOUS_DOCUMENT_CODE VARCHAR(50) NULL;
GO

CREATE OR ALTER FUNCTION dbo.fn_Document_CodeLineage (@CODE VARCHAR(255))
RETURNS @R TABLE (CODE VARCHAR(255) PRIMARY KEY, DEPTH INT)
AS
BEGIN
	IF @CODE IS NULL RETURN;
	INSERT INTO @R VALUES (@CODE, 0);
	DECLARE @DEPTH INT = 0, @ADDED INT = 1;
	WHILE @ADDED > 0 AND @DEPTH < 20
	BEGIN
		SET @DEPTH += 1;
		INSERT INTO @R (CODE, DEPTH)
		SELECT DISTINCT P.PREV, @DEPTH
		FROM (
			SELECT D.PREVIOUS_DOCUMENT_CODE AS PREV FROM dbo.TB_R_DOCUMENT D JOIN @R R ON R.CODE = D.DOCUMENT_CODE AND R.DEPTH = @DEPTH - 1
			UNION
			SELECT H.PREVIOUS_DOCUMENT_CODE FROM dbo.TB_R_DOCUMENT_HISTORY H JOIN @R R ON R.CODE = H.DOCUMENT_CODE AND R.DEPTH = @DEPTH - 1
		) P
		WHERE P.PREV IS NOT NULL AND NOT EXISTS (SELECT 1 FROM @R X WHERE X.CODE = P.PREV);
		SET @ADDED = @@ROWCOUNT;
	END
	RETURN;
END
GO

CREATE OR ALTER FUNCTION dbo.fn_Document_LatestCode (@CODE VARCHAR(255))
RETURNS VARCHAR(255)
AS
BEGIN
	DECLARE @CUR VARCHAR(255) = @CODE, @NEXT VARCHAR(255), @I INT = 0;
	WHILE @I < 20
	BEGIN
		SET @NEXT = NULL;
		SELECT TOP 1 @NEXT = D.DOCUMENT_CODE FROM dbo.TB_R_DOCUMENT D
		WHERE D.PREVIOUS_DOCUMENT_CODE = @CUR AND D.DOCUMENT_CODE <> @CUR AND ISNULL(D.DELETE_FLAG, 0) = 0 AND D.[STATUS] IN ('1', '5');
		IF @NEXT IS NULL
			SELECT TOP 1 @NEXT = H.DOCUMENT_CODE FROM dbo.TB_R_DOCUMENT_HISTORY H
			WHERE H.PREVIOUS_DOCUMENT_CODE = @CUR AND H.DOCUMENT_CODE <> @CUR;
		IF @NEXT IS NULL BREAK;
		SET @CUR = @NEXT;
		SET @I += 1;
	END
	RETURN @CUR;
END
GO
