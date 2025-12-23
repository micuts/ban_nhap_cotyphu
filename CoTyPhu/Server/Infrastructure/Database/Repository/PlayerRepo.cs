using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; // Cần thêm namespace này
using Npgsql;
using Server.Infrastructure.Database.Connection;
using Common.Domain.Models.Entities;

namespace Server.Infrastructure.Database.Repository
{
    public class PlayerRepo
    {
        private readonly DBConnection _db;

        public PlayerRepo(DBConnection db)
        {
            _db = db;
        }

        // Thêm 1 Player - Chuyển sang async Task
        public async Task<int> InsertPlayer(int idMatch, int idAccount, int characterIndex)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var getNewIdCmd = new NpgsqlCommand(@"
                    SELECT COUNT(*) + 1 
                    FROM ""Player"" 
                    WHERE ""IDMatch"" = @mid", conn);

                getNewIdCmd.Parameters.AddWithValue("@mid", idMatch);
                int newIdPlayer = Convert.ToInt32(await getNewIdCmd.ExecuteScalarAsync());

                var insertCmd = new NpgsqlCommand(@"
                    INSERT INTO ""Player""
                    (""IDMatch"", ""IDPlayer"", ""IDAccount"", ""Money"", ""Position"", ""StatusPlayer"", ""CharacterIndex"")
                    VALUES
                    (@mid, @pid, @acc, 1500, 0, 'Ready', @char)", conn);

                insertCmd.Parameters.AddWithValue("@mid", idMatch);
                insertCmd.Parameters.AddWithValue("@pid", newIdPlayer);
                insertCmd.Parameters.AddWithValue("@acc", idAccount);
                insertCmd.Parameters.AddWithValue("@char", characterIndex);

                await insertCmd.ExecuteNonQueryAsync();
                return newIdPlayer;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InsertPlayer Error: {ex.Message}");
                return -1;
            }
        }

        // Xóa Player - Sửa lỗi cú pháp 'sync' thành 'async'
        public async Task<bool> DeletePlayer(int idMatch, int idPlayer)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    DELETE FROM ""Player""
                    WHERE ""IDMatch""=@mid AND ""IDPlayer""=@pid", conn);

                cmd.Parameters.AddWithValue("@mid", idMatch);
                cmd.Parameters.AddWithValue("@pid", idPlayer);

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch { return false; }
        }

        // Đếm số player còn sống - Chuyển sang async Task
        public async Task<int> CountAlive(int idMatch)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    SELECT COUNT(*) 
                    FROM ""Player""
                    WHERE ""IDMatch""=@mid 
                    AND ""StatusPlayer"" NOT IN ('Bankrupt')", conn);

                cmd.Parameters.AddWithValue("@mid", idMatch);

                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch { return 0; }
        }

        // Player phá sản - Sửa GETDATE() thành NOW() cho PostgreSQL
        public async Task<bool> SetBankrupt(int idMatch, int idPlayer)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    UPDATE ""Player""
                    SET ""StatusPlayer""='Bankrupt',
                        ""CrashTime"" = NOW()
                    WHERE ""IDMatch""=@mid AND ""IDPlayer""=@pid", conn);

                cmd.Parameters.AddWithValue("@mid", idMatch);
                cmd.Parameters.AddWithValue("@pid", idPlayer);
                await cmd.ExecuteNonQueryAsync();

                // Lưu ý: Các Repo khác cũng cần chuyển sang Async để await tại đây
                var propertyRepo = new PropertyRepo(_db);
                await propertyRepo.ResetPlayerProperties(idMatch, idPlayer);

                var matchRepo = new MatchRepo(_db);
                await matchRepo.DecreasePlayerCount(idMatch);

                int alive = await CountAlive(idMatch);
                if (alive == 1)
                    await matchRepo.EndMatch(idMatch);

                return true;
            }
            catch { return false; }
        }
    }
}