// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using EvolveOS_Optimizer.Core.Model;
using EvolveOS_Optimizer.Utilities.Helpers;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class DataService
    {
        private string GetConnectionString()
        {
            return SqlConnectionHelper.connectReturn();
        }

        private void AddCommonParameters(SqlCommand cmd, PasswordEntry entry, string encryptedPassword)
        {
            cmd.Parameters.AddWithValue("@Us", entry.UserId);

            cmd.Parameters.AddWithValue("@Name", entry.Name);
            cmd.Parameters.AddWithValue("@Type", (object?)entry.Type ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Username", entry.Username);
            cmd.Parameters.AddWithValue("@Email", (object?)entry.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MobileNumber", (object?)entry.MobileNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Website", (object?)entry.Website ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Description", (object?)entry.Description ?? DBNull.Value);

            cmd.Parameters.AddWithValue("@Password", encryptedPassword);
        }

        public List<PasswordEntry> GetAllPasswordEntries(string userId)
        {
            List<PasswordEntry> entries = new List<PasswordEntry>();
            string query = "SELECT Id, Name, Type, Username, Email, Mobile, Website, Description, Password FROM MainData where UserId = @Us";

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Us", userId);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string encryptedPwd = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);

                                entries.Add(new PasswordEntry
                                {
                                    Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                                    Name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                                    Type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                    Username = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                    Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                                    MobileNumber = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                                    Website = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                                    Description = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),

                                    EncryptedPassword = encryptedPwd,

                                    UserId = userId
                                });
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Error loading entries: {ex.Message}");
            }
            return entries;
        }

        public bool AddPasswordEntry(PasswordEntry newEntry, string encryptedPassword)
        {
            if (string.IsNullOrWhiteSpace(newEntry.Id))
            {
                newEntry.Id = Guid.NewGuid().ToString();
            }

            string query = @"INSERT INTO MainData
                             (Id, UserId, Name, Type, Username, Email, Mobile, Password, Website, Description)
                             VALUES
                             (@Id, @Us, @Name, @Type, @Username, @Email, @MobileNumber, @Password, @Website, @Description)";

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        AddCommonParameters(cmd, newEntry, encryptedPassword);

                        cmd.Parameters.AddWithValue("@Id", newEntry.Id);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Error adding entry: {ex.Message}");
                return false;
            }
        }

        public bool UpdatePasswordEntry(PasswordEntry updatedEntry, string encryptedPassword)
        {
            string query = @"UPDATE MainData SET 
                                 Name = @Name, Type = @Type, Username = @Username, 
                                 Email = @Email, Mobile = @MobileNumber, Password = @Password, Website = @Website, 
                                 Description = @Description
                                 WHERE Id = @Id AND UserId = @Us";

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        AddCommonParameters(cmd, updatedEntry, encryptedPassword);
                        cmd.Parameters.AddWithValue("@Id", updatedEntry.Id);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Error updating entry: {ex.Message}");
                return false;
            }
        }

        public bool SavePasswordEntry(PasswordEntry entry, string encryptedPassword)
        {
            if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                return UpdatePasswordEntry(entry, encryptedPassword);
            }
            else
            {
                return AddPasswordEntry(entry, encryptedPassword);
            }
        }

        public bool DeletePasswordEntry(string entryName, string userId)
        {
            string query = "DELETE FROM MainData WHERE Name = @Name AND UserId = @Us";

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@Name", entryName);
                        cmd.Parameters.AddWithValue("@Us", userId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Error deleting entry: {ex.Message}");
                return false;
            }
        }

        public bool UpdateEncryptedPassword(string entryName, string newEncryptedPassword, string userId)
        {
            string query = @"UPDATE MainData 
                             SET Password = @NewPassword 
                             WHERE Name = @Name AND UserId = @Us";

            try
            {
                using (SqlConnection connection = new SqlConnection(GetConnectionString()))
                {
                    connection.Open();
                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@NewPassword", newEncryptedPassword);
                        cmd.Parameters.AddWithValue("@Name", entryName);
                        cmd.Parameters.AddWithValue("@Us", userId);

                        return cmd.ExecuteNonQuery() > 0;
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Error updating encrypted password for '{entryName}': {ex.Message}");
                return false;
            }
        }
    }
}