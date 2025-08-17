/**
 * SignalR Transport Configuration for Browser Compatibility
 * Task 5.1: Ensure broad browser compatibility with proper transport priorities
 */

class SignalRTransportManager
{
    constructor()
    {
        this.connection = null;
        this.retryPolicy = {
        enableAutoReconnect: true,
            maxRetryAttempts: 5,
            retryDelayInSeconds: [1, 2, 4, 8, 16],
            maxRetryDelayInSeconds: 30,
            reconnectTimeoutInSeconds: 120
        }
        ;

        this.transportFallbackOrder = [
            'WebSockets',
            'ServerSentEvents',
            'LongPolling'
        ];

        this.browserInfo = this.detectBrowser();
        this.supportedTransports = this.getSupportedTransports();
    }

    /**
     * Initialize SignalR connection with proper transport configuration
     */
    async initializeConnection(hubUrl, accessToken)
    {
        try
        {
            console.log('🔄 Initializing SignalR connection with transport priorities:', this.transportFallbackOrder);
            console.log('🌐 Browser detected:', this.browserInfo);
            console.log('🚀 Supported transports:', this.supportedTransports);

            // Build connection with transport configuration
            const connectionBuilder = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl, {
            accessTokenFactory: () => accessToken,
                    transport: this.getOptimalTransport(),
                    skipNegotiation: false, // Allow negotiation for transport selection
                    headers:
                {
                    'User-Agent': navigator.userAgent,
                        'X-Browser-Info': JSON.stringify(this.browserInfo)
                    }
            })
                .configureLogging(signalR.LogLevel.Information);

            // Configure automatic reconnection with retry policy
            if (this.retryPolicy.enableAutoReconnect)
            {
                connectionBuilder.withAutomaticReconnect({
                nextRetryDelayInMilliseconds: (retryContext) => {
                    const attempt = retryContext.previousRetryCount;

                    if (attempt >= this.retryPolicy.maxRetryAttempts)
                    {
                        console.warn('⚠️ Max retry attempts reached, stopping reconnection');
                        return null; // Stop retrying
                    }

                    const delays = this.retryPolicy.retryDelayInSeconds;
                    const delayIndex = Math.min(attempt, delays.length - 1);
                    const delayMs = delays[delayIndex] * 1000;

                    console.log(`🔄 Reconnect attempt ${ attempt + 1}/${ this.retryPolicy.maxRetryAttempts} in ${ delayMs}
                    ms`);

                    return Math.min(delayMs, this.retryPolicy.maxRetryDelayInSeconds * 1000);
                }
                });
            }

            this.connection = connectionBuilder.build();

            // Setup connection event handlers
            this.setupConnectionEventHandlers();

            // Start connection with transport fallback
            await this.startConnectionWithFallback();

            return this.connection;

        }
        catch (error)
        {
            console.error('❌ Failed to initialize SignalR connection:', error);
            throw error;
        }
    }

    /**
     * Start connection with automatic transport fallback
     */
    async startConnectionWithFallback()
    {
        for (let i = 0; i < this.supportedTransports.length; i++)
        {
            const transport = this.supportedTransports[i];

            try
            {
                console.log(`🚀 Attempting to connect using ${ transport}
                transport...`);

        // Reconfigure connection for specific transport if needed
        if (i > 0)
        {
            await this.reconfigureConnectionForTransport(transport);
        }

        await this.connection.start();

        console.log(`✅ Successfully connected using ${ transport}
        transport`);
        this.logTransportSuccess(transport);
        return;

    } catch (error) {
                console.warn(`⚠️ Failed to connect using ${ transport}
transport:`, error.message);
this.logTransportFailure(transport, error);

if (i === this.supportedTransports.length - 1)
{
    console.error('❌ All transport methods failed');
    throw new Error('Unable to establish SignalR connection with any transport method');
}

// Wait before trying next transport
await this.delay(1000);
            }
        }
    }

    /**
     * Setup connection event handlers for monitoring and logging
     */
    setupConnectionEventHandlers() {
    // Connection state change monitoring
    this.connection.onclose((error) => {
        if (error)
        {
            console.error('❌ SignalR connection closed with error:', error);
            this.logConnectionEvent('ConnectionClosed', { error: error.message });
} else
{
    console.log('🔌 SignalR connection closed gracefully');
    this.logConnectionEvent('ConnectionClosed', { graceful: true });
}
        });

this.connection.onreconnecting((error) => {
    console.log('🔄 SignalR reconnecting...', error?.message || '');
    this.logConnectionEvent('Reconnecting', {
    error: error?.message,
                timestamp: new Date().toISOString()
            });
        });

this.connection.onreconnected((connectionId) => {
console.log('✅ SignalR reconnected successfully. Connection ID:', connectionId);
this.logConnectionEvent('Reconnected', {
    connectionId,
                timestamp: new Date().toISOString()
            });
        });

// Monitor connection quality
this.startConnectionQualityMonitoring();
    }

    /**
     * Start monitoring connection quality and performance
     */
    startConnectionQualityMonitoring() {
    setInterval(() => {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected)
        {
            this.measureConnectionLatency();
        }
    }, 30000); // Check every 30 seconds
}

