-- =====================================================================
-- Delete for Division, Department and Section Master (request Hendra 2026-11-16).
--
-- Same rule as Document Category (sp_DocumentMaster_Remove, 2026-11-09):
--   - only data that was NEVER used can be deleted; used data can only be deactivated
--   - still a soft delete: DELETE_FLAG = 2 (Deleted) keeps the row for the audit trail,
--     but it is no longer listed, offered in any selection, or activated again
--   - DELETE_FLAG = 1 keeps meaning Inactive (Deactivate / Activate)
--
-- "Used" is decided in one place, dbo.fn_MasterData_UsedBy, so the grid (which rows may be
-- deleted) and the delete itself never disagree. References by code (division code,
-- department code + section code) count as used even when another row shares the code:
-- better to keep a row than to delete one that documents may point to.
--
-- Run this file, then:
--   SQL/StoredProcedures/sp_MasterData_Usage.sql
--   SQL/StoredProcedures/sp_MasterData_Remove.sql
--   SQL/StoredProcedures/sp_MasterData_Activate.sql        (a Deleted row cannot be activated)
--   SQL/StoredProcedures/sp_DivisionMaster_Search.sql      (Deleted rows never listed)
--   SQL/StoredProcedures/sp_DepartmentMaster_Search.sql
--   SQL/StoredProcedures/sp_SectionMaster_Search.sql
--   SQL/StoredProcedures/sp_DivisionMaster_Insert.sql      (a Deleted code may be used again)
--   SQL/StoredProcedures/sp_DivisionMaster_Update.sql
--   SQL/StoredProcedures/sp_DepartmentMaster_Insert.sql
--   SQL/StoredProcedures/sp_DepartmentMaster_Update.sql
--   SQL/StoredProcedures/sp_SectionMaster_Insert.sql
--   SQL/StoredProcedures/sp_SectionMaster_Update.sql
--   SQL/StoredProcedures/sp_SectionMaster_ListSectionCode.sql        (active only)
--   SQL/StoredProcedures/sp_SectionMaster_ListSectionCodeAndName.sql
--   SQL/StoredProcedures/sp_SectionMaster_ListSectionName.sql
--
-- Idempotent - safe to run again. Run on database DMS_NEW.
-- =====================================================================
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- Where a division / department / section is used, as a readable list
-- ("documents, users, ..."); '' = never used, so it may be deleted.
-- @ENTITY: DIVISION (TB_M_DIVISION.DIVISION_ID), DEPARTMENT (DEPARTMENT_ID), SECTION (SECTION_ID).
CREATE OR ALTER FUNCTION [dbo].[fn_MasterData_UsedBy] (@ENTITY VARCHAR(20), @ID INT)
RETURNS VARCHAR(500)
AS
BEGIN
	DECLARE @USED VARCHAR(500) = '', @CODE VARCHAR(50), @DEPT_CODE VARCHAR(50);

	IF @ENTITY = 'DIVISION'
	BEGIN
		SELECT @CODE = DIVISION_CODE FROM dbo.TB_M_DIVISION WHERE DIVISION_ID = @ID;
		IF @CODE IS NULL RETURN '';

		IF EXISTS (SELECT 1 FROM dbo.TB_M_DEPARTMENT WHERE DIVISION = @CODE AND ISNULL(DELETE_FLAG, 0) <> 2)
			SET @USED += ', departments';
		IF EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS WHERE DIVISION = @CODE)
			SET @USED += ', user positions';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT WHERE DIVISION = @CODE)
		   OR EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DIVISION = @CODE)
			SET @USED += ', documents';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_DISTRIBUTION WHERE DIVISION = @CODE)
		   OR EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_RELATED_DIVISION WHERE DIVISION_CODE = @CODE)
			SET @USED += ', distributions';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_CTRL_DOCUMENT WHERE DIVISION = @CODE)
			SET @USED += ', document registrations';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_COPY_REQUEST_H WHERE DIVISION = @CODE)
			SET @USED += ', print requests';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOC_SUBMISSION_FORM_H WHERE DIVISION = @CODE)
			SET @USED += ', document submissions';
		IF EXISTS (SELECT 1 FROM dbo.TB_M_EXTERNAL_DOCUMENT WHERE DIVISION = @CODE)
			SET @USED += ', external documents';
	END
	ELSE IF @ENTITY = 'DEPARTMENT'
	BEGIN
		SELECT @CODE = DEPARTMENT_CODE FROM dbo.TB_M_DEPARTMENT WHERE DEPARTMENT_ID = @ID;
		IF @CODE IS NULL RETURN '';

		IF EXISTS (SELECT 1 FROM dbo.TB_M_SECTION WHERE DEPARTMENT_CODE = @CODE AND ISNULL(DELETE_FLAG, 0) <> 2)
			SET @USED += ', sections';
		IF EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS WHERE DEPARTMENT_ID = @ID)
			SET @USED += ', user positions';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT WHERE DEPARTMENT = @ID)
		   OR EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_HISTORY WHERE DEPARTMENT = @ID)
			SET @USED += ', documents';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_DISTRIBUTION WHERE DEPARTMENT_ID = @ID)
		   OR EXISTS (SELECT 1 FROM dbo.TB_R_PUBLISH_HISTORY WHERE DEPARTMENT_ID = @ID)
			SET @USED += ', distributions';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_CTRL_DOCUMENT WHERE DEPARTMENT_ID = @ID)
			SET @USED += ', document registrations';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_COPY_REQUEST_H WHERE DEPARTMENT_ID = @ID)
			SET @USED += ', print requests';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOC_SUBMISSION_FORM_H WHERE DEPARTMENT_ID = @ID)
			SET @USED += ', document submissions';
		IF EXISTS (SELECT 1 FROM dbo.TB_M_EXTERNAL_DOCUMENT WHERE DEPARTMENT_ID = @ID)
			SET @USED += ', external documents';
		IF EXISTS (SELECT 1 FROM dbo.TB_M_DOCUMENT_FOLDER WHERE DEPARTMENT_ID = @ID AND ISNULL(DELETE_FLAG, 0) = 0)
			SET @USED += ', binders';
	END
	ELSE IF @ENTITY = 'SECTION'
	BEGIN
		SELECT @CODE = SECTION_CODE, @DEPT_CODE = DEPARTMENT_CODE FROM dbo.TB_M_SECTION WHERE SECTION_ID = @ID;
		IF @CODE IS NULL RETURN '';

		IF EXISTS (SELECT 1 FROM dbo.TB_M_USER_POS WHERE SECTION_ID = @ID)
			SET @USED += ', user positions';
		-- documents keep the department as an id and the section as a code
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT D JOIN dbo.TB_M_DEPARTMENT P ON P.DEPARTMENT_ID = D.DEPARTMENT
		           WHERE D.SECTION_CODE = @CODE AND P.DEPARTMENT_CODE = @DEPT_CODE)
		   OR EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_HISTORY D JOIN dbo.TB_M_DEPARTMENT P ON P.DEPARTMENT_ID = D.DEPARTMENT
		              WHERE D.SECTION_CODE = @CODE AND P.DEPARTMENT_CODE = @DEPT_CODE)
			SET @USED += ', documents';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_CTRL_DOCUMENT WHERE SECTION_CODE = @CODE AND DEPARTMENT_CODE = @DEPT_CODE)
			SET @USED += ', document registrations';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_COPY_REQUEST_H WHERE SECTION_CODE = @CODE AND DEPARTMENT_CODE = @DEPT_CODE)
			SET @USED += ', print requests';
		IF EXISTS (SELECT 1 FROM dbo.TB_R_DOC_SUBMISSION_FORM_H WHERE SECTION_CODE = @CODE AND DEPARTMENT_CODE = @DEPT_CODE)
			SET @USED += ', document submissions';
	END

	RETURN CASE WHEN @USED = '' THEN '' ELSE STUFF(@USED, 1, 2, '') END;
END
GO
