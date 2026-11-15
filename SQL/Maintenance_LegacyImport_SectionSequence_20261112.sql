-- ============================================================
-- 2026-11-12: tidy documents brought in by Legacy Document Import before the
-- importer filled Section and raised the DOC_NO counter
-- (sp_DocumentMaintenance_ImportLegacyInsert / ImportLegacyHistoryInsert).
--   1. TB_M_SEQUENCE: each DOC_NO counter is raised (never lowered) to the highest
--      number already used by an active document with that prefix.
--   2. Section code/name filled from the code for level 3/4 migrated documents
--      ("ITD/SOP-APP-00/001" -> "00").
--   3. Migrated history rows get SOURCE = 'MIGRATION', like their active rows.
-- Safe to run more than once.
-- ============================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

;WITH codes AS (
	SELECT DOCUMENT_CODE,
		   CHARINDEX('/', REVERSE(DOCUMENT_CODE)) AS lastSlash
	FROM TB_R_DOCUMENT
	WHERE ISNULL(DELETE_FLAG, 0) = 0
), parts AS (
	SELECT LEFT(DOCUMENT_CODE, LEN(DOCUMENT_CODE) - lastSlash) AS prefix,
		   RIGHT(DOCUMENT_CODE, lastSlash - 1) AS numPart
	FROM codes WHERE lastSlash > 1
), maxes AS (
	SELECT prefix, MAX(CAST(numPart AS INT)) AS maxNo
	FROM parts
	WHERE numPart NOT LIKE '%[^0-9]%' AND LEN(numPart) BETWEEN 1 AND 3
	GROUP BY prefix
)
SELECT * INTO #maxes FROM maxes;

UPDATE s SET SEQ_NO = m.maxNo, CHANGED_BY = 'System', CHANGED_DT = GETDATE()
FROM TB_M_SEQUENCE s JOIN #maxes m ON s.SEQ_TYPE = 'DOC_NO' AND s.SEQ_CODE = m.prefix
WHERE s.SEQ_NO < m.maxNo;
PRINT CONCAT('Counters raised: ', @@ROWCOUNT);

INSERT INTO TB_M_SEQUENCE (SEQ_TYPE, SEQ_CODE, SEQ_NO, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT)
SELECT 'DOC_NO', m.prefix, m.maxNo, 'System', GETDATE(), 'System', GETDATE()
FROM #maxes m
WHERE NOT EXISTS (SELECT 1 FROM TB_M_SEQUENCE s WHERE s.SEQ_TYPE = 'DOC_NO' AND s.SEQ_CODE = m.prefix);
PRINT CONCAT('Counters added: ', @@ROWCOUNT);

-- Section for migrated active documents
UPDATE d SET SECTION_CODE = x.sec, SECTION_NAME = sec.SECTION_NAME
FROM TB_R_DOCUMENT d
CROSS APPLY (SELECT LEFT(d.DOCUMENT_CODE, LEN(d.DOCUMENT_CODE) - CHARINDEX('/', REVERSE(d.DOCUMENT_CODE))) AS prefix) p
CROSS APPLY (SELECT RIGHT(p.prefix, CHARINDEX('-', REVERSE(p.prefix)) - 1) AS sec) x
LEFT JOIN TB_M_SECTION sec ON sec.SECTION_CODE = x.sec AND sec.DEPARTMENT_CODE = d.DEPARTMENT_CODE
WHERE d.SOURCE = 'MIGRATION' AND d.LEVEL_CODE IN (3, 4) AND d.SECTION_CODE IS NULL
  AND CHARINDEX('/', REVERSE(d.DOCUMENT_CODE)) > 1 AND CHARINDEX('-', d.DOCUMENT_CODE) > 0;
PRINT CONCAT('Active documents given a Section: ', @@ROWCOUNT);

-- Migrated history rows: no transaction ever existed for them
UPDATE h SET SECTION_CODE = ISNULL(h.SECTION_CODE, x.sec),
			 SECTION_NAME = ISNULL(h.SECTION_NAME, sec.SECTION_NAME),
			 SOURCE = 'MIGRATION'
FROM TB_R_DOCUMENT_HISTORY h
CROSS APPLY (SELECT LEFT(h.DOCUMENT_CODE, LEN(h.DOCUMENT_CODE) - CHARINDEX('/', REVERSE(h.DOCUMENT_CODE))) AS prefix) p
CROSS APPLY (SELECT CASE WHEN h.LEVEL_CODE IN (3, 4) AND CHARINDEX('-', p.prefix) > 0
						 THEN RIGHT(p.prefix, CHARINDEX('-', REVERSE(p.prefix)) - 1) END AS sec) x
LEFT JOIN TB_M_SECTION sec ON sec.SECTION_CODE = x.sec AND sec.DEPARTMENT_CODE = h.DEPARTMENT_CODE
WHERE h.DOCUMENT_TRANSACTION_ID IS NULL AND h.APPROVAL_ID IS NULL AND h.STATUS = '4'
  AND CHARINDEX('/', REVERSE(h.DOCUMENT_CODE)) > 1
  AND (h.SOURCE IS NULL OR h.SECTION_CODE IS NULL);
PRINT CONCAT('History rows tidied: ', @@ROWCOUNT);

-- 4. DOCUMENT_TYPE (01 New / 02 Revision) and EXTERNAL_FLAG were left NULL by the import,
--    so the User Dashboard and P4D Maintenance (inner join on DOCUMENT_TYPE) did not show
--    migrated documents to their recipients.
UPDATE TB_R_DOCUMENT SET DOCUMENT_TYPE = IIF(REVISION > 0, '02', '01'), EXTERNAL_FLAG = ISNULL(EXTERNAL_FLAG, '0')
WHERE SOURCE = 'MIGRATION' AND DOCUMENT_TYPE IS NULL;
PRINT CONCAT('Active documents given a Document Type: ', @@ROWCOUNT);

UPDATE c SET DOCUMENT_TYPE = d.DOCUMENT_TYPE, EXTERNAL_FLAG = ISNULL(c.EXTERNAL_FLAG, d.EXTERNAL_FLAG)
FROM TB_R_CTRL_DOCUMENT c JOIN TB_R_DOCUMENT d ON d.DOCUMENT_TRANSACTION_ID = c.DOCUMENT_TRANSACTION_ID
WHERE d.SOURCE = 'MIGRATION' AND c.DOCUMENT_TYPE IS NULL;
PRINT CONCAT('P4D registrations given a Document Type: ', @@ROWCOUNT);

UPDATE TB_R_DOCUMENT_HISTORY SET DOCUMENT_TYPE = IIF(REVISION > 0, '02', '01'), EXTERNAL_FLAG = ISNULL(EXTERNAL_FLAG, '0')
WHERE SOURCE = 'MIGRATION' AND DOCUMENT_TYPE IS NULL;
PRINT CONCAT('History rows given a Document Type: ', @@ROWCOUNT);

DROP TABLE #maxes;
COMMIT;
