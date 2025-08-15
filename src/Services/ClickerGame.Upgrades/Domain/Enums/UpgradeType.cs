namespace ClickerGame.Upgrades.Domain.Enums
{
    public enum UpgradeType
    {
        Linear = 1,        // Effect increases linearly (e.g., +10 per level)
        Exponential = 2,   // Effect increases exponentially (e.g., *1.5 per level)  
        Percentage = 3,    // Percentage-based improvement (e.g., +5% per level)
        Compound = 4,      // Compound percentage (e.g., 1.05^level)
        Threshold = 5,     // Unlocks at specific thresholds
        OneTime = 6        // Single purchase only
    }
}