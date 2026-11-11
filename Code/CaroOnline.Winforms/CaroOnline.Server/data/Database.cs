using Microsoft.Data.SqlClient;

namespace CaroOnline.Server
{
    public class Database
    {
        private readonly string connectionString =
            "Server=100.103.157.39,1433;" +
            "Database=CaroOnlineDB;" +
            "User Id=(tài khoản sql;" +
            "Password=(mk sql);" +
            "TrustServerCertificate=True;";

        public bool TestConnection(out string error)
        {
            try
            {
                using (SqlConnection connection =
                       new SqlConnection(connectionString))
                {
                    connection.Open();

                    error = "";
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}