namespace ClickerGame.GameCore.Application.Services
{
    public interface IScoreUpdateThrottleService
    {
        Task<bool> CanSendScoreUpdateAsync(Guid playerId);
        Task RecordScoreUpdateAsync(Guid playerId);
        Task<TimeSpan> GetRemainingThrottleTimeAsync(Guid playerId);
        Task ClearThrottleAsync(Guid playerId);
        Task<ScoreUpdateThrottleInfo> GetThrottleInfoAsync(Guid playerId);
    }

    public class ScoreUpdateThrottleInfo
    {
        public Guid PlayerId { get; init; }
        public bool IsThrottled { get; init; }
        public DateTime LastUpdate { get; init; }
        public TimeSpan RemainingThrottleTime { get; init; }
        public int UpdatesInCurrentWindow { get; init; }
        public int MaxUpdatesPerWindow { get; init; }
    }
}