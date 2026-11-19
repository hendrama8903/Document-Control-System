SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Master List matrix (form QCD/FR-QMS-00/004, 2026-11-19): the valid documents of the
-- register with their revision dates and full distribution history, one matrix per
-- owning department. Four result sets, read by MasterListMatrixService:
--   1. documents      the current, valid (Approved / Published) row of each document
--   2. revisions      every revision of those documents (current + TB_R_DOCUMENT_HISTORY)
--   3. distributions  every copy sent (STATUS 1), with the revision it belonged to
--   4. divisions      the active divisions = the distribution columns
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMasterlist_Matrix]
    @DIVISION         VARCHAR(50) = NULL,   -- owning division (NULL = all)
    @DEPARTMENT_CODE  VARCHAR(50) = NULL,   -- owning department (NULL = all)
    @MGMT_SYSTEM      VARCHAR(10) = NULL    -- QMS / K3L / ... (NULL = all, 2026-11-19)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.DOCUMENT_TRANSACTION_ID,
        D.DOCUMENT_CODE,
        D.DOCUMENT_TRANSACTION_NAME,
        DCAT.DOCUMENT_CODE AS CATEGORY_CODE,
        DCAT.LEVEL AS CATEGORY_LEVEL,   -- QMS Level 1-2 = company-wide sheet (2026-11-19)
        D.CLASSIFIED,
        ISNULL(D.REVISION, 0) AS REVISION,
        D.DOCUMENT_DATE,
        D.STATUS,
        D.DIVISION,
        ISNULL(D.DIVISION_NAME, DSYS.DIVISION_NAME) AS DIVISION_NAME,
        ISNULL(D.DEPARTMENT_CODE, DEP.DEPARTMENT_CODE) AS DEPARTMENT_CODE,
        ISNULL(D.DEPARTMENT_NAME, DEP.DEPARTMENT_NAME) AS DEPARTMENT_NAME,
        ISNULL(D.MGMT_SYSTEM, 'QMS') AS MGMT_SYSTEM,
        -- older numbers of a document renumbered at a revision (2026-11-19), newest first
        CASE WHEN D.PREVIOUS_DOCUMENT_CODE IS NULL THEN NULL ELSE
            (SELECT STRING_AGG(LIN.CODE, ', ') WITHIN GROUP (ORDER BY LIN.DEPTH) FROM dbo.fn_Document_CodeLineage(D.DOCUMENT_CODE) LIN WHERE LIN.DEPTH > 0) END AS OLD_CODES
    INTO #DOC
    FROM [dbo].[TB_R_DOCUMENT] D
    LEFT JOIN [dbo].[TB_M_DEPARTMENT] DEP ON DEP.DEPARTMENT_ID = D.DEPARTMENT
    LEFT JOIN [dbo].[TB_M_DIVISION] DSYS ON DSYS.DIVISION_CODE = D.DIVISION
    LEFT JOIN [dbo].[TB_M_DOCUMENT] DCAT ON DCAT.DOCUMENT_ID = D.DOCUMENT_ID
    WHERE ISNULL(D.DELETE_FLAG, 0) = 0
      AND D.STATUS IN ('1', '5')
      AND D.DOCUMENT_CODE IS NOT NULL
      AND (@DIVISION IS NULL OR D.DIVISION = @DIVISION)
      AND (@DEPARTMENT_CODE IS NULL OR ISNULL(D.DEPARTMENT_CODE, DEP.DEPARTMENT_CODE) = @DEPARTMENT_CODE)
      AND (@MGMT_SYSTEM IS NULL OR ISNULL(D.MGMT_SYSTEM, 'QMS') = @MGMT_SYSTEM);

    -- every number of each document (its own + the numbers it had before a renumbering)
    SELECT X.DOCUMENT_CODE AS CUR_CODE, LIN.CODE
    INTO #LIN
    FROM #DOC X CROSS APPLY dbo.fn_Document_CodeLineage(X.DOCUMENT_CODE) LIN;

    -- 1. documents
    SELECT * FROM #DOC ORDER BY DIVISION, DEPARTMENT_CODE, DOCUMENT_CODE;

    -- 2. revisions: the current one first, so it wins when history repeats a number
    SELECT DOCUMENT_CODE, REVISION, DOCUMENT_DATE, ITEM_CHANGED, REASON, IS_CURRENT
    FROM (
        SELECT D.DOCUMENT_CODE, ISNULL(D.REVISION, 0) AS REVISION, D.DOCUMENT_DATE,
               D.ITEM_CHANGED, D.REASON, 1 AS IS_CURRENT
        FROM [dbo].[TB_R_DOCUMENT] D
        JOIN #DOC X ON X.DOCUMENT_TRANSACTION_ID = D.DOCUMENT_TRANSACTION_ID
        UNION ALL
        SELECT L.CUR_CODE, ISNULL(H.REVISION, 0), H.DOCUMENT_DATE,
               H.ITEM_CHANGED, H.REASON, 0
        FROM [dbo].[TB_R_DOCUMENT_HISTORY] H
        JOIN #LIN L ON L.CODE = H.DOCUMENT_CODE
    ) R
    ORDER BY DOCUMENT_CODE, REVISION, IS_CURRENT DESC;

    -- 3. distributions: copies of the current revision and of every superseded one
    --    (a superseded revision keeps its DOCUMENT_TRANSACTION_ID in the history table)
    SELECT T.DOCUMENT_CODE, T.REVISION,
           DD.DIVISION, ISNULL(DD.DEPARTMENT_CODE, DEP.DEPARTMENT_CODE) AS DEPARTMENT_CODE,
           DD.DISTRIBUTION_DATE
    FROM [dbo].[TB_R_DOCUMENT_DISTRIBUTION] DD
    JOIN (
        SELECT DOCUMENT_TRANSACTION_ID, DOCUMENT_CODE, REVISION FROM #DOC
        UNION
        SELECT H.DOCUMENT_TRANSACTION_ID, L.CUR_CODE, ISNULL(H.REVISION, 0)
        FROM [dbo].[TB_R_DOCUMENT_HISTORY] H
        JOIN #LIN L ON L.CODE = H.DOCUMENT_CODE
        WHERE H.DOCUMENT_TRANSACTION_ID IS NOT NULL
    ) T ON T.DOCUMENT_TRANSACTION_ID = DD.DOCUMENT_TRANSACTION_ID
    LEFT JOIN [dbo].[TB_M_DEPARTMENT] DEP ON DEP.DEPARTMENT_ID = DD.DEPARTMENT_ID
    WHERE DD.STATUS = '1'
    ORDER BY T.DOCUMENT_CODE, T.REVISION, DD.DISTRIBUTION_DATE, DEPARTMENT_CODE;

    -- 4. divisions (distribution columns)
    SELECT DIVISION_CODE, DIVISION_NAME
    FROM [dbo].[TB_M_DIVISION]
    WHERE ISNULL(DELETE_FLAG, '0') = '0';

    DROP TABLE #LIN;
    DROP TABLE #DOC;
END
GO
