-- Fix: FromSqlRaw<ExternalDocument> requires ALL model properties to be present in the
-- result set (EF Core throws "required column ... was not present" otherwise - this is
-- exactly what silently breaks the existing document-review-reminder Hangfire job, which is
-- missing APPROVAL_ID). Add LAST_REVIEW_REMINDER_DT to Search/GetByKey, and make
-- GetDueForReview select the full column set instead of a 7-column subset.

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_ExternalDocument_Search]
    @DOCUMENT_NAME      VARCHAR(255) = NULL,
    @EXTERNAL_TYPE      VARCHAR(255) = NULL,
    @STATUS             INT = NULL,
    @DIVISION           VARCHAR(50) = NULL,
    @DEPARTMENT_ID      INT = NULL,
    @PageNumber         INT = NULL,
    @PageSize           INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH data AS
    (
        SELECT
            ROW_NUMBER() OVER (ORDER BY E.DOCUMENT_NAME ASC) as RowNumber,
            E.EXTERNAL_DOCUMENT_ID,
            E.DOCUMENT_NAME,
            E.REVISION_EDITION,
            E.RECEIVED_DATE,
            E.EXTERNAL_TYPE,
            E.STORAGE_LOCATION,
            E.STORAGE_PIC,
            E.REVIEW_FREQUENCY,
            E.REVIEW_SOURCE,
            E.LAST_CHECK_DATE,
            E.REVIEW_CYCLE_MONTHS,
            E.NEXT_REVIEW_DATE,
            E.LAST_REVIEW_REMINDER_DT,
            E.STATUS,
            SSYS.SYSTEM_VALUE AS STATUS_DISPLAY,
            E.INTERNAL_IMPACT,
            ISYS.SYSTEM_VALUE AS INTERNAL_IMPACT_DISPLAY,
            E.IMPACTED_DOCUMENT_TRANSACTION_ID,
            RD.DOCUMENT_CODE AS IMPACTED_DOCUMENT_CODE,
            RD.DOCUMENT_TRANSACTION_NAME AS IMPACTED_DOCUMENT_NAME,
            E.DIVISION,
            DIV.DIVISION_NAME,
            E.DEPARTMENT_ID,
            DEP.DEPARTMENT_CODE,
            DEP.DEPARTMENT_NAME,
            E.FILE_PATH,
            E.CREATED_BY,
            E.CREATED_DT,
            E.CHANGED_BY,
            E.CHANGED_DT
        FROM [dbo].[TB_M_EXTERNAL_DOCUMENT] E
        LEFT JOIN [dbo].[TB_M_SYSTEM] SSYS ON SSYS.SYSTEM_TYPE = 'EXT_DOC_STATUS' AND SSYS.SYSTEM_CODE = CAST(E.STATUS AS VARCHAR)
        LEFT JOIN [dbo].[TB_M_SYSTEM] ISYS ON ISYS.SYSTEM_TYPE = 'EXT_DOC_IMPACT' AND ISYS.SYSTEM_CODE = CAST(E.INTERNAL_IMPACT AS VARCHAR)
        LEFT JOIN [dbo].[TB_R_DOCUMENT] RD ON RD.DOCUMENT_TRANSACTION_ID = E.IMPACTED_DOCUMENT_TRANSACTION_ID
        LEFT JOIN [dbo].[TB_M_DIVISION] DIV ON DIV.DIVISION_CODE = E.DIVISION
        LEFT JOIN [dbo].[TB_M_DEPARTMENT] DEP ON DEP.DEPARTMENT_ID = E.DEPARTMENT_ID
        WHERE E.DELETE_FLAG = 0
        AND (@DOCUMENT_NAME IS NULL OR E.DOCUMENT_NAME LIKE '%' + @DOCUMENT_NAME + '%')
        AND (@EXTERNAL_TYPE IS NULL OR E.EXTERNAL_TYPE LIKE '%' + @EXTERNAL_TYPE + '%')
        AND (@STATUS IS NULL OR E.STATUS = @STATUS)
        AND (@DIVISION IS NULL OR E.DIVISION = @DIVISION)
        AND (@DEPARTMENT_ID IS NULL OR E.DEPARTMENT_ID = @DEPARTMENT_ID)
    )
    SELECT * FROM data
    WHERE (@PageSize IS NULL OR @PageNumber IS NULL)
    OR (RowNumber > (@PageSize * (@PageNumber - 1)) AND RowNumber <= (@PageSize + (@PageSize * (@PageNumber - 1))))
    ORDER BY RowNumber;
END
GO
