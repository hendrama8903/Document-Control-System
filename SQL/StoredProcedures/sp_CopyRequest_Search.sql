SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_CopyRequest_Search]
	@REQUEST_NO      VARCHAR(50) = NULL,
	@STATUS          CHAR(1) = NULL,
	@USERNAME        VARCHAR(255) = NULL,
	@PageNumber      INT = NULL,
	@PageSize        INT = NULL
AS
BEGIN
	SET NOCOUNT ON;

	-- Tim QMS (department ber-DOCUMENT_CONTROL_ACCESS=1, mis. QPD) harus bisa
	-- lihat & approve request dari SEMUA department, bukan cuma department
	-- sendiri - kalau tidak, mereka tidak akan pernah lihat request yang perlu
	-- di-approve dari department lain (bug ditemukan 2026-08-11: qad23/QPD
	-- tidak melihat request dari department IT sama sekali). Pola sama persis
	-- sp_P4DMaintenance_Search. Requester biasa (bukan QMS) tetap dibatasi
	-- cuma lihat request department-nya sendiri.
	DECLARE @IsQms INT = 0;
	SELECT @IsQms = COUNT(D.DOCUMENT_CONTROL_ACCESS) FROM TB_M_USER_POS P
	LEFT JOIN TB_M_DEPARTMENT D ON D.DIVISION = P.DIVISION
	WHERE P.USERNAME = @USERNAME AND D.DOCUMENT_CONTROL_ACCESS = 1;

	;WITH data AS
	(
		SELECT
			ROW_NUMBER() OVER (ORDER BY H.REQUEST_ID DESC) as RowNumber,
			H.REQUEST_ID,
			H.REQUEST_NO,
			H.DIVISION,
			DIV.DIVISION_NAME,
			H.DEPARTMENT_ID,
			DEP.DEPARTMENT_CODE,
			DEP.DEPARTMENT_NAME,
			H.SECTION_CODE,
			SEC.SECTION_NAME,
			H.REQUEST_DATE,
			H.DOC_CATEGORY,
			H.STATUS,
			SYS.SYSTEM_VALUE AS STATUS_DISPLAY,
			H.REQUESTED_BY,
			H.SUBMITTED_BY,
			H.SUBMITTED_DT,
			H.REMARK,
			H.APPROVED_BY,
			H.APPROVED_DT,
			H.APPROVAL_REMARK,
			H.ACCEPTED_FLAG,
			H.ACCEPTED_BY,
			H.ACCEPTED_DT,
			(SELECT COUNT(*) FROM TB_R_COPY_REQUEST_D D WHERE D.REQUEST_ID = H.REQUEST_ID AND D.DELETE_FLAG = 0) AS DETAIL_COUNT,
			H.CREATED_BY,
			H.CREATED_DT,
			H.CHANGED_BY,
			H.CHANGED_DT
		FROM [dbo].[TB_R_COPY_REQUEST_H] H
		LEFT JOIN [dbo].[TB_M_SYSTEM] SYS ON SYS.SYSTEM_TYPE = 'COPY_REQUEST_STATUS' AND SYS.SYSTEM_CODE = H.STATUS
		LEFT JOIN [dbo].[TB_M_DIVISION] DIV ON DIV.DIVISION_CODE = H.DIVISION
		LEFT JOIN [dbo].[TB_M_DEPARTMENT] DEP ON DEP.DEPARTMENT_ID = H.DEPARTMENT_ID
		-- SECTION_CODE sendirian bukan unique - hanya unik per department (lihat
		-- TB_M_SECTION.DEPARTMENT_CODE) - tanpa syarat department di sini, join-nya
		-- fan-out ke SEMUA section dengan kode sama di department lain juga.
		LEFT JOIN [dbo].[TB_M_SECTION] SEC ON SEC.SECTION_CODE = H.SECTION_CODE AND SEC.DEPARTMENT_CODE = DEP.DEPARTMENT_CODE
		WHERE H.DELETE_FLAG = 0
		AND (@REQUEST_NO IS NULL OR H.REQUEST_NO LIKE '%' + @REQUEST_NO + '%')
		AND (@STATUS IS NULL OR H.STATUS = @STATUS)
		AND (
			@IsQms = 1
			OR @USERNAME IS NULL
			OR H.DEPARTMENT_ID IN (SELECT DEPARTMENT_ID FROM TB_M_USER_POS WHERE USERNAME = @USERNAME)
		)
	)
	SELECT * FROM data
	WHERE (@PageSize IS NULL OR @PageNumber IS NULL)
	OR (RowNumber > (@PageSize * (@PageNumber - 1)) AND RowNumber <= (@PageSize + (@PageSize * (@PageNumber - 1))))
	ORDER BY RowNumber;
END
GO
