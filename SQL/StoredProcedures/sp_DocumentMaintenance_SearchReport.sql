-- =====================================================================
-- Fix: menu Reports > Document Report (sp_DocumentMaintenance_SearchReport)
-- tidak pernah menyertakan NEXT_REVIEW_DATE, padahal ini satu-satunya
-- tujuan link "More Detail" dari KPI card "Due for Review" di Dashboard.
-- Akibatnya staff/QMS klik "More Detail" tapi tidak melihat kolom apapun
-- untuk tahu dokumen mana saja yang sebenarnya due for review.
--
-- Fix: tambahkan D.NEXT_REVIEW_DATE ke SELECT list. Kolom baru ini WAJIB
-- juga ditambahkan ke properti DocumentReport.cs (model yang dipetakan
-- lewat FromSqlRaw<DocumentReport>) - kalau tidak, akan kena bug pola
-- "FromSqlRaw kolom tidak lengkap" yang pernah terjadi di modul lain
-- (lihat SQL/DocumentMaintenance_AddP4DStatusColumn.sql history).
--
-- Idempotent (CREATE OR ALTER). Jalankan di database DMS_NEW.
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_DocumentMaintenance_SearchReport]
  @DOCUMENT_TRANSACTION_ID INT,
  @DOCUMENT_CODE varchar(50),
  @DOCUMENT_TRANSACTION_NAME varchar(255),
  @DIVISION varchar(50),
  @DEPARTMENT_ID INT,
  @DEPARTMENT_CODE VARCHAR(50),
	@YEAR	VARCHAR(4),
	@PageNumber int,
	@PageSize int
AS
BEGIN

	DECLARE @QUERY VARCHAR(MAX)

	SET @QUERY = 'WITH data AS
								(
									SELECT
										ROW_NUMBER() OVER (ORDER BY D.DOCUMENT_TRANSACTION_ID DESC) as RowNumber,
										D.DOCUMENT_CODE,
										D.DOCUMENT_TRANSACTION_ID,
										D.DOCUMENT_TRANSACTION_NAME,
										D.DOCUMENT_DATE,
										D.NEXT_REVIEW_DATE,
										CAST(YEAR(D.DOCUMENT_DATE) as varchar(4)) DOCUMENT_YEAR,
										DIS.DEPARTMENT_ID,
										ISNULL(DIS.DEPARTMENT_CODE, DEP.DEPARTMENT_CODE) AS DEPARTMENT_CODE,
										ISNULL(DIS.DEPARTMENT_NAME, DEP.DEPARTMENT_NAME) AS DEPARTMENT_NAME,
										ISNULL(DIS.DIVISION, DEP.DIVISION) AS DIVISION,
										ISNULL(DIS.DIVISION_NAME, SYS.DIVISION_NAME) AS DIVISION_NAME,
										D.REVISION,
										D.DOCUMENT_CREATOR,
										DIS.DISTRIBUTION_DATE,
										(SELECT TOP 1 1 FROM TB_R_PUBLISH_HISTORY WHERE DOCUMENT_CTRL_ID = CTRL.DOCUMENT_CTRL_ID AND DEPARTMENT_ID = DIS.DEPARTMENT_ID) as PUBLISH_FLAG
									FROM [dbo].[TB_R_DOCUMENT_DISTRIBUTION] DIS
									LEFT JOIN [dbo].[TB_R_DOCUMENT] D ON D.DOCUMENT_TRANSACTION_ID = DIS.DOCUMENT_TRANSACTION_ID
									LEFT JOIN [dbo].[TB_M_DEPARTMENT] DEP ON DEP.DEPARTMENT_ID = DIS.DEPARTMENT_ID
									LEFT JOIN [dbo].[TB_M_DIVISION] SYS ON SYS.DIVISION_CODE = DEP.DIVISION
									LEFT JOIN [dbo].[TB_R_CTRL_DOCUMENT] CTRL ON CTRL.DOCUMENT_TRANSACTION_ID = DIS.DOCUMENT_TRANSACTION_ID
									WHERE 1 = 1 '

									IF @DOCUMENT_TRANSACTION_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND DIS.DOCUMENT_TRANSACTION_ID LIKE ''' + REPLACE(@DOCUMENT_TRANSACTION_ID, '*', '%') + ''' '
									END

									IF @DIVISION IS NOT NULL
									BEGIN
										SET @QUERY += ' AND DEP.DIVISION LIKE ''' + REPLACE(@DIVISION, '*', '%') + ''' '
									END

									IF @DEPARTMENT_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND DIS.DEPARTMENT_ID LIKE ''' + REPLACE(@DEPARTMENT_ID, '*', '%') + ''' '
									END

									IF @DEPARTMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND DEP.DEPARTMENT_CODE LIKE ''' + REPLACE(@DEPARTMENT_CODE, '*', '%') + ''' '
									END

									IF @DOCUMENT_CODE IS NOT NULL
									BEGIN
										SET @QUERY += ' AND D.DOCUMENT_CODE LIKE ''' + REPLACE(@DOCUMENT_CODE, '*', '%') + ''' '
									END

									IF @DOCUMENT_TRANSACTION_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND D.DOCUMENT_TRANSACTION_NAME LIKE ''' + REPLACE(@DOCUMENT_TRANSACTION_NAME, '*', '%') + ''' '
									END

									IF ISNULL(@YEAR, '') <> ''  --@YEAR IS NOT NULL
										BEGIN
										SET @QUERY += ' AND YEAR(D.DOCUMENT_DATE) = ''' + REPLACE(@YEAR , '*', '') + ''' '
									END

	SET @QUERY += '
								)
								SELECT * FROM data
								WHERE 1 = 1 '

	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END

	EXEC(@QUERY)

END
GO
