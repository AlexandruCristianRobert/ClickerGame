using ClickerGame.Upgrades.Application.DTOs;
using ClickerGame.Upgrades.Domain.Entities;
using ClickerGame.Upgrades.Domain.ValueObjects;

namespace ClickerGame.Upgrades.Application.Services
{
    public interface IUpgradePurchaseTransactionService
    {
        Task<TransactionResult> ExecutePurchaseTransactionAsync(
            Guid playerId,
            PurchaseUpgradeRequest request,
            ValidationResult validationResult);

        Task<TransactionResult> ExecuteBulkPurchaseTransactionAsync(
            Guid playerId,
            BulkPurchaseRequest request,
            ValidationResult validationResult);

        Task RollbackTransactionAsync(string transactionId);
        Task<TransactionStatus> GetTransactionStatusAsync(string transactionId);
    }

    public class TransactionResult
    {
        public bool Success { get; init; }
        public string TransactionId { get; init; } = string.Empty;
        public UpgradePurchaseResult? PurchaseResult { get; init; }
        public List<string> Errors { get; init; } = new();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public TransactionMetadata Metadata { get; init; } = new();
    }

    public class TransactionMetadata
    {
        public BigNumber OriginalScore { get; init; }
        public BigNumber FinalScore { get; init; }
        public BigNumber AmountDeducted { get; init; }
        public List<string> ModifiedUpgrades { get; init; } = new();
        public string? RollbackInfo { get; init; }
    }

    public enum TransactionStatus
    {
        Pending,
        Completed,
        Failed,
        RolledBack,
        Unknown
    }
}