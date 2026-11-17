-- =====================================================================
-- Roles redesigned (request Hendra 2026-11-17): the old roles were leftovers
-- (QPD-specific DeptHead/SecHead/Staff, PIC, DMS-ADMIN-DEPT, GENERAL-*, ZZTEST).
-- At the time of running only dms.admin has a user account (DMS-ADMIN).
--
-- A user has ONE role (TB_M_USER.ROLE_ID), so the roles are cumulative bundles:
--   DMS-ADMIN   Super Administrator   everything; only dms.admin (break-glass account).
--                                     Code relies on this ID (Hangfire, Search, Access
--                                     Rights, fn_User_IsDocumentControl) - keep the ID.
--   SYS-ADMIN   System Administrator  IT: users, roles, organisation master, system
--                                     parameters, logs, access review + daily user rights.
--                                     NOT: Menu & Function, Document Control, numbering.
--   DOC-CONTROL Document Controller   QMS secretariat: registration (Accept/Reject/
--                                     distribution/stamp), Document Control, external
--                                     documents, print approval, numbering, categories,
--                                     workflows, import + approver rights.
--   AUDITOR     Auditor (Read Only)   internal/ISO audit: registers, reports, logs,
--                                     access review; no changes.
--   APPROVER    Approver              Section Head and above: daily user rights +
--                                     DOCUMENT-APPROVE + distribution report.
--   USER        General User          staff: prepare documents, request registration and
--                                     prints, read and acknowledge distributed documents.
-- Kept for DMS-ADMIN only: Menu & Function, Document Numbering reset, Document Request
-- (menu disabled). Which documents a user may OPEN is not set here - that is
-- fn_Document_CanView (distribution / own division / Document Control).
--
-- Backups: TB_M_ROLE / TB_M_AUTH_MENU / TB_M_AUTH_FUNCTION _BACKUP_20261117R.
-- One transaction. Run on database DMS_NEW.
-- =====================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID('dbo.TB_M_ROLE_BACKUP_20261117R', 'U') IS NOT NULL
BEGIN
    RAISERROR('Already run (TB_M_ROLE_BACKUP_20261117R exists).', 16, 1);
    SET NOEXEC ON;
END
GO
IF EXISTS (SELECT 1 FROM dbo.TB_M_USER WHERE ROLE_ID NOT IN ('DMS-ADMIN', 'SYS-ADMIN', 'DOC-CONTROL', 'AUDITOR', 'APPROVER', 'USER'))
BEGIN
    RAISERROR('Some users still have an old role - give them a new role first.', 16, 1);
    SET NOEXEC ON;
END
GO

BEGIN TRANSACTION;

SELECT * INTO dbo.TB_M_ROLE_BACKUP_20261117R          FROM dbo.TB_M_ROLE;
SELECT * INTO dbo.TB_M_AUTH_MENU_BACKUP_20261117R     FROM dbo.TB_M_AUTH_MENU;
SELECT * INTO dbo.TB_M_AUTH_FUNCTION_BACKUP_20261117R FROM dbo.TB_M_AUTH_FUNCTION;

DECLARE @BY VARCHAR(20) = 'dms.admin', @NOW DATETIME = GETDATE();

DELETE FROM dbo.TB_M_AUTH_FUNCTION;
DELETE FROM dbo.TB_M_AUTH_MENU;
DELETE FROM dbo.TB_M_ROLE WHERE ROLE_ID <> 'DMS-ADMIN';

UPDATE dbo.TB_M_ROLE SET ROLE_NAME = 'Super Administrator',
    ROLE_DESC = 'Full access to every menu, function and document. Break-glass account dms.admin only.',
    CHANGED_BY = @BY, CHANGED_DT = @NOW
WHERE ROLE_ID = 'DMS-ADMIN';
IF @@ROWCOUNT = 0
    INSERT dbo.TB_M_ROLE (ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_BY, CREATED_DT)
    VALUES ('DMS-ADMIN', 'Super Administrator', 'Full access to every menu, function and document. Break-glass account dms.admin only.', @BY, @NOW);

