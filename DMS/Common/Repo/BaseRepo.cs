using DMS.Models.DB;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DMS.Common.Repo
{
    public class BaseRepo
    {
        protected object CheckNullValue(int? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }
        protected object CheckNullValue(Decimal? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }

        protected object CheckNullValue(string? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }

        protected object CheckNullValue(Guid? val)
        {
            if (val == null) return DBNull.Value;
            return val;
        }

        protected object CheckNullValue(DateTime? val)
        {
            if (!val.HasValue) return DBNull.Value;
            return val;
        }

        protected SqlParameter CreateSqlParameterOutputInt(string parameterName)
        {
            var output = new SqlParameter(parameterName, SqlDbType.Int);
            output.Direction = System.Data.ParameterDirection.Output;
            output.Value = -1;

            return output;
        }

        protected SqlParameter CreateSqlParameterOutputString(string parameterName)
        {
            var output = new SqlParameter(parameterName, SqlDbType.VarChar, 2000);
            output.Direction = System.Data.ParameterDirection.Output;
            output.Value = string.Empty;

            return output;
        }

    }
}
