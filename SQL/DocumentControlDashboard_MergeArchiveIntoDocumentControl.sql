-- Merges Document Archive (ISO) into Distribution Approval, renamed "Document Control":
-- DocumentControlDashboardController.Search no longer filters by OPERATION_TYPE, so it
-- now shows the full document-control register (both user-requested and P4D-registered
-- entries, all statuses) that DocumentArchive used to show a read-only subset of.
--
-- 1) Rename the menu.
-- 2) Grant M00006-04 to the 4 roles that only had M00006-08 (Document Archive), so they
--    don't lose visibility once Archive is removed.
-- 3) Remove Document Archive's menu + role assignments (controller/views dropped in code).

UPDATE TB_M_MENU
SET MENU_NAME = 'Document Control', CHANGED_BY = 'dms.admin', CHANGED_DT = GETDATE()
WHERE MENU_ID = 'M00006-04';

INSERT INTO TB_M_AUTH_MENU (ROLE_ID, MENU_ID, CREATED_BY, CREATED_DT)
SELECT ROLE_ID, 'M00006-04', 'dms.admin', GETDATE()
FROM TB_M_ROLE
WHERE ROLE_ID IN ('DMS-ADMIN-DEPT', 'GENERAL-SECHEAD', 'PIC', 'DeptHead')
AND ROLE_ID NOT IN (SELECT ROLE_ID FROM TB_M_AUTH_MENU WHERE MENU_ID = 'M00006-04');

DELETE FROM TB_M_AUTH_MENU WHERE MENU_ID = 'M00006-08';
DELETE FROM TB_M_MENU WHERE MENU_ID = 'M00006-08';
