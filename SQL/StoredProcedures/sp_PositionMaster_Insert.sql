-- =====================================================================
-- Position Master saat ini cuma bisa dibaca (sp_PositionMaster_Search /
-- sp_PositionMaster_GetByKey ada, tapi tidak ada Insert/Update/Delete)
-- dan tidak punya halaman admin sama sekali - beda dengan Department,
-- Section, dan Division yang masing-masing punya Controller + View CRUD
-- lengkap. Data TB_M_POSITION (Staff, Section Head, Dept. Head, dst)
-- selama ini hanya bisa diubah langsung lewat database.
--
-- Fix: tambah 3 stored procedure (Insert/Update/Delete) mengikuti pola
-- sp_SectionMaster_* / sp_DivisionMaster_* (validasi wajib isi, cek
-- duplikat, soft delete via DELETE_FLAG), plus daftarkan menu "Position
-- Master" di bawah "Master Data" (M00002) dan Function POSITIONMASTER-
-- ADD/EDIT/DELETE, di-grant ke role Admin (DMS-ADMIN) dulu supaya bisa
-- langsung dites lalu di-share ke role lain lewat halaman Role
-- Authorization.
--
-- Duplikat hanya dicek berdasarkan POSITION_NAME. POSITION_LEVEL SENGAJA
-- BOLEH berulang di beberapa nama posisi - TB_M_WORKFLOW_DOC_H memetakan
-- rute approval berdasarkan CREATOR_LEVEL (angka), bukan nama/ID posisi,
-- jadi ini memang dipakai untuk kasus "grade sama, sebutan beda" (mis.
-- grade 6 = Supervisor di satu bagian, Specialist di bagian lain; atau
-- Dept. Head yang di beberapa departemen disebut Coordinator) - keduanya
-- otomatis ikut alur approval yang sama selama POSITION_LEVEL-nya sama.
-- POSITION_ID == 5 tetap di-hardcode sebagai Executive Officer di kode
-- C#, jadi khusus ID itu jangan diubah/dihapus.
--
-- Catatan: menu di sidebar dan Function (tombol Add/Edit/dst) diatur
-- oleh DUA tabel otorisasi terpisah - TB_M_AUTH_MENU (menu apa yang
-- tampil di sidebar per role) dan TB_M_AUTH_FUNCTION (tombol apa yang
-- aktif per role). Menu baru harus di-grant ke KEDUANYA.
--
-- Idempotent - aman dijalankan ulang.
-- Jalankan di database DMS_NEW
-- =====================================================================

CREATE OR ALTER PROCEDURE [dbo].[sp_PositionMaster_Insert]
	@POSITION_NAME 			VARCHAR(255),
	@POSITION_LEVEL 		INT,
	@LOGIN_USER 				VARCHAR(255),
	@RETURN_MSG 				VARCHAR(MAX) OUTPUT
AS
BEGIN TRY
	DECLARE @PROCESS_ID BIGINT,
					@LOCATION VARCHAR(255) = 'sp_PositionMaster_Insert';

	EXEC sp_StartLog @PROCESS_ID OUTPUT, 'Position Master', 'Insert', @LOCATION, @LOGIN_USER

	IF @POSITION_NAME IS NULL OR LEN(@POSITION_NAME) < 1
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position Name should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF @POSITION_LEVEL IS NULL
	BEGIN
		SET @RETURN_MSG = 'ERROR: Position Level should not be null';
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	IF EXISTS (SELECT TOP 1 1 FROM [dbo].[TB_M_POSITION]
	WHERE [POSITION_NAME] = @POSITION_NAME
	AND ISNULL([DELETE_FLAG], 0) != 1)
	BEGIN
		SET @RETURN_MSG = 'ERROR: Data already exist Position Name = ' + @POSITION_NAME;
		EXEC sp_WriteLog @PROCESS_ID, '3', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
		RETURN 0;
	END

	INSERT INTO [dbo].[TB_M_POSITION] (
		POSITION_NAME,
		POSITION_LEVEL,
		DELETE_FLAG,
		CREATED_DT,
		CREATED_BY,
		CHANGED_DT,
		CHANGED_BY
	) VALUES (
		@POSITION_NAME,
		@POSITION_LEVEL,
		0,
		GETDATE(),
		@LOGIN_USER,
		GETDATE(),
		@LOGIN_USER
	)

	SET @RETURN_MSG = 'Successfully Save Data'
	EXEC sp_WriteLog @PROCESS_ID, '2', 'INF', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 1;
END TRY
BEGIN CATCH
	SET @RETURN_MSG = 'ERROR: ' + ERROR_PROCEDURE() +': '+ ERROR_MESSAGE() + ', at line = ' +  CAST(ERROR_LINE() AS VARCHAR);
	EXEC sp_WriteLog @PROCESS_ID, '4', 'ERR', @RETURN_MSG, @LOCATION, @LOGIN_USER
	RETURN 0;
END CATCH
GO
