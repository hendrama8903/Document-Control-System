using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DMS.Models.Repo
{
    public class MenuRepo : BaseRepo
    {
        private MenuRepo() { }

        #region Singleton
        private static MenuRepo instance = null;
        public static MenuRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MenuRepo();
                }
                return instance;
            }
        }
        #endregion

        public MenuFunctionStats GetStats(DBContext db)
        {
            string query = "EXEC [dbo].[sp_Menu_Function_Stats]";
            MenuFunctionStats Result = db.MenuFunctionStats.FromSqlRaw<MenuFunctionStats>(query).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public IList<MenuListItem> GetTree(DBContext db)
        {
            string query = "EXEC [dbo].[sp_Menu_Tree]";
            IList<MenuListItem> Result = db.MenuListItem.FromSqlRaw<MenuListItem>(query).ToList();

            return Result;
        }

        public DBResult ToggleStatus(string menuId, int deleteFlag, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@MENU_ID", CheckNullValue(menuId) ),
                new SqlParameter ( "@DELETE_FLAG", deleteFlag ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Menu_ToggleStatus] @MENU_ID, @DELETE_FLAG, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public IList<Menu> GetAll(DBContext db)
        {
            string query = "EXEC [dbo].[sp_Menu_GetAll]";
            IList<Menu> Result = db.Menu.FromSqlRaw<Menu>(query).ToList();

            return Result;
        }

        public IList<Menu> Search(Menu data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_NAME", CheckNullValue(data.MENU_NAME) ),
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_Menu_Search] @MENU_NAME, @PARENT_ID, @PageNumber, @PageSize";
            IList<Menu> Result = db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).ToList();

            return Result;
        }

        public IList<Menu> SearchParent(Menu data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_NAME", CheckNullValue(data.MENU_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };
            string query = "EXEC [dbo].[sp_Menu_Parent_Search] @MENU_NAME, @PageNumber, @PageSize";
            IList<Menu> Result = db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).ToList();

            return Result;
        }
        
        public IList<Menu> SearchChild(Menu data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_NAME", CheckNullValue(data.MENU_NAME) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };
            string query = "EXEC [dbo].[sp_Menu_Child_Search] @MENU_NAME, @PageNumber, @PageSize";
            IList<Menu> Result = db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).ToList();

            return Result;
        }

        public Menu GetByKey(Menu data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) )
            };

            string query = "EXEC [dbo].[sp_Menu_GetByKey] @MENU_ID";
            Menu Result = db.Menu.FromSqlRaw<Menu>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public String GetMenuLastID(Menu data, DBContext db)
        {
            SqlParameter lastID = CreateSqlParameterOutputString("@LAST_ID");

            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                lastID
            };

            string query = "EXEC [dbo].[sp_GetMenuLastID] @PARENT_ID, @LAST_ID OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());
            string result = lastID.Value.ToString();

            return result;
        }

        public String GetMenuLastSeq(Menu data, DBContext db)
        {
            SqlParameter lastSeq = CreateSqlParameterOutputString("@LAST_SEQ");

            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                lastSeq
            };

            string query = "EXEC [dbo].[sp_GetMenuLastSeq] @PARENT_ID, @LAST_SEQ OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());
            string result = lastSeq.Value.ToString();

            return result;
        }

        public DBResult Insert(Menu data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) ),
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                new SqlParameter ( "@MENU_NAME", CheckNullValue(data.MENU_NAME) ),
                new SqlParameter ( "@MENU_ICON", CheckNullValue(data.MENU_ICON) ),
                new SqlParameter ( "@MENU_URL", CheckNullValue(data.MENU_URL) ),
                new SqlParameter ( "@MENU_SEQ", CheckNullValue(data.MENU_SEQ) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Menu_Insert] @MENU_ID, @PARENT_ID, @MENU_NAME, @MENU_ICON, @MENU_URL, @MENU_SEQ, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(Menu data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) ),
                new SqlParameter ( "@PARENT_ID", CheckNullValue(data.PARENT_ID) ),
                new SqlParameter ( "@MENU_NAME", CheckNullValue(data.MENU_NAME) ),
                new SqlParameter ( "@MENU_ICON", CheckNullValue(data.MENU_ICON) ),
                new SqlParameter ( "@MENU_URL", CheckNullValue(data.MENU_URL) ),
                new SqlParameter ( "@MENU_SEQ", CheckNullValue(data.MENU_SEQ) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Menu_Update] @MENU_ID, @PARENT_ID, @MENU_NAME, @MENU_ICON, @MENU_URL, @MENU_SEQ, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(Menu data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@MENU_ID", CheckNullValue(data.MENU_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_Menu_Delete] @MENU_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

    }
}