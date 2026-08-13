// Copyright (c) 2026 EvolveOS Software
// Licensed under the MIT License.

using System.IO;
using System.Threading;
using EvolveOS_Optimizer.Utilities.Controls;
using EvolveOS_Optimizer.Utilities.Services;
using Microsoft.Data.SqlClient;

namespace EvolveOS_Optimizer.Utilities.Helpers
{
    public static class SqlConnectionHelper
    {
        private static string GetRealBaseDirectory()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;
            return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        }

        private static string DbPath => Path.Combine(GetRealBaseDirectory(), "EvolveOS_OptimizerDb.mdf");

        public static string connectReturn()
        {
            return $@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename={DbPath};Initial Catalog=EvolveOS_OptimizerDb_Main;Integrated Security=True;Connect Timeout=30";
        }

        public static string connectReturnMARS()
        {
            return $@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename={DbPath};Initial Catalog=EvolveOS_OptimizerDb_Main;MultipleActiveResultSets=True;Integrated Security=True;Connect Timeout=30";
        }

        public static void ReleaseDatabase()
        {
            string masterConnString = @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True";
            string dbFilePath = DbPath;

            string dbSafePath = dbFilePath.Replace("'", "''");

            try
            {
                SqlConnection.ClearAllPools();

                using (var conn = new SqlConnection(masterConnString))
                {
                    conn.Open();

                    string sql = $@"
                DECLARE @dbName NVARCHAR(256);
                SELECT @dbName = DB_NAME(database_id) 
                FROM sys.master_files 
                WHERE physical_name = '{dbSafePath}';
                
                IF @dbName IS NOT NULL
                BEGIN
                    EXEC('ALTER DATABASE [' + @dbName + '] SET SINGLE_USER WITH ROLLBACK IMMEDIATE');
                    EXEC sp_detach_db @dbName;
                END";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                Thread.Sleep(800);
            }
            catch (SqlException ex) when (ex.Number == 50 || ex.Number == -1)
            {
                Debug.WriteLine($"[Database Release] Ignored LocalDB boot failure: {ex.Message}");
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex);
            }
        }

        public static bool RestoreDatabase(string selectedBackupFilePath)
        {
            SqlConnection.ClearAllPools();

            string targetDbName = "EvolveOS_OptimizerDb_Main";

            string masterConnString = @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True";

            string workingBakPath = selectedBackupFilePath;
            bool usingTempDecryptedFile = false;

            try
            {
                if (selectedBackupFilePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
                {
                    workingBakPath = Path.Combine(Path.GetTempPath(), "EvolveOS_TempRestore.bak");

                    DatabaseSecurityService.DecryptDatabase(selectedBackupFilePath, workingBakPath);
                    usingTempDecryptedFile = true;
                }

                using (var conn = new SqlConnection(masterConnString))
                {
                    conn.Open();

                    string sql = $@"
                        ALTER DATABASE [{targetDbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                
                        RESTORE DATABASE [{targetDbName}] FROM DISK = '{workingBakPath}' WITH REPLACE;
                
                        ALTER DATABASE [{targetDbName}] SET MULTI_USER;
                    ";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = 120;
                        cmd.ExecuteNonQuery();
                    }
                }

                Debug.WriteLine("[App] Database restored successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Failed to restore database: {ex.Message}");
                return false;
            }
            finally
            {
                if (usingTempDecryptedFile && File.Exists(workingBakPath))
                {
                    try
                    {
                        File.Delete(workingBakPath);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            }
        }
    }
}