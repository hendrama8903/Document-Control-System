using DMS.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;

namespace DMS.Services
{
    /// <summary>
    /// Where the document stamps go on a PDF page (request Hendra 2026-11-11, Master Data >
    /// Stamp Layout, SQL/StampLayout_Migration.sql). One position per stamp type and page
    /// orientation; a Document Category may have its own set, otherwise the global set applies,
    /// and the built-in defaults (the old fixed positions) cover anything missing.
    /// Positions are percentages of the page, so they fit any paper size.
    /// </summary>
    public static class StampLayoutService
    {
        public const string FunctionEdit = "STAMPLAYOUT-EDIT";

        public const string Master = "MASTER";
        public const string Controlled = "CONTROLLED";
        public const string Uncontrolled = "UNCONTROLLED";
        public const string Obsolete = "OBSOLETE";
        public static readonly string[] StampTypes = { Master, Controlled, Uncontrolled, Obsolete };

        public const string Portrait = "P";
        public const string Landscape = "L";
        public static readonly string[] Orientations = { Portrait, Landscape };

        /// <summary>Printers rarely print the outer 5 mm, so stamps and notes stay inside it.</summary>
        public const double SafeMarginPt = 14.17;
        /// <summary>Smallest and largest stamp width; below the minimum the stamp text is unreadable.</summary>
        public const double MinWidthPt = 100, MaxWidthPt = 340;
        /// <summary>Note lines (copy number, recipient...) under a stamp.</summary>
        public const double NoteGapPt = 3, NoteLineHeightPt = 9;

        public class Position
        {
            public string STAMP_TYPE { get; set; }
            public string ORIENTATION { get; set; }
            public decimal POS_X { get; set; }
            public decimal POS_Y { get; set; }
            public decimal WIDTH_PCT { get; set; }
            /// <summary>
            /// Document positions only (2026-11-11): null = every page of this orientation,
            /// a number = only that page (1-based). Page positions are not inherited by a new revision.
            /// </summary>
            public int? PAGE_NO { get; set; }
        }

        /// <summary>Highest page number and most page positions one document can have.</summary>
        public const int MaxPageNo = 9999, MaxPagePositions = 400;

        public class CategoryItem
        {
            public int DOCUMENT_ID { get; set; }
            public string DOCUMENT_CODE { get; set; }
            public string DOCUMENT_NAME { get; set; }
            public bool IS_ACTIVE { get; set; }
            public bool HAS_CUSTOM { get; set; }
        }

        /// <summary>The old fixed positions (A4, 180 pt wide), same values as the migration seed.</summary>
        public static List<Position> BuiltInDefaults() => new List<Position>
        {
            P(Master, Portrait, 3.360m, 2.376m, 30.238m),
            P(Master, Landscape, 2.376m, 3.360m, 21.380m),
            P(Controlled, Portrait, 34.881m, 92.070m, 30.238m),
            P(Controlled, Landscape, 39.310m, 88.800m, 21.380m),
            P(Uncontrolled, Portrait, 34.881m, 92.070m, 30.238m),
            P(Uncontrolled, Landscape, 39.310m, 88.800m, 21.380m),
            P(Obsolete, Portrait, 34.881m, 93.190m, 30.238m),
            P(Obsolete, Landscape, 39.310m, 90.370m, 21.380m),
        };

        static Position P(string type, string orientation, decimal x, decimal y, decimal w) =>
            new Position { STAMP_TYPE = type, ORIENTATION = orientation, POS_X = x, POS_Y = y, WIDTH_PCT = w };

        /// <summary>The positions used for a page of this orientation, resolved for one document category.</summary>
        public class Layout
        {
            readonly Dictionary<(string, string), Position> positions;
            readonly Dictionary<(string, int), Position> pages;

