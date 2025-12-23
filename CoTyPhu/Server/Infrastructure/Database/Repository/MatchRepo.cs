using System;
using System.Threading.Tasks;
using Npgsql;
using Server.Infrastructure.Database.Connection;

namespace Server.Infrastructure.Database.Repository
{
    public class MatchRepo
    {
        private readonly DBConnection _db;

        public MatchRepo(DBConnection db)
        {
            _db = db;
        }

        public async Task<int> CreateMatch()
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            var cmd = new NpgsqlCommand(@"
                INSERT INTO ""Match"" (""NumberPlayer"", ""Status"")
                VALUES (1, 'Waiting')
                RETURNING ""IDMatch""", conn);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        public async Task<bool> StartMatch(int idMatch)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            var cmd = new NpgsqlCommand(@"
                UPDATE ""Match""
                SET ""Status"" = 'Playing',
                    ""StartTime"" = NOW(),
                    ""Turn"" = 1
                WHERE ""IDMatch"" = @id", conn);

            cmd.Parameters.AddWithValue("@id", idMatch);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> IncreasePlayerCount(int idMatch)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            var cmd = new NpgsqlCommand(@"
                UPDATE ""Match"" 
                SET ""NumberPlayer"" = ""NumberPlayer"" + 1 
                WHERE ""IDMatch"" = @id", conn);

            cmd.Parameters.AddWithValue("@id", idMatch);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DecreasePlayerCount(int idMatch)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            var cmd = new NpgsqlCommand(@"
                UPDATE ""Match"" 
                SET ""NumberPlayer"" = ""NumberPlayer"" - 1 
                WHERE ""IDMatch"" = @id", conn);

            cmd.Parameters.AddWithValue("@id", idMatch);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> EndMatch(int idMatch)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            var cmd = new NpgsqlCommand(@"
                UPDATE ""Match""
                SET ""EndTime"" = NOW(),
                    ""Status"" = 'End'
                WHERE ""IDMatch"" = @id", conn);

            cmd.Parameters.AddWithValue("@id", idMatch);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}