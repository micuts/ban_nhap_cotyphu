using System;
using System.Collections.Generic;
using System.Threading.Tasks; // Thêm thư viện này
using Npgsql;
using Server.Infrastructure.Database.Connection;
using Common.Domain.Models.Entities;

namespace Server.Infrastructure.Database.Repository
{
    public class AccountRepo
    {
        private readonly DBConnection _db;

        public AccountRepo(DBConnection db)
        {
            _db = db;
        }

        // Chuyển sang Task<Account> và OpenAsync/ExecuteReaderAsync
        public async Task<Account> GetById(int id)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                var cmd = new NpgsqlCommand(
                    "SELECT idaccount, username, passwordhash, email, displayname FROM account WHERE idaccount = @id",
                    conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    return new Account
                    {
                        IDAccount = rd.GetInt32(0),
                        Username = rd.GetString(1),
                        PasswordHash = rd.GetString(2),
                        Email = rd.GetString(3),
                        DisplayName = rd.IsDBNull(4) ? null : rd.GetString(4)
                    };
                }
            }
            catch (Exception ex) { Console.WriteLine("Loi GetById: " + ex.Message); }
            return null;
        }

        public async Task<bool> CheckUsername(string username)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();
                var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM account WHERE username = @u", conn);
                cmd.Parameters.AddWithValue("@u", username);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            catch { return false; }
        }

        public async Task<bool> CheckEmail(string email)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();
                var cmd = new NpgsqlCommand("SELECT COUNT(1) FROM account WHERE email = @e", conn);
                cmd.Parameters.AddWithValue("@e", email);
                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi CheckEmail: " + ex.Message);
                return false;
            }
        }

        public async Task<int> Insert(Account acc)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();
                var cmd = new NpgsqlCommand(@"
                    INSERT INTO account(username, passwordhash, email, displayname)
                    VALUES(@u, @p, @e, @d)
                    RETURNING idaccount", conn);

                cmd.Parameters.AddWithValue("@u", acc.Username);
                cmd.Parameters.AddWithValue("@p", acc.PasswordHash);
                cmd.Parameters.AddWithValue("@e", acc.Email);
                cmd.Parameters.AddWithValue("@d", (object)acc.DisplayName ?? DBNull.Value);

                return Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi Insert: " + ex.Message);
                return -1;
            }
        }

        public async Task<bool> CheckLogin(string username, string passwordHash)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();
                var cmd = new NpgsqlCommand(@"
                    SELECT COUNT(1) FROM account 
                    WHERE username=@u AND passwordhash=@p", conn);

                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", passwordHash);

                return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi CheckLogin: " + ex.Message);
                return false;
            }
        }

        public async Task<int> GetIdByLogin(string username, string passwordHash)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();
                var cmd = new NpgsqlCommand(@"
                    SELECT idaccount FROM account 
                    WHERE username = @u AND passwordhash = @p", conn);

                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@p", passwordHash);

                object result = await cmd.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                    return Convert.ToInt32(result);
            }
            catch (Exception ex) { Console.WriteLine("Loi GetIdByLogin: " + ex.Message); }
            return -1;
        }

        public async Task<bool> ChangePasswordByEmail(string email, string newPasswordHash)
        {
            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();
                var updateCmd = new NpgsqlCommand(@"
                    UPDATE account SET passwordhash = @new WHERE email = @e", conn);

                updateCmd.Parameters.AddWithValue("@new", newPasswordHash);
                updateCmd.Parameters.AddWithValue("@e", email);

                return await updateCmd.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Loi ChangePassword: " + ex.Message);
                return false;
            }
        }
    }
}