            public Layout(IEnumerable<Position> positions)
            {
                var list = positions.ToList();
                this.positions = list.Where(p => p.PAGE_NO == null).ToDictionary(p => (p.STAMP_TYPE, p.ORIENTATION));
                pages = list.Where(p => p.PAGE_NO != null).ToDictionary(p => (p.STAMP_TYPE, p.PAGE_NO.Value));
            }

            public static string OrientationOf(double pageWidth, double pageHeight) =>
                pageWidth > pageHeight ? Landscape : Portrait;

            /// <summary>
            /// The stamp rectangle on a page. aspect = image height / width. noteLines reserves
            /// room for the note under the stamp, so stamp and note both stay inside the safe margin.
            /// pageNo (1-based, 0 = unknown) picks the document's position for that page when it has
            /// one of the same orientation; otherwise the position for every page applies.
            /// </summary>
            public XRect Place(string stampType, double pageWidth, double pageHeight, double aspect, int noteLines = 0, int pageNo = 0)
            {
                string orientation = OrientationOf(pageWidth, pageHeight);
                Position p = pageNo > 0 && pages.TryGetValue((stampType, pageNo), out Position own) && own.ORIENTATION == orientation
                    ? own
                    : positions[(stampType, orientation)];

                double usable = Math.Max(1, pageWidth - 2 * SafeMarginPt);
                double width = Clamp((double)p.WIDTH_PCT / 100 * pageWidth, Math.Min(MinWidthPt, usable), Math.Min(MaxWidthPt, usable));
                double height = width * aspect;
                double block = height + (noteLines > 0 ? NoteGapPt + noteLines * NoteLineHeightPt : 0);

                double x = Clamp((double)p.POS_X / 100 * pageWidth, SafeMarginPt, pageWidth - SafeMarginPt - width);
                double y = Clamp((double)p.POS_Y / 100 * pageHeight, SafeMarginPt, pageHeight - SafeMarginPt - block);
                return new XRect(x, y, width, height);
            }

            static double Clamp(double value, double min, double max) =>
                max < min ? min : Math.Max(min, Math.Min(max, value));
        }

        /// <summary>
        /// Draws note lines under a stamp, centred on it but kept inside the safe margin
        /// (a note is often wider than the stamp).
        /// </summary>
        public static void DrawNote(XGraphics gfx, double pageWidth, XRect stamp, IEnumerable<string> lines, XFont font, XBrush brush)
        {
            int i = 0;
            foreach (string line in lines)
            {
                double textWidth = gfx.MeasureString(line, font).Width;
                double x = stamp.X + stamp.Width / 2 - textWidth / 2;
                x = Math.Max(SafeMarginPt, Math.Min(x, pageWidth - SafeMarginPt - textWidth));
                double y = stamp.Bottom + NoteGapPt + i * NoteLineHeightPt;
                gfx.DrawString(line, font, brush, new XRect(x, y, textWidth, NoteLineHeightPt), XStringFormats.TopLeft);
                i++;
            }
        }

        /// <summary>Layout for a document of this category (null = global only).</summary>
        public static Layout ForCategory(DBContext db, int? documentId) => new Layout(Effective(db, documentId));

        /// <summary>
        /// Layout of one document revision: its own positions (chosen on Accept in Document
        /// Registration, stage 2), else its category, else global.
        /// </summary>
        public static Layout ForDocument(DBContext db, int? categoryId, int? documentTransactionId, int? revision)
        {
            List<Position> positions = Effective(db, categoryId);
            if (documentTransactionId != null)
            {
                List<Position> own = LoadDocument(db, documentTransactionId.Value, revision ?? 0);
                var merged = positions.ToDictionary(p => (p.STAMP_TYPE, p.ORIENTATION));
                foreach (Position p in own.Where(p => p.PAGE_NO == null))
                    merged[(p.STAMP_TYPE, p.ORIENTATION)] = p;
                positions = merged.Values.Concat(own.Where(p => p.PAGE_NO != null)).ToList();
            }
            return new Layout(positions);
        }

