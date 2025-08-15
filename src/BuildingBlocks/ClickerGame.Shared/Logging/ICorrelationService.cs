namespace ClickerGame.Shared.Logging
{
    public interface ICorrelationService
    {
        string GetCorrelationId();
        string GetRequestId();
        CorrelationContext GetContext();
        void SetCorrelationId(string correlationId);
        void SetUserId(string userId, string userName = "");
        void SetServiceName(string serviceName);
        void SetRequestInfo(string path, string method, string clientIp, string userAgent);
    }
}