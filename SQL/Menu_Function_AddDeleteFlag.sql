-- Adds a soft-deactivate flag to TB_M_MENU and TB_M_FUNCTION, following the DELETE_FLAG INT
-- (0/1) convention already used on other TB_M_* master tables (TB_M_DEPARTMENT, TB_M_DIVISION,
-- TB_M_POSITION, TB_M_SECTION, etc). Inactive menus/functions are excluded from the real
-- logged-in-user sidebar/functionList (see sp_Auth_Menu / sp_Auth_Function) - not just a
-- cosmetic UI badge.
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'TB_M_MENU' AND COLUMN_NAME = 'DELETE_FLAG'
)
BEGIN
    ALTER TABLE TB_M_MENU ADD DELETE_FLAG INT NOT NULL DEFAULT 0;
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'TB_M_FUNCTION' AND COLUMN_NAME = 'DELETE_FLAG'
)
BEGIN
    ALTER TABLE TB_M_FUNCTION ADD DELETE_FLAG INT NOT NULL DEFAULT 0;
END