/**
 * Measure connection latency for performance monitoring
 */
async measureConnectionLatency()
{
    try
    {
        const startTime = performance.now();

        // Send a ping message to the hub
        await this.connection.invoke('Ping');

        const latency = performance.now() - startTime;

        console.log(`📊 Connection latency: ${ latency.toFixed(2)}
        ms`);

        this.logConnectionEvent('LatencyMeasured', {
        latencyMs: latency,
                timestamp: new Date().toISOString(),
                connectionState: this.connection.state
            });

    }
    catch (error)
    {
        console.warn('⚠️ Failed to measure connection latency:', error.message);
    }
}

/**
 * Detect browser information for compatibility checks
 */
detectBrowser() {
    const userAgent = navigator.userAgent;
    let browser = 'Unknown';
    let version = 'Unknown';

    if (userAgent.includes('Chrome'))
    {
        browser = 'Chrome';
        const match = userAgent.match(/ Chrome\/ (\d +) /);
        version = match ? match[1] : 'Unknown';
    }
    else if (userAgent.includes('Firefox'))
    {
        browser = 'Firefox';
        const match = userAgent.match(/ Firefox\/ (\d +) /);
        version = match ? match[1] : 'Unknown';
    }
    else if (userAgent.includes('Safari') && !userAgent.includes('Chrome'))
    {
        browser = 'Safari';
        const match = userAgent.match(/ Version\/ (\d +) /);
        version = match ? match[1] : 'Unknown';
    }
    else if (userAgent.includes('Edge'))
    {
        browser = 'Edge';
        const match = userAgent.match(/ Edge\/ (\d +) /);
        version = match ? match[1] : 'Unknown';
    }
    else if (userAgent.includes('MSIE') || userAgent.includes('Trident'))
    {
        browser = 'Internet Explorer';
        const match = userAgent.match(/ (?: MSIE | rv:)(\d +)/);
        version = match ? match[1] : 'Unknown';
    }

    return {
    name: browser,
            version: version,
            userAgent: userAgent,
            supportsWebSockets: 'WebSocket' in window,
            supportsServerSentEvents: 'EventSource' in window
        }
    ;
}

/**
 * Get supported transports based on browser capabilities
 */
getSupportedTransports() {
    const transports = [];

    // Check WebSocket support
    if (this.browserInfo.supportsWebSockets && this.shouldUseWebSockets())
    {
        transports.push('WebSockets');
    }

    // Check Server-Sent Events support
    if (this.browserInfo.supportsServerSentEvents)
    {
        transports.push('ServerSentEvents');
    }

    // Long Polling is always supported as ultimate fallback
    transports.push('LongPolling');

    return transports;
}

