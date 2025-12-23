using System;
using Npgsql; // Đổi từ SqlClient sang Npgsql
using DotNetEnv;
namespace Server.Infrastructure.Database.Connection
{
    public class DBConnection
    {
        public static string GetConnectionString()
        {
            // Tải file .env
            Env.Load();

            string host = Env.GetString("DB_HOST");
            string user = Env.GetString("DB_USER");
            string pass = Env.GetString("DB_PASSWORD");
            string dbName = Env.GetString("DB_NAME");
            string port = Env.GetString("DB_PORT");

            return $"Host={host};Port={port};Database={dbName};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true;";
        }
        // Lấy chuỗi này từ Supabase: Settings -> Database -> Connection string -> ADO.NET
        private readonly string _connectionString =
            "Host=db.hgvrqnoppstwhdkcdvyp.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=quynhduyen..;";

        public NpgsqlConnection GetConnection()
        {
            // Trả về NpgsqlConnection thay vì NpgsqlConnection
            return new NpgsqlConnection(_connectionString);
        }

        public bool TestConnection()
        {
            try
            {
                using var conn = GetConnection();
                conn.Open();
                Console.WriteLine("Ket noi Supabase thanh cong!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Loi ket noi Supabase: {ex.Message}");
                return false;
            }
        }
    }
}