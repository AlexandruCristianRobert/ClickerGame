using ClickerGame.GameCore.Application.DTOs;

namespace ClickerGame.GameCore.Application.Services
{
    public interface IScoreBroadcastService
    {
        Task BroadcastScoreUpdateAsync(Guid playerId, ScoreUpdateDto scoreUpdate);
        Task BroadcastLeaderboardUpdateAsync(ScoreLeaderboardUpdateDto leaderboardUpdate);
        Task BroadcastMilestoneAchievedAsync(Guid playerId, string milestone, string score);
        Task BroadcastScoreRankChangeAsync(Guid playerId, int newRank, int previousRank);
    }
}