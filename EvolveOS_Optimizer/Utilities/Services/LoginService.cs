// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class LoginService
    {
        private const string MasterTable = "admin";
        private const string PasswordColumn = "password";

        public bool Authenticate(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return false;
            }

            string query = $"SELECT {PasswordColumn} FROM {MasterTable} WHERE username = @user";
            string? storedHashedPassword = null;

            try
            {
                using (SqlConnection connect = new SqlConnection(SqlConnectionHelper.connectReturn()))
                {
                    connect.Open();

                    using (SqlCommand cmd = new SqlCommand(query, connect))
                    {
                        cmd.Parameters.AddWithValue("@user", username);

                        using (SqlDataReader datareader = cmd.ExecuteReader())
                        {
                            if (datareader.Read())
                            {
                                storedHashedPassword = datareader.GetString(0);
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Database Error during Authentication: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Error during Authentication: {ex.Message}");
                return false;
            }

            if (!string.IsNullOrEmpty(storedHashedPassword))
            {
                return BCrypt.Net.BCrypt.Verify(password, storedHashedPassword);
            }

            return false;
        }
    }
}