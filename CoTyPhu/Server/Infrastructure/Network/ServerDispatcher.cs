using Common.Constracts;
using Common.Constracts.Room;
using Common.Contracts.Auth;
using Common.Contracts.Game;
using Common.Contracts.Room;
using Common.Domain.Models.Entities;
using Server.Domain;
using Server.Domain.GameState;
using Server.Infrastructure.Database.Connection;
using Server.Infrastructure.Database.Repository;
using Server.Infrastructure.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Server.Infrastructure.Network
{
    public sealed class ServerDispatcher : IRequestDispatcher
    {
        private static readonly JsonSerializerOptions JsonOpt = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly AccountRepo _accountRepo;
        private readonly MatchRepo _matchRepo;
        private readonly PlayerRepo _playerRepo;
        private static readonly object _locksGuard = new();
        private static readonly Dictionary<int, object> _matchLocks = new();

        private static object GetMatchLock(int matchId)
        {
            lock (_locksGuard)
            {
                if (!_matchLocks.TryGetValue(matchId, out var o))
                    _matchLocks[matchId] = o = new object();
                return o;
            }
        }

        private static readonly Dictionary<int, ClientConnection> _connections = new();

        public ServerDispatcher()
        {
            var db = new DBConnection();
            _accountRepo = new AccountRepo(db);
            _matchRepo = new MatchRepo(db);
            _playerRepo = new PlayerRepo(db);
        }

        public async Task<MessageEnvelope> DispatchAsync(MessageEnvelope req)
        {
            // TẤT CẢ các hàm Handle bây giờ đều được await trực tiếp
            return req.Type switch
            {
                MessageType.LoginRequest => await HandleLogin(req),
                MessageType.RegisterRequest => await HandleRegister(req),
                MessageType.ForgotPasswordRequest => await HandleForgotPassword(req),
                MessageType.VerifyOTPRequest => await HandleVerifyOtp(req),
                MessageType.ResetPasswordRequest => await HandleResetPassword(req),

                MessageType.CreateRoomRequest => await HandleCreateRoom(req),
                MessageType.SearchRoomRequest => await HandleSearchRoom(req),
                MessageType.JoinRoomRequest => await HandleJoinRoom(req),
                MessageType.LeaveMatchRequest => await HandleLeaveRoom(req),
                MessageType.StartMatchRequest => await HandleStartMatch(req),

                _ => MakeError("Thông điệp không thể xử lý")
            };
        }

        #region Auth Handlers
        // Sửa thành async Task để giải quyết lỗi CS1061 (GetAwaiter)
        private async Task<MessageEnvelope> HandleLogin(MessageEnvelope req)
        {
            var body = JsonSerializer.Deserialize<LoginRequest>(req.Payload, JsonOpt)!;
            string hash = NormalizeToSha256Hex(body.Password);

            // THÊM await để giải quyết lỗi CS0029 (Task<bool> to bool)
            bool ok = await _accountRepo.CheckLogin(body.Username, hash);
            int? id = ok ? await _accountRepo.GetIdByLogin(body.Username, hash) : null;

            if (ok && id != null)
            {
                _connections[id.Value] = ServerState.CurrentConnection!;
            }

            return Wrap(
                MessageType.LoginResponse,
                new LoginResponse
                {
                    Success = ok,
                    Message = ok ? "Đăng nhập thành công" : "Sai tài khoản hoặc mật khẩu",
                    IDAccount = id
                });
        }

        private async Task<MessageEnvelope> HandleRegister(MessageEnvelope req)
        {
            var body = JsonSerializer.Deserialize<RegisterRequest>(req.Payload, JsonOpt)!;

            // SỬA LỖI CS0023: Thêm await vào trong ngoặc để kiểm tra bool
            if (await _accountRepo.CheckUsername(body.Username))
                return Wrap(MessageType.RegisterResponse,
                    new RegisterResponse { Success = false, Message = "Username đã tồn tại" });

            if (await _accountRepo.CheckEmail(body.Email))
                return Wrap(MessageType.RegisterResponse,
                    new RegisterResponse { Success = false, Message = "Email đã tồn tại" });

            int id = await _accountRepo.Insert(new Account
            {
                Username = body.Username,
                PasswordHash = NormalizeToSha256Hex(body.Password),
                Email = body.Email,
                DisplayName = body.DisplayName
            });

            return Wrap(
                MessageType.RegisterResponse,
                new RegisterResponse
                {
                    Success = id > 0,
                    Message = id > 0 ? "Đăng ký thành công" : "Đăng ký thất bại"
                });
        }

        private async Task<MessageEnvelope> HandleForgotPassword(MessageEnvelope req)
        {
            var body = JsonSerializer.Deserialize<ForgotPasswordRequest>(req.Payload, JsonOpt)!;
            if (!(await _accountRepo.CheckEmail(body.Email)))
                return Wrap(MessageType.ForgotPasswordResponse,
                    new ForgotPasswordResponse { Success = false });

            GenerateOtp(body.Email);
            return Wrap(MessageType.ForgotPasswordResponse,
                new ForgotPasswordResponse { Success = true });
        }

        private async Task<MessageEnvelope> HandleVerifyOtp(MessageEnvelope req)
        {
            var body = JsonSerializer.Deserialize<VerifyOTPRequest>(req.Payload, JsonOpt)!;
            // OTP logic thường chạy trong RAM nên không nhất thiết await nếu không lưu DB
            return Wrap(
                MessageType.VerifyOTPResponse,
                new VerifyOTPResponse { Success = VerifyOtp(body.Email, body.OTP) });
        }

        private async Task<MessageEnvelope> HandleResetPassword(MessageEnvelope req)
        {
            var body = JsonSerializer.Deserialize<ResetPasswordRequest>(req.Payload, JsonOpt)!;

            bool ok = await _accountRepo.ChangePasswordByEmail(
                body.Email,
                NormalizeToSha256Hex(body.NewPassword));

            return Wrap(
                MessageType.ResetPasswordResponse,
                new ResetPasswordResponse { Success = ok });
        }
        #endregion

        #region Room Handlers
        private async Task<MessageEnvelope> HandleCreateRoom(MessageEnvelope req)
        {
            int matchId = await _matchRepo.CreateMatch();
            if (matchId <= 0)
                return MakeError("Tạo phòng thất bại!");

            ServerState.Matches[matchId] = new MatchState
            {
                MatchId = matchId,
                IsMatch = 0,
                CurrentTurnPlayerId = 0
            };

            return Wrap(
                MessageType.CreateRoomResponse,
                new CreateRoomResponse { Success = true, RoomID = matchId },
                matchId,
                null
            );
        }

        private async Task<MessageEnvelope> HandleSearchRoom(MessageEnvelope req)
        {
            if (req.MatchId == null ||
                !ServerState.Matches.TryGetValue(req.MatchId.Value, out var match))
            {
                return Wrap(MessageType.SearchRoomResponse,
                    new SearchRoomResponse { Success = false });
            }

            // Xử lý list player bất đồng bộ để tránh lỗi 'Task<Account>' does not contain Username
            var playerList = new List<RoomPlayerInfo>();
            foreach (var p in match.Players.Values.OrderBy(p => p.PlayerId))
            {
                var acc = await _accountRepo.GetById(p.AccountId); // Phải có await ở đây
                playerList.Add(new RoomPlayerInfo
                {
                    DisplayName = acc?.Username ?? $"Player {p.PlayerId}",
                    PlayerId = p.PlayerId,
                    CharacterIndex = p.CharacterIndex
                });
            }

            return Wrap(
                MessageType.SearchRoomResponse,
                new SearchRoomResponse
                {
                    Success = true,
                    RoomId = match.MatchId,
                    PlayerRooms = match.Players.Values
                        .Select(p => p.CharacterIndex)
                        .Where(c => c > 0)
                        .ToList(),
                    Players = playerList
                },
                match.MatchId,
                null
            );
        }

        private async Task<MessageEnvelope> HandleJoinRoom(MessageEnvelope req)
        {
            var body = JsonSerializer.Deserialize<JoinRoomRequest>(req.Payload, JsonOpt)!;

            if (!ServerState.Matches.TryGetValue(body.RoomID, out var match))
                return Wrap(MessageType.JoinRoomResponse, new JoinRoomResponse { Success = false });

            lock (GetMatchLock(match.MatchId))
            {
                if (match.IsMatch != 0)
                    return Wrap(MessageType.JoinRoomResponse, new JoinRoomResponse { Success = false });

                if (match.Players.Values.Any(p => p.CharacterIndex == body.CharacterIndex))
                    return Wrap(MessageType.JoinRoomResponse, new JoinRoomResponse { Success = false });

                int slot = Enumerable.Range(1, 4).FirstOrDefault(i => !match.Players.ContainsKey(i));
                if (slot == 0)
                    return Wrap(MessageType.JoinRoomResponse, new JoinRoomResponse { Success = false });

                match.Players[slot] = new PlayerState
                {
                    PlayerId = slot,
                    AccountId = body.AccountID,
                    CharacterIndex = body.CharacterIndex
                };

                _connections[body.AccountID] = ServerState.CurrentConnection!;

                if (match.CurrentTurnPlayerId == 0)
                    match.CurrentTurnPlayerId = 1;
            }

            await _playerRepo.InsertPlayer(body.RoomID, body.AccountID, body.CharacterIndex);
            await _matchRepo.IncreasePlayerCount(body.RoomID);

            BroadcastRoom(
                match.MatchId,
                Wrap(MessageType.RoomUpdatedEvent, new RoomUpdatedEvent { RoomId = match.MatchId }, match.MatchId, null)
            );

            return Wrap(
                MessageType.JoinRoomResponse,
                new JoinRoomResponse { Success = true, IDPlayer = match.Players.Keys.Max() },
                match.MatchId,
                null
            );
        }

        private async Task<MessageEnvelope> HandleLeaveRoom(MessageEnvelope req)
        {
            int matchId = req.MatchId!.Value;
            int playerId = req.PlayerId!.Value;

            if (!ServerState.Matches.TryGetValue(matchId, out var match))
                return MakeError("Không tìm thấy trận đấu.");

            match.Players.Remove(playerId);

            await _playerRepo.DeletePlayer(matchId, playerId);
            await _matchRepo.DecreasePlayerCount(matchId);

            if (match.Players.Count == 0)
            {
                await _matchRepo.EndMatch(matchId);
                ServerState.Matches.Remove(matchId);
                return Wrap(
                    MessageType.PlayerLeftEvent,
                    new PlayerLeftEvent { PlayerId = playerId },
                    matchId,
                    null
                );
            }

            if (match.CurrentTurnPlayerId == playerId)
            {
                match.CurrentTurnPlayerId = match.Players.Keys.Min();
            }

            BroadcastRoom(
                matchId,
                Wrap(
                    MessageType.RoomUpdatedEvent,
                    new RoomUpdatedEvent { RoomId = matchId },
                    matchId,
                    null
                )
            );

            return Wrap(
                MessageType.PlayerLeftEvent,
                new PlayerLeftEvent { PlayerId = playerId },
                matchId,
                null
            );
        }

        private async Task<MessageEnvelope> HandleStartMatch(MessageEnvelope req)
        {
            if (req.MatchId == null || req.PlayerId == null)
                return MakeError("Không thể bắt đầu trận đấu!");

            if (req.PlayerId != 1)
                return MakeError("Chỉ có chủ phòng mới có thể bắt đầu trận đấu!");

            var match = ServerState.Matches[req.MatchId.Value];

            match.IsMatch = 1;
            match.CurrentTurnPlayerId = match.Players.Keys.Min();

            await _matchRepo.StartMatch(match.MatchId);

            BroadcastRoom(
                match.MatchId,
                Wrap(
                    MessageType.StartMatchResponse,
                    new StartMatchResponse { MatchId = match.MatchId },
                    match.MatchId,
                    null
                )
            );

            return Wrap(
                MessageType.StartMatchResponse,
                new { Success = true },
                match.MatchId,
                null
            );
        }
        #endregion

        #region Broadcast Helpers
        private void BroadcastRoom(int matchId, MessageEnvelope env)
        {
            if (!ServerState.Matches.TryGetValue(matchId, out var match))
                return;

            foreach (var p in match.Players.Values)
            {
                if (_connections.TryGetValue(p.AccountId, out var conn))
                {
                    _ = conn.SendAsync(env);
                }
            }
        }
        #endregion

        #region Helpers
        private static MessageEnvelope Wrap<T>(
            MessageType type,
            T body,
            int? matchId = null,
            int? playerId = null)
            => new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                Type = type,
                MatchId = matchId,
                PlayerId = playerId,
                Payload = JsonSerializer.Serialize(body, JsonOpt)
            };

        private static MessageEnvelope MakeError(string msg)
            => new MessageEnvelope
            {
                MessageId = Guid.NewGuid(),
                Type = MessageType.ErrorResponse,
                Payload = $"{{\"message\":\"{msg}\"}}"
            };
        #endregion

        #region OTP and Hash Handlers
        private static string NormalizeToSha256Hex(string input)
            => IsHexSha256(input) ? input.ToLowerInvariant() : Sha256Hex(input);

        private static bool IsHexSha256(string s)
            => !string.IsNullOrEmpty(s) && s.Length == 64 &&
               Regex.IsMatch(s, "^[0-9a-fA-F]{64}$");

        private static string Sha256Hex(string raw)
        {
            using var sha = SHA256.Create();
            return string.Concat(
                sha.ComputeHash(Encoding.UTF8.GetBytes(raw))
                   .Select(b => b.ToString("x2")));
        }

        private static readonly Dictionary<string, (string otp, DateTime exp, bool verified)> _otp = new();

        private static void GenerateOtp(string email)
        {
            _otp[email] = (new Random().Next(100000, 999999).ToString(),
                DateTime.UtcNow.AddMinutes(2), false);
        }

        private static bool VerifyOtp(string email, string input)
        {
            if (!_otp.TryGetValue(email, out var s)) return false;
            if (s.exp < DateTime.UtcNow) return false;
            if (s.otp != input) return false;
            _otp[email] = (s.otp, s.exp, true);
            return true;
        }
        #endregion
    }
}