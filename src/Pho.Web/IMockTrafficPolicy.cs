using Microsoft.AspNetCore.Http;

namespace Pho.Web;

/// <summary>
/// Decides whether an inbound request belongs to the mock-serving surface (port 8081) or the
/// admin UI (port 8080). Abstracted so tests can force either side without real sockets.
/// </summary>
public interface IMockTrafficPolicy
{
    bool IsMockTraffic(HttpContext context);
}

/// <summary>Treats requests arriving on the configured mock port as mock traffic.</summary>
public sealed class PortMockTrafficPolicy : IMockTrafficPolicy
{
    private readonly int _mockPort;

    public PortMockTrafficPolicy(int mockPort)
    {
        _mockPort = mockPort;
    }

    public bool IsMockTraffic(HttpContext context)
        => context.Connection.LocalPort == _mockPort;
}
