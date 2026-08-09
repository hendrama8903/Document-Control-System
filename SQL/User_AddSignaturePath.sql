-- Adds a dedicated digital-signature column to TB_M_USER, separate from the existing
-- FILE_PATH (profile photo). FILE_PATH is left untouched and keeps being used as-is by
-- the existing approval/"Pengesahan" document-stamping feature (DocumentMaintenanceController)
-- - this migration does not change that behavior, it only adds a new, independent field for
-- the redesigned User Profile page's own signature upload.
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'TB_M_USER' AND COLUMN_NAME = 'SIGNATURE_PATH'
)
BEGIN
    ALTER TABLE TB_M_USER ADD SIGNATURE_PATH VARCHAR(255) NULL;
END
