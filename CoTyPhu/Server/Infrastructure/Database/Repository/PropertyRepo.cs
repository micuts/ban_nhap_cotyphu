using System;
using System.Collections.Generic;
using System.Threading.Tasks; // Cần thiết cho async/await
using Npgsql;
using Server.Infrastructure.Database.Connection;
using Common.Domain.Models.Entities;

namespace Server.Infrastructure.Database.Repository
{
    public class PropertyRepo
    {
        private readonly DBConnection _db;

        public PropertyRepo(DBConnection db)
        {
            _db = db;
        }

        // Reset đất của 1 player khi phá sản - Cần thiết cho PlayerRepo.SetBankrupt
        public async Task<bool> ResetPlayerProperties(int matchId, int playerId)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                // Dùng dấu ngoặc kép ""Property"" để tránh lỗi phân biệt hoa thường của Postgres
                var cmd = new NpgsqlCommand(@"
                    UPDATE ""Property""
                    SET ""PlayerID""=NULL, ""Level""=0
                    WHERE ""IDMatch""=@mid AND ""PlayerID""=@pid", conn);

                cmd.Parameters.AddWithValue("@mid", matchId);
                cmd.Parameters.AddWithValue("@pid", playerId);

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch { return false; }
        }

        // Lấy 1 property - Chuyển sang Async
        public async Task<Property?> GetProperty(int matchId, int propertyId)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    SELECT ""IDProperty"", ""IDMatch"", ""Name"", ""Value"", ""Level"", ""TypeProperty"", ""PlayerID""
                    FROM ""Property""
                    WHERE ""IDMatch""=@mid AND ""IDProperty""=@pid", conn);

                cmd.Parameters.AddWithValue("@mid", matchId);
                cmd.Parameters.AddWithValue("@pid", propertyId);

                using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    return new Property
                    {
                        IDProperty = rd.GetInt32(0),
                        IDMatch = rd.GetInt32(1),
                        Name = rd.GetString(2),
                        Value = rd.GetInt32(3),
                        Level = rd.GetInt32(4),
                        TypeProperty = rd.GetString(5),
                        PlayerID = rd.IsDBNull(6) ? null : rd.GetInt32(6)
                    };
                }
            }
            catch { }
            return null;
        }

        // Đổi chủ sở hữu - Chuyển sang Async
        public async Task<bool> UpdateOwner(int matchId, int propertyId, int? newOwner)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    UPDATE ""Property""
                    SET ""PlayerID""=@pid
                    WHERE ""IDMatch""=@mid AND ""IDProperty""=@prop", conn);

                cmd.Parameters.AddWithValue("@mid", matchId);
                cmd.Parameters.AddWithValue("@prop", propertyId);
                cmd.Parameters.AddWithValue("@pid", (object?)newOwner ?? DBNull.Value);

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch { return false; }
        }

        // Đổi level nhà - Chuyển sang Async
        public async Task<bool> UpdateLevel(int matchId, int propertyId, int newLevel)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(@"
                    UPDATE ""Property""
                    SET ""Level""=@lv
                    WHERE ""IDMatch""=@mid AND ""IDProperty""=@prop", conn);

                cmd.Parameters.AddWithValue("@mid", matchId);
                cmd.Parameters.AddWithValue("@prop", propertyId);
                cmd.Parameters.AddWithValue("@lv", newLevel);

                return await cmd.ExecuteNonQueryAsync() > 0;
            }
            catch { return false; }
        }
    }
}