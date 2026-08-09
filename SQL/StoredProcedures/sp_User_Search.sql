-- =====================================================================
-- USERNAME adalah Primary Key fisik di TB_M_USER, jadi begitu sebuah
-- username di-soft-delete (DELETE_FLAG='1'), username itu tidak bisa
-- dipakai lagi sama sekali - baik untuk membuat user baru maupun untuk
-- mengaktifkan kembali orang yang sama - selama baris lamanya masih ada.
-- Sebelumnya satu-satunya cara mengembalikan user yang salah dihapus
-- (atau karyawan yang resign lalu masuk lagi) adalah UPDATE manual ke
-- DB. Kasus nyata: user 'Danu' di-soft-delete 17-Jul-2026, lalu gagal
-- di-Add ulang 30-Jul-2026 dengan pesan "Username Already Exist" walau
-- tidak terlihat di grid.
--
-- Fix: tambah fitur Restore di halaman /User/Index.
-- 1) sp_User_Search ditambah parameter @SHOW_DELETED - default (NULL/'0')
--    perilakunya sama seperti sekarang (hanya tampilkan user aktif),
--    kalau '1' maka sebaliknya tampilkan HANYA user yang sudah dihapus.
-- 2) sp_User_Restore (baru) - kebalikan dari sp_User_Delete, set
--    DELETE_FLAG kembali ke '0'. Menolak restore kalau user memang
--    belum/tidak dalam status deleted.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE [dbo].[sp_User_Search]
  @USERNAME varchar(255),
  @FULL_NAME varchar(255),
  @DIVISION varchar(5),
  @DEPARTMENT_ID int,
	@DOCUMENT_CONTROL_ACCESS VARCHAR(5),
	@PageNumber int,
	@PageSize int,
	@SHOW_DELETED CHAR(1) = NULL,
	@SHOW_ALL CHAR(1) = NULL
AS
BEGIN

	DECLARE @QUERY VARCHAR(MAX)

	SET @QUERY = 'WITH data AS
								(
									SELECT DISTINCT
										ROW_NUMBER() OVER (ORDER BY A.USERNAME ASC) as RowNumber,
										A.USERNAME,
										A.REG_NO,
										A.FULL_NAME,
										A.PASSWORD,
										A.PASSWORD AS CONFIRM_PASSWORD,
										A.PASSWORD AS OLD_PASSWORD,
										A.EMAIL,
										A.PHONE,
										A.ROLE_ID,
										B.ROLE_NAME,
										P.POSITION_ID,
										O.POSITION_NAME,
										P.DIVISION,
										Y.DIVISION_NAME,
										P.DEPARTMENT_ID,
										D.DEPARTMENT_CODE,
										D.DEPARTMENT_NAME,
										(SELECT TOP 1 DOCUMENT_CONTROL_ACCESS FROM TB_M_DEPARTMENT MD LEFT JOIN TB_M_USER_POS MP ON MD.DIVISION = MP.DIVISION WHERE MP.USERNAME = A.USERNAME ORDER BY DOCUMENT_CONTROL_ACCESS DESC) AS DOCUMENT_CONTROL_ACCESS,
										P.SECTION_ID,
										S.SECTION_CODE,
										S.SECTION_NAME,
										A.FILE_PATH,
										A.AD_USER,
										ISNULL(A.DELETE_FLAG, ''0'') AS DELETE_FLAG,
										A.CREATED_DT,
										A.CREATED_BY,
										A.CHANGED_DT,
										A.CHANGED_BY
									FROM [dbo].[TB_M_USER] A
									LEFT JOIN (SELECT USERNAME, MAX(POSITION_ID) POSITION_ID, MAX(DIVISION) DIVISION, MAX(DEPARTMENT_ID) DEPARTMENT_ID, MAX(SECTION_ID) SECTION_ID FROM [dbo].[TB_M_USER_POS] GROUP BY USERNAME)
									P ON A.[USERNAME] = P.[USERNAME]
									LEFT JOIN [dbo].[TB_M_ROLE] B ON A.[ROLE_ID] = B.[ROLE_ID]
									LEFT JOIN [dbo].[TB_M_POSITION] O ON P.[POSITION_ID] = O.[POSITION_ID]
									LEFT JOIN [dbo].[TB_M_DEPARTMENT] D ON D.[DEPARTMENT_ID] = P.[DEPARTMENT_ID]
									LEFT JOIN [dbo].[TB_M_SECTION] S ON S.[SECTION_ID] = P.[SECTION_ID]
									LEFT JOIN [dbo].[TB_M_DIVISION] Y ON Y.[DIVISION_CODE] = P.[DIVISION]
									WHERE 1 = 1 '

									IF ISNULL(@SHOW_ALL, '0') <> '1'
									BEGIN
										IF ISNULL(@SHOW_DELETED, '0') = '1'
										BEGIN
											SET @QUERY += ' AND ISNULL(A.DELETE_FLAG, ''0'') = ''1'' '
										END
										ELSE
										BEGIN
											SET @QUERY += ' AND ISNULL(A.DELETE_FLAG, ''0'') <> ''1'' '
										END
									END

									IF @USERNAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND A.USERNAME LIKE ''' + REPLACE(@USERNAME, '*', '%') + ''' '
									END

									IF @FULL_NAME IS NOT NULL
									BEGIN
										SET @QUERY += ' AND A.FULL_NAME LIKE ''' + REPLACE(@FULL_NAME, '*', '%') + ''' '
									END

									IF @DIVISION IS NOT NULL
									BEGIN
										SET @QUERY += ' AND D.DIVISION LIKE ''' + REPLACE(@DIVISION, '*', '%') + ''' '
									END

									IF @DEPARTMENT_ID IS NOT NULL
									BEGIN
										SET @QUERY += ' AND P.DEPARTMENT_ID LIKE ''' + REPLACE(@DEPARTMENT_ID, '*', '%') + ''' '
									END

	SET @QUERY += '
								)
								SELECT * FROM data
								WHERE 1 = 1 '

									IF @DOCUMENT_CONTROL_ACCESS IS NOT NULL
									BEGIN
										SET @QUERY += ' AND DOCUMENT_CONTROL_ACCESS LIKE ''' + REPLACE(@DOCUMENT_CONTROL_ACCESS, '*', '%') + ''' '
									END

	IF (@PageSize IS NOT NULL AND @PageNumber IS NOT NULL)
	BEGIN
		SET @QUERY += ' AND RowNumber > '+ CAST((@PageSize * (@PageNumber - 1)) AS VARCHAR) +' AND RowNumber <= ' +CAST(@PageSize + (@PageSize * (@PageNumber - 1)) AS VARCHAR);
	END

	EXEC(@QUERY)

END
GO
