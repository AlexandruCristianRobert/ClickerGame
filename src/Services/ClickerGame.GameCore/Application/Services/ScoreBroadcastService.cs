using Microsoft.AspNetCore.SignalR;
using ClickerGame.GameCore.Hubs;
using ClickerGame.GameCore.Application.DTOs;
using ClickerGame.Shared.Logging;

namespace ClickerGame.GameCore.Application.Services
{
    public class ScoreBroadcastService : IScoreBroadcastService
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly ILogger<ScoreBroadcastService> _logger;
        private readonly ICorrelationService _correlationService;
        private readonly ISignalRConnectionManager _connectionManager;

        public ScoreBroadcastService(
            IHubContext<GameHub> hubContext,
            ILogger<ScoreBroadcastService> logger,
            ICorrelationService correlationService,
            ISignalRConnectionManager connectionManager)
        {
            _hubContext = hubContext;
            _logger = logger;
            _correlationService = correlationService;
            _connectionManager = connectionManager;
        }

        public async Task BroadcastScoreUpdateAsync(Guid playerId, ScoreUpdateDto scoreUpdate)
        {
            try
            {
                // Send to player's specific score update group
                await _hubContext.Clients.Group($"ScoreUpdates_{playerId}")
                    .SendAsync("ScoreUpdateBroadcast", scoreUpdate);

                _logger.LogDebug("Score update broadcasted for player {PlayerId}", playerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting score update for player {PlayerId}", playerId);
            }
        }

        public async Task BroadcastLeaderboardUpdateAsync(ScoreLeaderboardUpdateDto leaderboardUpdate)
        {
            try
            {
                await _hubContext.Clients.Group("GameEvents")
                    .SendAsync("LeaderboardUpdate", leaderboardUpdate);

                // Also send to the specific player
                await _hubContext.Clients.Group($"Player_{leaderboardUpdate.PlayerId}")
                    .SendAsync("RankChanged", leaderboardUpdate);

                _logger.LogBusinessEvent(_correlationService, "LeaderboardUpdateBroadcasted", new
                {
                    leaderboardUpdate.PlayerId,
                    leaderboardUpdate.Rank,
                    leaderboardUpdate.RankChanged
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting leaderboard update");
            }
        }

        public async Task BroadcastMilestoneAchievedAsync(Guid playerId, string milestone, string score)
        {
            try
            {
                var connections = await _connectionManager.GetPlayerConnectionsAsync(playerId);
                var username = connections.FirstOrDefault()?.Username ?? "Unknown";

                await _hubContext.Clients.Group("GameEvents")
                    .SendAsync("MilestoneAchieved", new
                    {
                        PlayerId = playerId,
                        Username = username,
                        Milestone = milestone,
                        Score = score,
                        Timestamp = DateTime.UtcNow
                    });

                _logger.LogBusinessEvent(_correlationService, "MilestoneBroadcasted", new
                {
                    PlayerId = playerId,
                    Milestone = milestone,
                    Score = score
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting milestone for player {PlayerId}", playerId);
            }
        }

        public async Task BroadcastScoreRankChangeAsync(Guid playerId, int newRank, int previousRank)
        {
            try
            {
                await _hubContext.Clients.Group($"Player_{playerId}")
                    .SendAsync("RankChanged", new
                    {
                        PlayerId = playerId,
                        NewRank = newRank,
                        PreviousRank = previousRank,
                        RankImproved = newRank < previousRank,
                        RankChange = previousRank - newRank,
                        Timestamp = DateTime.UtcNow
                    });

                _logger.LogInformation("Rank change broadcasted for player {PlayerId}: {PreviousRank} -> {NewRank}",
                    playerId, previousRank, newRank);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting rank change for player {PlayerId}", playerId);
            }
        }
    }
}