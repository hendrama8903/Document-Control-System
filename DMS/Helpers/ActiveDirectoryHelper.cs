using System.DirectoryServices.AccountManagement;

namespace DMS.Helpers
{
    public static class ActiveDirectoryHelper
    {
        public static (bool Success, string Message) ValidateUser(string domain, string username, string password)
        {
            try
            {
                using (PrincipalContext context = new PrincipalContext(ContextType.Domain, domain))
                {
                    bool isValid = context.ValidateCredentials(username, password);
                    return isValid ? (true, "") : (false, "Invalid Active Directory username or password.");
                }
            }
            catch (Exception ex)
            {
                return (false, "Active Directory authentication failed: " + ex.Message);
            }
        }
    }
}