INSERT dbo.TB_M_ROLE (ROLE_ID, ROLE_NAME, ROLE_DESC, CREATED_BY, CREATED_DT) VALUES
('SYS-ADMIN',   'System Administrator', 'IT administrator: users, roles, organisation master, system parameters, logs and access review. No document control rights.', @BY, @NOW),
('DOC-CONTROL', 'Document Controller',  'QMS secretariat: document registration, distribution, Document Control register, external documents, print approval, numbering and workflows.', @BY, @NOW),
('AUDITOR',     'Auditor',              'Read only: registers, reports, activity and access logs, access rights review.', @BY, @NOW),
('APPROVER',    'Approver',             'Section Head and above: General User rights plus document approval and distribution report.', @BY, @NOW),
('USER',        'General User',         'Staff: prepare documents, request registration and prints, read and acknowledge distributed documents.', @BY, @NOW);

-- Grants: ROLE_ID + menu ID (view) or function ID (action; its menu is granted with it).
DECLARE @G TABLE (ROLE_ID VARCHAR(20), ID VARCHAR(100));

-- General User
INSERT @G SELECT 'USER', v FROM (VALUES
 ('M00001'), ('M00009'), ('M00006-10'), ('M00008-02'),
 ('DOCUMENT-ADD'), ('DOCUMENT-EDIT'), ('DOCUMENT-DELETE'), ('DOCUMENT-DOWNLOAD'), ('DOCUMENTMASTER-DOWNLOAD-FILEPATH'),
 ('P4D-ADD'), ('P4D-DOWNLOAD'),
 ('USERDASHBOARD-DOWNLOAD'), ('USERDASHBOARD-PUBLISH'), ('USERDASHBOARD-REQUEST'),
 ('M00006-07'), ('EXTERNALDOCUMENT-DOWNLOAD-FILEPATH'),
 ('COPYREQUEST-ADD'), ('COPYREQUEST-EDIT'), ('COPYREQUEST-DELETE'), ('COPYREQUEST-SUBMIT')
) x(v);

-- Approver = General User + approval
INSERT @G SELECT 'APPROVER', ID FROM @G WHERE ROLE_ID = 'USER';
INSERT @G VALUES ('APPROVER', 'DOCUMENT-APPROVE'), ('APPROVER', 'M00008-01');

-- Document Controller = Approver + document control
INSERT @G SELECT 'DOC-CONTROL', ID FROM @G WHERE ROLE_ID = 'APPROVER';
INSERT @G SELECT 'DOC-CONTROL', v FROM (VALUES
 ('P4D-EDIT'), ('P4D-DELETE'), ('P4D-SEND'), ('P4D-RECEIVE'), ('P4D-UNRECEIVE'), ('P4D-REJECT'), ('P4D-DISTRIBUTION'), ('P4D-STAMPLAYOUT'),
 ('M00006-04'), ('DOCUMENTCONTROLDASHBOARD-EXPORTPDF'), ('DOCUMENTCONTROLDASHBOARD-SEND'),
 ('EXTERNALDOCUMENT-ADD'), ('EXTERNALDOCUMENT-EDIT'), ('EXTERNALDOCUMENT-DELETE'), ('EXTERNALDOCUMENT-DELETE-FILEPATH'),
 ('COPYREQUEST-APPROVE'), ('COPYREQUEST-PRINT'),
 ('M00008-03'),
 ('DOCUMENTMASTER-ADD'), ('DOCUMENTMASTER-EDIT'), ('DOCUMENTMASTER-DELETE'), ('DOCUMENTMASTER-DELETE-FILEPATH'),
 ('WORKFLOWDOC-ADD'), ('WORKFLOWDOC-EDIT'), ('WORKFLOWDOC-DELETE'),
 ('MSEQUENCE-EDIT'), ('STAMPLAYOUT-EDIT'), ('SIGNATURESETTING-EDIT'),
 ('LEGACYDOCUMENTIMPORT-IMPORT'), ('REASSIGN-EXECUTE'),
 ('M00005'), ('M00003-06')
) x(v);

