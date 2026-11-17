SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Where every row of a Division / Department / Section Master is used (2026-11-16,
-- SQL/MasterDataRemove_Migration.sql). USED_BY = '' means never used: the grid offers
-- Delete only for those rows. Deleted rows (DELETE_FLAG = 2) are left out.
CREATE OR ALTER PROCEDURE [dbo].[sp_MasterData_Usage]
	@ENTITY  VARCHAR(20)
AS
BEGIN
	SET NOCOUNT ON;

	IF @ENTITY = 'DIVISION'
		SELECT DIVISION_ID AS ID, dbo.fn_MasterData_UsedBy(@ENTITY, DIVISION_ID) AS USED_BY
		FROM dbo.TB_M_DIVISION WHERE ISNULL(DELETE_FLAG, 0) <> 2;
	ELSE IF @ENTITY = 'DEPARTMENT'
		SELECT DEPARTMENT_ID AS ID, dbo.fn_MasterData_UsedBy(@ENTITY, DEPARTMENT_ID) AS USED_BY
		FROM dbo.TB_M_DEPARTMENT WHERE ISNULL(DELETE_FLAG, 0) <> 2;
	ELSE IF @ENTITY = 'SECTION'
		SELECT SECTION_ID AS ID, dbo.fn_MasterData_UsedBy(@ENTITY, SECTION_ID) AS USED_BY
		FROM dbo.TB_M_SECTION WHERE ISNULL(DELETE_FLAG, 0) <> 2;
	ELSE
		THROW 50031, 'Unknown master data type.', 1;
END
GO