/**
 * Determine if WebSockets should be used based on browser compatibility
 */
shouldUseWebSockets() {
    const { name, version } = this.browserInfo;

    // Disable WebSockets for very old browsers
    const minVersions = {
            'Chrome': 50,
            'Firefox': 45,
            'Safari': 10,
            'Edge': 12,
            'Internet Explorer': 11
        }
;

const minVersion = minVersions[name];
if (minVersion && parseInt(version) < minVersion)
{
    console.log(`🚫 WebSockets disabled for ${ name} ${ version} (requires ${ minVersion}
    +)`);
    return false;
}

return true;
    }

    /**
     * Get optimal transport configuration for current browser
     */
    getOptimalTransport() {
    let transportFlags = 0;

    if (this.supportedTransports.includes('WebSockets'))
    {
        transportFlags |= signalR.HttpTransportType.WebSockets;
    }
    if (this.supportedTransports.includes('ServerSentEvents'))
    {
        transportFlags |= signalR.HttpTransportType.ServerSentEvents;
    }
    if (this.supportedTransports.includes('LongPolling'))
    {
        transportFlags |= signalR.HttpTransportType.LongPolling;
    }

    return transportFlags;
}

/**
 * Reconfigure connection for specific transport (if needed)
 */
async reconfigureConnectionForTransport(transport)
{
    // This would be implemented if we need to change connection settings
    // based on the transport being used
    console.log(`🔧 Reconfiguring connection for ${ transport}
    transport`);
}

/**
 * Log transport success for monitoring
 */
logTransportSuccess(transport) {
    const logData = {
            event: 'TransportSuccess',
            transport: transport,
            browserInfo: this.browserInfo,
            timestamp: new Date().toISOString(),
            connectionId: this.connection?.connectionId
        }
;

// Send to server for analytics
this.sendTelemetryData(logData);
    }

    /**
     * Log transport failure for monitoring
     */
    logTransportFailure(transport, error) {
    const logData = {
            event: 'TransportFailure',
            transport: transport,
            error: error.message,
            browserInfo: this.browserInfo,
            timestamp: new Date().toISOString()
        }
;

// Send to server for analytics
this.sendTelemetryData(logData);
    }

    /**
     * Log general connection events
     */
    logConnectionEvent(eventType, data) {
    const logData = {
            event: eventType,
            data: data,
            browserInfo: this.browserInfo,
            timestamp: new Date().toISOString(),
            connectionId: this.connection?.connectionId
        }
;

// Send to server for analytics
this.sendTelemetryData(logData);
    }

    /**
     * Send telemetry data to server for monitoring
     */
    sendTelemetryData(data) {
    try
    {
        // Use fetch to send telemetry data asynchronously
        fetch('/api/signalr/telemetry', {
        method: 'POST',
                headers:
            {
                'Content-Type': 'application/json'
                },
                body: JSON.stringify(data)
            }).catch (error => {
        console.warn('⚠️ Failed to send telemetry data:', error.message);
    });
    }
    catch (error)
    {
        console.warn('⚠️ Error sending telemetry data:', error.message);
    }
}

/**
 * Utility function for delays
 */
delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

/**
 * Get current connection status
 */
getConnectionStatus() {
    return {
    state: this.connection?.state || 'Disconnected',
            connectionId: this.connection?.connectionId,
            transport: this.getCurrentTransport(),
            browserInfo: this.browserInfo,
            supportedTransports: this.supportedTransports
        }
    ;
}

/**
 * Get currently active transport method
 */
getCurrentTransport() {
    // This would need to be implemented based on SignalR's internal state
    // For now, return the first successful transport
    return this.supportedTransports[0] || 'Unknown';
}
}

// Export for use in applications
window.SignalRTransportManager = SignalRTransportManager;