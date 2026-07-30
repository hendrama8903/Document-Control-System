# Stored Procedures — Version-Controlled Snapshot

Setiap stored procedure di database `DMS_NEW` punya satu file `.sql` di folder ini,
bernama sama persis dengan nama SP-nya (mis. `sp_User_Insert.sql`). Ini adalah
**snapshot kondisi terkini** dari setiap SP — bukan riwayat perubahan.

Diekstrak pertama kali: 2026-07-30 (170 stored procedure). Sebelum ini, seluruh
logika bisnis SP hanya hidup di database, tidak ada riwayat perubahan di git sama
sekali (lihat memory `dms-project-architecture.md`).

## Kenapa ini penting untuk sistem QMS

Sebagian besar validasi, alur approval, dan aturan bisnis dokumen ISO ada di dalam
SP ini (bukan di kode C#). Tanpa version control, tidak ada cara untuk:
- Tahu SP mana yang berubah kapan dan oleh siapa
- Review perubahan sebelum diterapkan (code review)
- Rollback ke versi sebelumnya kalau ada bug
- Setup ulang database dari nol dengan logika bisnis yang sama persis

## Cara pakai — WAJIB diikuti untuk setiap perubahan SP

1. **Sebelum ubah SP apa pun di database**, update dulu file `.sql`-nya di folder ini
   (atau buat file baru kalau SP baru).
2. Semua file di sini pakai `CREATE OR ALTER PROCEDURE` (bukan `CREATE PROCEDURE`),
   supaya aman dijalankan berulang kali tanpa perlu DROP dulu.
3. Jalankan file itu ke database (`sqlcmd -S <server> -d DMS_NEW -E -i <file>.sql`)
   untuk menerapkan perubahan.
4. Commit file `.sql` yang berubah ke git **di commit yang sama** dengan perubahan
   kode C# yang terkait (kalau ada), supaya history-nya nyambung.
5. Kalau perubahan berupa migrasi satu-kali yang juga menyentuh data/menu/permission
   (bukan cuma definisi SP), tetap buat script terpisah di `SQL/` (folder induk,
   di luar `StoredProcedures/`) seperti pola yang sudah ada
   (`PositionMaster_AddCrud.sql`, `User_AddRestore.sql`, dst) — folder `SQL/`
   itu riwayat migrasi kronologis, folder `StoredProcedures/` ini snapshot
   kondisi terkini per objek.

## Menjaga snapshot tetap sinkron

Kalau ada SP yang diubah langsung di database tanpa lewat proses di atas (misalnya
lewat SSMS langsung), file di folder ini akan basi/tidak sinkron. Untuk re-sync
penuh (misalnya setelah audit menemukan drift), export ulang semua SP dari database
lalu timpa seluruh isi folder ini.
