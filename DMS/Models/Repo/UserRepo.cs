using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class UserRepo : BaseRepo
    {
        private UserRepo() { }

        #region Singleton
        private static UserRepo instance = null;
        public static UserRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new UserRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<User> Search(User data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@FULL_NAME", CheckNullValue(data.FULL_NAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DOCUMENT_CONTROL_ACCESS", CheckNullValue(data.DOCUMENT_CONTROL_ACCESS) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_User_Search] @USERNAME, @FULL_NAME, @DIVISION, @DEPARTMENT_ID, @DOCUMENT_CONTROL_ACCESS, @PageNumber, @PageSize";
            IList<User> Result = db.User.FromSqlRaw<User>(query, param.ToArray()).ToList();

            return Result;
        }

        public User GetByKey(User data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) )
            };

            string query = "EXEC [dbo].[sp_User_GetByKey] @USERNAME";
            User Result = db.User.FromSqlRaw<User>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public DBResult Insert(User data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@REG_NO", CheckNullValue(data.REG_NO) ),
                new SqlParameter ( "@FULL_NAME", CheckNullValue(data.FULL_NAME) ),
                new SqlParameter ( "@PASSWORD", CheckNullValue(data.PASSWORD) ),
                new SqlParameter ( "@CONFIRM_PASSWORD", CheckNullValue(data.CONFIRM_PASSWORD) ),
                new SqlParameter ( "@EMAIL", CheckNullValue(data.EMAIL) ),
                new SqlParameter ( "@PHONE", CheckNullValue(data.PHONE) ),
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@AD_USER", CheckNullValue(data.AD_USER) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_User_Insert] @USERNAME, @REG_NO, @FULL_NAME, @PASSWORD, @CONFIRM_PASSWORD, @EMAIL, @PHONE, @ROLE_ID, @DEPARTMENT_ID, @FILE_PATH, @AD_USER, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Update(User data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@REG_NO", CheckNullValue(data.REG_NO) ),
                new SqlParameter ( "@FULL_NAME", CheckNullValue(data.FULL_NAME) ),
                //new SqlParameter ( "@PASSWORD", CheckNullValue(data.PASSWORD) ),
                //new SqlParameter ( "@CONFIRM_PASSWORD", CheckNullValue(data.CONFIRM_PASSWORD) ),
                new SqlParameter ( "@EMAIL", CheckNullValue(data.EMAIL) ),
                new SqlParameter ( "@PHONE", CheckNullValue(data.PHONE) ),
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@AD_USER", CheckNullValue(data.AD_USER) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_User_Update] @USERNAME, @REG_NO, @FULL_NAME, @EMAIL, @PHONE, @ROLE_ID, @DEPARTMENT_ID, @FILE_PATH, @AD_USER, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult UpdatePassword(User data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@REG_NO", CheckNullValue(data.REG_NO) ),
                new SqlParameter ( "@FULL_NAME", CheckNullValue(data.FULL_NAME) ),
                new SqlParameter ( "@OLD_PASSWORD", CheckNullValue(data.OLD_PASSWORD) ),
                new SqlParameter ( "@PASSWORD", CheckNullValue(data.PASSWORD) ),
                new SqlParameter ( "@CONFIRM_PASSWORD", CheckNullValue(data.CONFIRM_PASSWORD) ),
                new SqlParameter ( "@EMAIL", CheckNullValue(data.EMAIL) ),
                new SqlParameter ( "@PHONE", CheckNullValue(data.PHONE) ),
                new SqlParameter ( "@ROLE_ID", CheckNullValue(data.ROLE_ID) ),
                new SqlParameter ( "@FILE_PATH", CheckNullValue(data.FILE_PATH) ),
                new SqlParameter ( "@AD_USER", CheckNullValue(data.AD_USER) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_User_UpdatePassword] @USERNAME, @REG_NO, @FULL_NAME, @OLD_PASSWORD, @PASSWORD, @CONFIRM_PASSWORD, @EMAIL, @PHONE, @ROLE_ID, @FILE_PATH, @AD_USER, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult RemoveAttachment(User data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_User_RemoveAttachment] @USERNAME," +
                "@LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult Delete(User data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_User_Delete] @USERNAME, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        /*public DBResult Delete(User data)
        {
            DBContext db = new DBContext();
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USER_ID", CheckNullValue(data.USER_ID) ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_User_Delete] @USER_ID, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }*/

        //User Position
        public UserPosition PositionGetByKey(UserPosition data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) )
            };

            string query = "EXEC [dbo].[sp_UserPosition_GetByUsername] @USERNAME";
            UserPosition Result = db.UserPosition.FromSqlRaw<UserPosition>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }

        public IList<UserPosition> SearchPosition(UserPosition data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@DOCUMENT_CONTROL_ACCESS", CheckNullValue(data.DOCUMENT_CONTROL_ACCESS) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_UserPosition_Search] @USERNAME, @DIVISION, @DEPARTMENT_ID, @DOCUMENT_CONTROL_ACCESS, @PageNumber, @PageSize";
            IList<UserPosition> Result = db.UserPosition.FromSqlRaw<UserPosition>(query, param.ToArray()).ToList();

            return Result;
        }

        public DBResult DeletePosition(UserPosition data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_UserPosition_Delete] @USERNAME, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

        public DBResult InsertUpdatePosition(UserPosition data, string loginUser, DBContext db)
        {
            SqlParameter returnVal = CreateSqlParameterOutputInt("@RETURN_VAL");
            SqlParameter returnMsg = CreateSqlParameterOutputString("@RETURN_MSG");

            List<SqlParameter> param = new List<SqlParameter>
            {
                returnVal,
                new SqlParameter ( "@USERNAME", CheckNullValue(data.USERNAME) ),
                new SqlParameter ( "@POSITION_ID", CheckNullValue(data.POSITION_ID) ),
                new SqlParameter ( "@DIVISION", CheckNullValue(data.DIVISION) ),
                new SqlParameter ( "@DEPARTMENT_ID", CheckNullValue(data.DEPARTMENT_ID) ),
                new SqlParameter ( "@SECTION_ID", CheckNullValue(data.SECTION_ID) ),
                new SqlParameter ( "@LOGIN_USER", loginUser ),
                returnMsg
            };

            string query = "EXEC @RETURN_VAL = [dbo].[sp_UserPosition_InsertUpdate] @USERNAME, @POSITION_ID, @DIVISION, @DEPARTMENT_ID, @SECTION_ID, @LOGIN_USER, @RETURN_MSG OUTPUT";
            int affectedRow = db.Database.ExecuteSqlRaw(query, param.ToArray());

            DBResult result = new DBResult(Convert.ToBoolean(returnVal.Value), returnMsg.Value.ToString());
            return result;
        }

    }
}
