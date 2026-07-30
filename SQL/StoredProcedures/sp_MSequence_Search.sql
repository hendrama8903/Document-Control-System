-- ============================================================
-- Document Numbering (Master Sequence) module - Edit-only admin view
-- over the existing TB_M_SEQUENCE table.
-- ============================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_MSequence_Search]
    @SEQ_CODE   VARCHAR(50) = NULL,
    @PageNumber INT = NULL,
    @PageSize   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @QUERY VARCHAR(MAX)

    SET @QUERY = 'WITH data AS
                (
                    SELECT
                        ROW_NUMBER() OVER (ORDER BY SEQ_CODE ASC) as RowNumber,
                        SEQ_TYPE,
                        SEQ_CODE,
                        SEQ_NO,
                        CREATED_BY,
                        CREATED_DT,
                        CHANGED_BY,
                        CHANGED_DT
                    FROM [dbo].[TB_M_SEQUENCE]
                    WHERE 1 = 1'

    IF @SEQ_CODE IS NOT NULL
    BEGIN
        SET @QUERY += ' AND SEQ_CODE LIKE ''' + REPLACE(@SEQ_CODE, '*', '%') + ''' '
    END

    SET @QUERY += '
                )
                SELECT * FROM data
                WHERE 1 = 1 '

    IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
    BEGIN
        SET @QUERY += ' AND RowNumber > ' + CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) + ' AND RowNumber <= ' + CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR)
    END

    EXEC(@QUERY)
END
GO
