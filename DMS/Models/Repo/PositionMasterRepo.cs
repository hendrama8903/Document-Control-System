using DMS.Common.Models;
using DMS.Common.Repo;
using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DMS.Models.Repo
{
    public class PositionMasterRepo : BaseRepo
    {
        private PositionMasterRepo() { }

        #region Singleton
        private static PositionMasterRepo instance = null;
        public static PositionMasterRepo Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PositionMasterRepo();
                }
                return instance;
            }
        }
        #endregion

        public IList<PositionMaster> Search(PositionMaster data, DBContext db, int? PageNumber, int? PageSize)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@POSITION_NAME", CheckNullValue(data.POSITION_NAME) ),
                new SqlParameter ( "@POSITION_LEVEL", CheckNullValue(data.POSITION_LEVEL) ),
                new SqlParameter ( "@PageNumber", CheckNullValue(PageNumber) ),
                new SqlParameter ( "@PageSize", CheckNullValue(PageSize) )
            };

            string query = "EXEC [dbo].[sp_PositionMaster_Search] @POSITION_NAME, @POSITION_LEVEL, @PageNumber, @PageSize";
            IList<PositionMaster> Result = db.PositionMaster.FromSqlRaw<PositionMaster>(query, param.ToArray()).ToList();

            return Result;
        }

        public PositionMaster GetByKey(PositionMaster data, DBContext db)
        {
            List<SqlParameter> param = new List<SqlParameter>
            {
                new SqlParameter ( "@POSITION_ID", CheckNullValue(data.POSITION_ID) )
            };

            string query = "EXEC [dbo].[sp_PositionMaster_GetByKey] @POSITION_ID";
            PositionMaster Result = db.PositionMaster.FromSqlRaw<PositionMaster>(query, param.ToArray()).AsEnumerable().FirstOrDefault();

            return Result;
        }
    }
}
