using Hangfire.Dashboard;

namespace MDC.Services
{
    public class HangfireAuthorization : IDashboardAuthorizationFilter
    {

        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();
            if (httpContext.Session.GetString("ROLE_ID") != "DMS-ADMIN")
            {
                return false;
            }
            return true;
        }

    }
}
