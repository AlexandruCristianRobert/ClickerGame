namespace ClickerGame.GameCore.Application.DTOs
{
    public class UpgradeEffectsDto
    {
        public decimal ClickPowerBonus { get; init; }
        public decimal PassiveIncomeBonus { get; init; }
        public decimal MultiplierBonus { get; init; } = 1.0m;
        public string SourceUpgradeId { get; init; } = string.Empty;
        public string UpgradeName { get; init; } = string.Empty;
        public int UpgradeLevel { get; init; }
        public Dictionary<string, object> AdditionalEffects { get; init; } = new();
    }
}