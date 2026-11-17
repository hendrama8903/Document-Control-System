-- =====================================================================
-- Former departments after a reorganisation (request Hendra 2026-11-16).
--
-- Policy (agreed with Hendra, ISO 9001 7.5):
--   - A document keeps its number for life, also when the department in it no longer
--     exists (e.g. QCD/SOP-QMS-00/001). It may get a new number only when it is revised,
--     by decision of QMS.
--   - The document is owned by the successor department, and a distribution to the old
--     department is registered to the department that holds the copy now.
--   - Former departments stay in Department Master as inactive, for history; they are not
--     reactivated. Names on existing documents are snapshots, so they keep showing.
--
-- 1. System Parameters type DEPT_SUCCESSOR: SYSTEM_CODE = former department code,
--    SYSTEM_VALUE = current department code(s), comma separated for a split department
--    (the first one becomes the document owner). Maintained by QMS from System Parameters.
--    Read by Document Import > Convert Master List (Services/MasterListConverter).
-- 2. Seeded only where the successor is clear from the names; the others are left for QMS.
-- 3. The 8 former departments reactivated earlier today for the import are made inactive
--    again (their state before, from TB_M_DEPARTMENT_BACKUP_20261116).
--
-- Idempotent - safe to run again. Run on database DMS_NEW.
-- =====================================================================
SET NOCOUNT ON;

MERGE dbo.TB_M_SYSTEM AS t
USING (VALUES
    ('UDT#1', 'UD1'),   -- Unit Drivetrain #1 -> Unit Drivetrain #1 Production Dept.
    ('UDT#2', 'UD2'),   -- Unit Drivetrain #2 -> Unit Drivetrain #2 Production Dept.
    ('SFT',   'SFH')    -- Safety Dept. -> Safety & Health Dept.
) AS s (CODE, VALUE)
ON t.SYSTEM_TYPE = 'DEPT_SUCCESSOR' AND t.SYSTEM_CODE = s.CODE
WHEN NOT MATCHED THEN
    INSERT (SYSTEM_TYPE, SYSTEM_CODE, SYSTEM_VALUE, STATUS, CREATED_BY, CREATED_DT)
    VALUES ('DEPT_SUCCESSOR', s.CODE, s.VALUE, 1, 'dms.admin', GETDATE());
GO

IF OBJECT_ID('dbo.TB_M_DEPARTMENT_BACKUP_20261116', 'U') IS NOT NULL
    UPDATE d
    SET d.DELETE_FLAG = b.DELETE_FLAG,
        d.CHANGED_BY = 'dms.admin',
        d.CHANGED_DT = GETDATE()
    FROM dbo.TB_M_DEPARTMENT d
    JOIN dbo.TB_M_DEPARTMENT_BACKUP_20261116 b ON b.DEPARTMENT_ID = d.DEPARTMENT_ID
    WHERE d.DEPARTMENT_CODE IN ('EIP', 'ESD', 'HEV', 'MUK', 'QDT', 'QMS', 'SFT', 'UPD')
      AND ISNULL(b.DELETE_FLAG, 0) = 1
      AND ISNULL(d.DELETE_FLAG, 0) = 0;
GO
