using ClickerGame.GameCore.Domain.Enums;

namespace ClickerGame.GameCore.Application.DTOs.Notifications
{
    public class ErrorNotificationDto : BaseNotificationDto
    {
        public ErrorNotificationDto()
        {
            Type = NotificationType.Error;
            Priority = NotificationPriority.High;
            DisplayDuration = TimeSpan.FromSeconds(8);
        }

        public string ErrorCode { get; init; } = string.Empty;
        public string ErrorType { get; init; } = string.Empty;
        public string? StackTrace { get; init; }
        public Dictionary<string, string> ErrorContext { get; init; } = new();
        public bool IsRetryable { get; init; } = false;
        public string? RetryAction { get; init; }
    }
}