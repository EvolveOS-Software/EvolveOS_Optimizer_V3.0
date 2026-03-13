using System.IO;
using EvolveOS_Optimizer.Utilities.Controls;

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
                Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();

                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(masterConnString))
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

                    using (var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                System.Threading.Thread.Sleep(800);
            }
            catch (Exception ex)
            {
                ErrorLogging.LogWritingFile(ex);
            }
        }
    }
}