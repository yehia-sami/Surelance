using Hangfire.Dashboard;
using System.Net;

namespace Surelance.Infrastructure.Jobs;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _isDevelopment;

    public HangfireAuthorizationFilter(bool isDevelopment = true)
    {
        _isDevelopment = isDevelopment;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In local dev, allow loopback so inspecting the dashboard doesn't require juggling auth headers
        if (_isDevelopment)
        {
            var remoteIp = httpContext.Connection.RemoteIpAddress;
            if (remoteIp != null && (IPAddress.IsLoopback(remoteIp) || remoteIp.Equals(httpContext.Connection.LocalIpAddress)))
            {
                return true;
            }
        }

        // Remote requests require an Arbitrator/Admin token
        return httpContext.User.Identity?.IsAuthenticated == true &&
               (httpContext.User.IsInRole("Arbitrator") || httpContext.User.IsInRole("Admin"));
    }
}