        /// <summary>Whether a document revision has positions fixed on it.</summary>
        public static bool HasDocument(DBContext db, int documentTransactionId, int revision)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.TB_R_DOCUMENT_STAMP_LAYOUT
                WHERE DOCUMENT_TRANSACTION_ID = @ID AND REVISION = @REVISION) THEN 1 ELSE 0 END";
            command.Parameters.Add(new SqlParameter("@ID", documentTransactionId));
            command.Parameters.Add(new SqlParameter("@REVISION", revision));
            return (int)command.ExecuteScalar() == 1;
        }

        /// <summary>Built-in, then global, then category positions (null = global only).</summary>
        public static List<Position> Effective(DBContext db, int? documentId)
        {
            var merged = BuiltInDefaults().ToDictionary(p => (p.STAMP_TYPE, p.ORIENTATION));
            foreach (Position p in Load(db, null)) merged[(p.STAMP_TYPE, p.ORIENTATION)] = p;
            if (documentId != null)
                foreach (Position p in Load(db, documentId)) merged[(p.STAMP_TYPE, p.ORIENTATION)] = p;
            return merged.Values.ToList();
        }

        /// <summary>Own positions of one document revision; empty when it follows its category.</summary>
        public static List<Position> LoadDocument(DBContext db, int documentTransactionId, int revision)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT STAMP_TYPE, ORIENTATION, POS_X, POS_Y, WIDTH_PCT, PAGE_NO FROM dbo.TB_R_DOCUMENT_STAMP_LAYOUT
                WHERE DOCUMENT_TRANSACTION_ID = @ID AND REVISION = @REVISION
                ORDER BY CASE WHEN PAGE_NO IS NULL THEN 0 ELSE 1 END, PAGE_NO, STAMP_TYPE";
            command.Parameters.Add(new SqlParameter("@ID", documentTransactionId));
            command.Parameters.Add(new SqlParameter("@REVISION", revision));
            return Read(command);
        }

        // Stage 3 (2026-11-11): where a document's positions came from, and the log actions.
        public const string SourceCategory = "CATEGORY";
        public const string SourceCustom = "CUSTOM";
        public const string ActionAccept = "ACCEPT";
        public const string ActionChange = "CHANGE";
        public const string ActionInherit = "INHERIT";

        /// <summary>CATEGORY / CUSTOM of a document revision's stored positions, null when it has none.</summary>
        public static string LoadDocumentSource(DBContext db, int documentTransactionId, int revision)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT TOP 1 SOURCE FROM dbo.TB_R_DOCUMENT_STAMP_LAYOUT
                WHERE DOCUMENT_TRANSACTION_ID = @ID AND REVISION = @REVISION
                ORDER BY CASE SOURCE WHEN 'CUSTOM' THEN 0 ELSE 1 END";
            command.Parameters.Add(new SqlParameter("@ID", documentTransactionId));
            command.Parameters.Add(new SqlParameter("@REVISION", revision));
            return command.ExecuteScalar() as string;
        }

        public class HistoryItem
        {
            public string ACTION { get; set; }
            public string SOURCE { get; set; }
            public int REVISION { get; set; }
            public string REASON { get; set; }
            public string CREATED_BY { get; set; }
            public DateTime CREATED_DT { get; set; }
        }

        /// <summary>Stamp position log of a document revision, newest first.</summary>
        public static List<HistoryItem> History(DBContext db, int documentTransactionId)
        {
            var list = new List<HistoryItem>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT ACTION, SOURCE, REVISION, REASON, CREATED_BY, CREATED_DT
                FROM dbo.TB_R_DOCUMENT_STAMP_LAYOUT_LOG WHERE DOCUMENT_TRANSACTION_ID = @ID ORDER BY LOG_ID DESC";
            command.Parameters.Add(new SqlParameter("@ID", documentTransactionId));
            using var r = command.ExecuteReader();
            while (r.Read())
            {
                list.Add(new HistoryItem
                {
                    ACTION = (string)r["ACTION"],
                    SOURCE = (string)r["SOURCE"],
                    REVISION = (int)r["REVISION"],
                    REASON = r["REASON"] as string,
                    CREATED_BY = r["CREATED_BY"] as string,
                    CREATED_DT = (DateTime)r["CREATED_DT"],
                });
            }
            return list;
        }

        /// <summary>
        /// Fixes the positions of one document revision (a full set) and logs it, in one
        /// transaction. source: CATEGORY = copy of the category layout, CUSTOM = set for this
        /// document. action: ACCEPT or CHANGE (CHANGE needs a reason, checked by the caller).
        /// </summary>
        public static void SaveDocument(DBContext db, int documentTransactionId, int revision, List<Position> positions,
            string source, string action, string reason, string username)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using (var log = connection.CreateCommand())
            {
                log.Transaction = transaction;
                log.CommandText = @"INSERT INTO dbo.TB_R_DOCUMENT_STAMP_LAYOUT_LOG
                    (DOCUMENT_TRANSACTION_ID, REVISION, ACTION, SOURCE, POSITIONS, REASON, CREATED_BY, CREATED_DT)
                    VALUES (@ID, @REVISION, @ACTION, @SOURCE, @POSITIONS, @REASON, @USERNAME, GETDATE())";
                log.Parameters.Add(new SqlParameter("@ID", documentTransactionId));
                log.Parameters.Add(new SqlParameter("@REVISION", revision));
                log.Parameters.Add(new SqlParameter("@ACTION", action));
                log.Parameters.Add(new SqlParameter("@SOURCE", source));
                log.Parameters.Add(new SqlParameter("@POSITIONS", System.Text.Json.JsonSerializer.Serialize(positions)));
                log.Parameters.Add(new SqlParameter("@REASON", (object)reason ?? DBNull.Value));
                log.Parameters.Add(new SqlParameter("@USERNAME", username));
                log.ExecuteNonQuery();
            }

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.TB_R_DOCUMENT_STAMP_LAYOUT WHERE DOCUMENT_TRANSACTION_ID = @ID AND REVISION = @REVISION";
                delete.Parameters.Add(new SqlParameter("@ID", documentTransactionId));
                delete.Parameters.Add(new SqlParameter("@REVISION", revision));
                delete.ExecuteNonQuery();
            }

            foreach (Position p in positions)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = @"INSERT INTO dbo.TB_R_DOCUMENT_STAMP_LAYOUT
                    (DOCUMENT_TRANSACTION_ID, REVISION, STAMP_TYPE, ORIENTATION, POS_X, POS_Y, WIDTH_PCT, PAGE_NO, SOURCE, CREATED_BY, CREATED_DT)
                    VALUES (@ID, @REVISION, @STAMP_TYPE, @ORIENTATION, @POS_X, @POS_Y, @WIDTH_PCT, @PAGE_NO, @SOURCE, @USERNAME, GETDATE())";
                insert.Parameters.Add(new SqlParameter("@PAGE_NO", (object)p.PAGE_NO ?? DBNull.Value));
                insert.Parameters.Add(new SqlParameter("@ID", documentTransactionId));
                insert.Parameters.Add(new SqlParameter("@REVISION", revision));
                insert.Parameters.Add(new SqlParameter("@SOURCE", source));
                insert.Parameters.Add(new SqlParameter("@STAMP_TYPE", p.STAMP_TYPE));
                insert.Parameters.Add(new SqlParameter("@ORIENTATION", p.ORIENTATION));
                insert.Parameters.Add(new SqlParameter("@POS_X", Math.Round(p.POS_X, 3)));
                insert.Parameters.Add(new SqlParameter("@POS_Y", Math.Round(p.POS_Y, 3)));
                insert.Parameters.Add(new SqlParameter("@WIDTH_PCT", Math.Round(p.WIDTH_PCT, 3)));
                insert.Parameters.Add(new SqlParameter("@USERNAME", username));
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        static List<Position> Read(SqlCommand command)
        {
            var list = new List<Position>();
            using var r = command.ExecuteReader();
            int pageColumn = -1;
            for (int i = 0; i < r.FieldCount; i++)
                if (r.GetName(i) == "PAGE_NO") pageColumn = i;
            while (r.Read())
            {
                list.Add(new Position
                {
                    STAMP_TYPE = (string)r["STAMP_TYPE"],
                    ORIENTATION = (string)r["ORIENTATION"],
                    POS_X = (decimal)r["POS_X"],
                    POS_Y = (decimal)r["POS_Y"],
                    WIDTH_PCT = (decimal)r["WIDTH_PCT"],
                    PAGE_NO = pageColumn < 0 || r.IsDBNull(pageColumn) ? null : r.GetInt32(pageColumn),
                });
            }
            return list;
        }

        /// <summary>The stored rows of one scope (null = global). Empty when the category uses the global set.</summary>
        public static List<Position> Load(DBContext db, int? documentId)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = documentId == null
                ? "SELECT STAMP_TYPE, ORIENTATION, POS_X, POS_Y, WIDTH_PCT FROM dbo.TB_M_STAMP_LAYOUT WHERE DOCUMENT_ID IS NULL"
                : "SELECT STAMP_TYPE, ORIENTATION, POS_X, POS_Y, WIDTH_PCT FROM dbo.TB_M_STAMP_LAYOUT WHERE DOCUMENT_ID = @DOCUMENT_ID";
            if (documentId != null) command.Parameters.Add(new SqlParameter("@DOCUMENT_ID", documentId.Value));
            return Read(command);
        }

        /// <summary>Global set, completed with the built-in defaults.</summary>
        public static List<Position> Global(DBContext db) => Effective(db, null);

        /// <summary>
        /// Checks a full set (every stamp type in both orientations, values in range). allowPages:
        /// a document set may also hold positions for single pages (one per stamp and page).
        /// Returns an error message, or null when valid.
        /// </summary>
        public static string Validate(List<Position> positions, bool allowPages = false)
        {
            if (positions == null) return "Stamp positions are missing.";
            var all = positions.Where(p => p.PAGE_NO == null).ToList();
            var pageRows = positions.Where(p => p.PAGE_NO != null).ToList();
            foreach (string type in StampTypes)
                foreach (string orientation in Orientations)
                    if (all.Count(p => p.STAMP_TYPE == type && p.ORIENTATION == orientation) != 1)
                        return "Every stamp needs one position for Portrait and one for Landscape.";
            if (all.Count != StampTypes.Length * Orientations.Length)
                return "Unknown stamp position.";
            if (pageRows.Count > 0)
            {
                if (!allowPages) return "Page positions are only allowed on a document.";
                if (pageRows.Count > MaxPagePositions) return "Too many page positions.";
                if (pageRows.Any(p => !StampTypes.Contains(p.STAMP_TYPE) || !Orientations.Contains(p.ORIENTATION)
                        || p.PAGE_NO < 1 || p.PAGE_NO > MaxPageNo))
                    return "Unknown page position.";
                if (pageRows.GroupBy(p => (p.STAMP_TYPE, p.PAGE_NO)).Any(g => g.Count() > 1))
                    return "A stamp has more than one position for the same page.";
            }
            foreach (Position p in positions)
            {
                if (p.POS_X < 0 || p.POS_X > 100 || p.POS_Y < 0 || p.POS_Y > 100)
                    return "A stamp is outside the page.";
                if (p.WIDTH_PCT < 1 || p.WIDTH_PCT > 100)
                    return "Stamp width is out of range.";
            }
            return null;
        }

        /// <summary>Replaces every row of one scope (null = global) in one transaction.</summary>
        public static void Save(DBContext db, int? documentId, List<Position> positions, string username)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = documentId == null
                    ? "DELETE FROM dbo.TB_M_STAMP_LAYOUT WHERE DOCUMENT_ID IS NULL"
                    : "DELETE FROM dbo.TB_M_STAMP_LAYOUT WHERE DOCUMENT_ID = @DOCUMENT_ID";
                if (documentId != null) delete.Parameters.Add(new SqlParameter("@DOCUMENT_ID", documentId.Value));
                delete.ExecuteNonQuery();
            }

            foreach (Position p in positions)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = @"INSERT INTO dbo.TB_M_STAMP_LAYOUT
                    (DOCUMENT_ID, STAMP_TYPE, ORIENTATION, POS_X, POS_Y, WIDTH_PCT, CREATED_BY, CREATED_DT, CHANGED_BY, CHANGED_DT)
                    VALUES (@DOCUMENT_ID, @STAMP_TYPE, @ORIENTATION, @POS_X, @POS_Y, @WIDTH_PCT, @USERNAME, GETDATE(), @USERNAME, GETDATE())";
                insert.Parameters.Add(new SqlParameter("@DOCUMENT_ID", (object)documentId ?? DBNull.Value));
                insert.Parameters.Add(new SqlParameter("@STAMP_TYPE", p.STAMP_TYPE));
                insert.Parameters.Add(new SqlParameter("@ORIENTATION", p.ORIENTATION));
                insert.Parameters.Add(new SqlParameter("@POS_X", Math.Round(p.POS_X, 3)));
                insert.Parameters.Add(new SqlParameter("@POS_Y", Math.Round(p.POS_Y, 3)));
                insert.Parameters.Add(new SqlParameter("@WIDTH_PCT", Math.Round(p.WIDTH_PCT, 3)));
                insert.Parameters.Add(new SqlParameter("@USERNAME", username));
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        /// <summary>Removes a category's own set, so it follows the global set again.</summary>
        public static void UseGlobal(DBContext db, int documentId)
        {
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM dbo.TB_M_STAMP_LAYOUT WHERE DOCUMENT_ID = @DOCUMENT_ID";
            command.Parameters.Add(new SqlParameter("@DOCUMENT_ID", documentId));
            command.ExecuteNonQuery();
        }

        /// <summary>Document categories that still exist (not deleted), with whether they have their own layout.</summary>
        public static List<CategoryItem> Categories(DBContext db)
        {
            var list = new List<CategoryItem>();
            using var connection = new SqlConnection(db.Database.GetConnectionString());
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"SELECT d.DOCUMENT_ID, d.DOCUMENT_CODE, d.DOCUMENT_NAME, ISNULL(d.DELETE_FLAG, 0) AS DELETE_FLAG,
                    CASE WHEN EXISTS (SELECT 1 FROM dbo.TB_M_STAMP_LAYOUT s WHERE s.DOCUMENT_ID = d.DOCUMENT_ID) THEN 1 ELSE 0 END AS HAS_CUSTOM
                FROM dbo.TB_M_DOCUMENT d
                WHERE ISNULL(d.DELETE_FLAG, 0) <> 2
                ORDER BY d.DOCUMENT_CODE, d.DOCUMENT_NAME";
            using var r = command.ExecuteReader();
            while (r.Read())
            {
                list.Add(new CategoryItem
                {
                    DOCUMENT_ID = (int)r["DOCUMENT_ID"],
                    DOCUMENT_CODE = r["DOCUMENT_CODE"] as string,
                    DOCUMENT_NAME = r["DOCUMENT_NAME"] as string,
                    IS_ACTIVE = (int)r["DELETE_FLAG"] == 0,
                    HAS_CUSTOM = (int)r["HAS_CUSTOM"] == 1,
                });
            }
            return list;
        }

        public static bool CategoryExists(DBContext db, int documentId) =>
            Categories(db).Any(c => c.DOCUMENT_ID == documentId);
    }
}
