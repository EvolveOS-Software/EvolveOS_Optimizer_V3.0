// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using System;
using System.Threading.Tasks;

namespace EvolveOS_Optimizer.Core.Model
{
    public class UserLoginData
    {
        public string? PasswordHash { get; set; }
        public byte[]? ProfileImageBytes { get; set; }
        public string? UserType { get; set; }
    }

    public class UserDataAccess
    {
        private readonly string _connectionString;

        public UserDataAccess(string connectionString)
        {
            _connectionString = connectionString;
        }

        public bool IsDatabaseEmpty()
        {
            string cmdText = @"IF EXISTS(SELECT 1 FROM admin WHERE username IS NOT NULL) SELECT 1 ELSE SELECT 0";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            using (SqlCommand cmd = new SqlCommand(cmdText, connection))
            {
                try
                {
                    connection.Open();
                    int exists = Convert.ToInt32(cmd.ExecuteScalar());
                    return exists == 0;
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"SQL Error during initial DB check: {ex.Message}");
                    throw;
                }
            }
        }

        public UserLoginData GetPasswordAndImage(string username)
        {
            string selectPasswordSql = "SELECT password FROM admin WHERE username = @Username";

            string selectUserDataSql = "SELECT username, image, usertype FROM admin WHERE username = @Username";

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string? passwordHash = null;

                    using (SqlCommand cmdPassword = new SqlCommand(selectPasswordSql, connection))
                    {
                        cmdPassword.Parameters.AddWithValue("@Username", username);

                        using (SqlDataReader reader = cmdPassword.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                passwordHash = reader.GetString(0);
                            }
                        }
                    }

                    if (passwordHash == null)
                    {
                        return new UserLoginData();
                    }

                    using (SqlCommand cmdUserData = new SqlCommand(selectUserDataSql, connection))
                    {
                        cmdUserData.Parameters.AddWithValue("@Username", username);

                        using (SqlDataReader reader = cmdUserData.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                byte[]? imageData = reader[1] as byte[];

                                string? userType = reader.IsDBNull(2) ? null : reader.GetString(2);

                                return new UserLoginData
                                {
                                    PasswordHash = passwordHash,
                                    ProfileImageBytes = imageData,
                                    UserType = userType
                                };
                            }
                        }
                    }

                    return new UserLoginData { PasswordHash = passwordHash };

                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"SQL Error during login: {ex.Message}");
                    throw new InvalidOperationException("Database connection or query failed during login.", ex);
                }
            }
        }

        public Task<UserLoginData> GetPasswordAndImageAsync(string username)
        {
            return Task.Run(() => GetPasswordAndImage(username));
        }
    }
}