-- System Administrator = General User + administration
INSERT @G SELECT 'SYS-ADMIN', ID FROM @G WHERE ROLE_ID = 'USER';
INSERT @G SELECT 'SYS-ADMIN', v FROM (VALUES
 ('USER-ADD'), ('USER-EDIT'), ('USER-DELETE'), ('USER-RESTORE'), ('USER-POSITION'),
 ('ROLE-ADD'), ('ROLE-EDIT'), ('ROLE-DELETE'), ('ROLE-AUTHORIZATION'),
 ('DIVISIONMASTER-ADD'), ('DIVISIONMASTER-EDIT'), ('DIVISIONMASTER-DELETE'),
 ('DEPARTMENTMASTER-ADD'), ('DEPARTMENTMASTER-EDIT'), ('DEPARTMENTMASTER-DELETE'),
 ('SECTIONMASTER-ADD'), ('SECTIONMASTER-EDIT'), ('SECTIONMASTER-DELETE'),
 ('POSITIONMASTER-ADD'), ('POSITIONMASTER-EDIT'), ('POSITIONMASTER-DELETE'),
 ('SYSTEMMASTER-ADD'), ('SYSTEMMASTER-EDIT'), ('SYSTEMMASTER-DELETE'),
 ('NOTIFICATIONSETTING-TOGGLE'),
 ('M00003-08'), ('M00005'), ('M00003-06')
) x(v);

-- Auditor: read only
INSERT @G SELECT 'AUDITOR', v FROM (VALUES
 ('M00001'), ('M00009'), ('M00006-10'), ('M00006-06'), ('USERDASHBOARD-DOWNLOAD'), ('USERDASHBOARD-PUBLISH'),
 ('M00006-04'), ('M00006-07'), ('EXTERNALDOCUMENT-DOWNLOAD-FILEPATH'),
 ('M00008-01'), ('M00008-02'), ('M00008-03'),
 ('M00005'), ('M00003-06'), ('M00003-08')
) x(v);

-- Super Administrator: everything
INSERT @G SELECT 'DMS-ADMIN', MENU_ID FROM dbo.TB_M_MENU;
INSERT @G SELECT 'DMS-ADMIN', FUNCTION_ID FROM dbo.TB_M_FUNCTION;

-- Guard against typos: every ID must be a menu or a function.
IF EXISTS (SELECT 1 FROM @G g WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU m WHERE m.MENU_ID = g.ID)
                                AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION f WHERE f.FUNCTION_ID = g.ID))
BEGIN
    SELECT DISTINCT ID AS UNKNOWN_ID FROM @G g WHERE NOT EXISTS (SELECT 1 FROM dbo.TB_M_MENU m WHERE m.MENU_ID = g.ID)
                                                 AND NOT EXISTS (SELECT 1 FROM dbo.TB_M_FUNCTION f WHERE f.FUNCTION_ID = g.ID);
    RAISERROR('Unknown menu/function ID in grants.', 16, 1);
END

INSERT dbo.TB_M_AUTH_FUNCTION (ROLE_ID, FUNCTION_ID, CREATED_BY, CREATED_DT)
SELECT DISTINCT g.ROLE_ID, g.ID, @BY, @NOW FROM @G g JOIN dbo.TB_M_FUNCTION f ON f.FUNCTION_ID = g.ID;

-- Menus: granted directly, through a function, and their parent menus.
;WITH m AS (
    SELECT g.ROLE_ID, g.ID AS MENU_ID FROM @G g JOIN dbo.TB_M_MENU m ON m.MENU_ID = g.ID
    UNION SELECT g.ROLE_ID, f.MENU_ID FROM @G g JOIN dbo.TB_M_FUNCTION f ON f.FUNCTION_ID = g.ID
), p AS (
    SELECT ROLE_ID, MENU_ID FROM m
    UNION SELECT m.ROLE_ID, mm.PARENT_ID FROM m JOIN dbo.TB_M_MENU mm ON mm.MENU_ID = m.MENU_ID WHERE mm.PARENT_ID IS NOT NULL
)
INSERT dbo.TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT DISTINCT ROLE_ID, MENU_ID, @BY, @NOW FROM p;

COMMIT TRANSACTION;

SELECT r.ROLE_ID, r.ROLE_NAME,
       (SELECT COUNT(*) FROM dbo.TB_M_AUTH_MENU a WHERE a.ROLE_ID = r.ROLE_ID) MENUS,
       (SELECT COUNT(*) FROM dbo.TB_M_AUTH_FUNCTION a WHERE a.ROLE_ID = r.ROLE_ID) FUNCTIONS
FROM dbo.TB_M_ROLE r ORDER BY MENUS DESC;
GO
SET NOEXEC OFF;
GO
