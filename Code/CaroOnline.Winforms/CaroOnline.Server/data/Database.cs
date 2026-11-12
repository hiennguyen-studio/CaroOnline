using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace CaroOnline.Server
{
    public class UserAccount
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }     // "Admin" hoặc "User"
        public string Status { get; set; }   // "Active" hoặc "Locked"
    }

    public class Database
    {
        private readonly string connectionString =
            "Server=100.103.157.39,1433;" +
            "Database=CaroOnlineDB;" +
            "User Id=caro_member_tk;" +
            "Password=mk;" +
            "TrustServerCertificate=True;";

        // Hash SHA256 (hex in hoa), khớp với HASHBYTES trong SQL
        public static string Hash(string input) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

        public bool TestConnection(out string error)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
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

        public bool RegisterUser(string username, string password, string displayName, out string message)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand checkCmd = new SqlCommand(
                        "SELECT COUNT(*) FROM Users WHERE Username = @Username", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Username", username.Trim());
                        if ((int)checkCmd.ExecuteScalar() > 0)
                        {
                            message = "Tên tài khoản đã tồn tại!";
                            return false;
                        }
                    }

                    using (SqlCommand insertCmd = new SqlCommand(
                        @"INSERT INTO Users (Username, Password, DisplayName, Role, Status)
                          VALUES (@Username, @Password, @DisplayName, 'User', 'Active')", conn))
                    {
                        insertCmd.Parameters.AddWithValue("@Username", username.Trim());
                        insertCmd.Parameters.AddWithValue("@Password", Hash(password));
                        insertCmd.Parameters.AddWithValue("@DisplayName", displayName.Trim());
                        insertCmd.ExecuteNonQuery();
                        message = "Đăng ký thành công!";
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                message = "Lỗi Database: " + ex.Message;
                return false;
            }
        }

        public UserAccount LoginUser(string username, string password, out string message)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        @"SELECT Id, Username, DisplayName, Role, Status FROM Users
                          WHERE Username = @Username AND Password = @Password", conn))
                    {
                        cmd.Parameters.AddWithValue("@Username", username.Trim());
                        cmd.Parameters.AddWithValue("@Password", Hash(password));

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string status = reader["Status"].ToString();
                                if (status == "Locked")
                                {
                                    message = "Tài khoản đang bị khóa!";
                                    return null;
                                }

                                message = "Đăng nhập thành công!";
                                return new UserAccount
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    Username = reader["Username"].ToString(),
                                    DisplayName = reader["DisplayName"].ToString(),
                                    Role = reader["Role"].ToString(),
                                    Status = status
                                };
                            }
                        }
                    }
                }
                message = "Sai tên đăng nhập hoặc mật khẩu!";
                return null;
            }
            catch (Exception ex)
            {
                message = "Lỗi Database: " + ex.Message;
                return null;
            }
        }

        public DataTable GetAllUsers()
        {
            DataTable dt = new DataTable();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlDataAdapter da = new SqlDataAdapter(
                    "SELECT Id, Username, DisplayName, Role, Status, CreatedAt FROM Users", conn))
                {
                    da.Fill(dt);
                }
            }
            return dt;
        }

        public bool UpdateUserByAdmin(int id, string displayName, string role, string status)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    @"UPDATE Users SET DisplayName = @DisplayName, Role = @Role, Status = @Status
                      WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@DisplayName", displayName);
                    cmd.Parameters.AddWithValue("@Role", role);
                    cmd.Parameters.AddWithValue("@Status", status);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }
    }
}