SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Jabatan yang bisa muncul di approval chain sebuah Document Category - dipakai
-- pengecekan template di Document Category untuk memperingatkan kotak POS<id>
-- yang jabatannya tidak pernah ada di alur (kotak itu akan selalu kosong).
-- Request Hendra 2026-11-06.
--
-- Untuk tiap level pembuat yang punya alur (default level atau khusus
-- kategori), dipakai alur yang benar-benar berlaku (khusus kategori menang).
-- Jabatan pembuat sendiri juga dihitung (kotak POS pembuat diisi pembuat).
--
-- Hasil: POSITION_ID, POSITION_NAME, MAX_STEPS (langkah terbanyak di antara
-- alur yang berlaku, untuk mengecek kotak STEPn), NEEDS_BOX (1 = jabatan ini
-- approver di alur dan butuh kotak tanda tangan; langkah setingkat pembuat tidak
-- dihitung karena tercetak di kotak pembuat). Request Hendra 2026-11-07.
CREATE OR ALTER PROCEDURE [dbo].[sp_WorkflowDoc_CategoryPositions]
	@DOCUMENT_LEVEL INT,
	@DOCUMENT_ID		INT
AS
BEGIN
	SET NOCOUNT ON;

	;WITH CL AS (
		SELECT DISTINCT CREATOR_LEVEL
		FROM dbo.TB_M_WORKFLOW_DOC_H
		WHERE IS_ACTIVE = 1 AND ((DOCUMENT_ID IS NULL AND DOCUMENT_LEVEL = @DOCUMENT_LEVEL)
		   OR (@DOCUMENT_ID IS NOT NULL AND DOCUMENT_ID = @DOCUMENT_ID))
	),
	WH AS (
		SELECT CL.CREATOR_LEVEL, H.WORKFLOW_DOC_ID
		FROM CL
		CROSS APPLY dbo.fn_WorkflowDoc_FindHeader(@DOCUMENT_LEVEL, @DOCUMENT_ID, CL.CREATOR_LEVEL) H
	),
	POS AS (
		SELECT D.POSITION_ID FROM WH JOIN dbo.TB_M_WORKFLOW_DOC_D D ON D.WORKFLOW_DOC_ID = WH.WORKFLOW_DOC_ID
		UNION
		SELECT P.POSITION_ID FROM dbo.TB_M_POSITION P
		WHERE ISNULL(P.DELETE_FLAG, 0) = 0 AND P.POSITION_LEVEL IN (SELECT CREATOR_LEVEL FROM WH)
	),
	APPROVER_POS AS (
		SELECT DISTINCT D.POSITION_ID
		FROM WH
		JOIN dbo.TB_M_WORKFLOW_DOC_D D ON D.WORKFLOW_DOC_ID = WH.WORKFLOW_DOC_ID
		LEFT JOIN dbo.TB_M_POSITION P ON P.POSITION_ID = D.POSITION_ID
		WHERE ISNULL(P.POSITION_LEVEL, -1) <> WH.CREATOR_LEVEL
	)
	SELECT
		POS.POSITION_ID,
		P.POSITION_NAME,
		(SELECT MAX(N) FROM (SELECT COUNT(*) AS N FROM WH JOIN dbo.TB_M_WORKFLOW_DOC_D D ON D.WORKFLOW_DOC_ID = WH.WORKFLOW_DOC_ID GROUP BY WH.WORKFLOW_DOC_ID) X) AS MAX_STEPS,
		CAST(CASE WHEN POS.POSITION_ID IN (SELECT POSITION_ID FROM APPROVER_POS) THEN 1 ELSE 0 END AS BIT) AS NEEDS_BOX
	FROM POS
	LEFT JOIN dbo.TB_M_POSITION P ON P.POSITION_ID = POS.POSITION_ID
	ORDER BY POS.POSITION_ID;
END
GO
