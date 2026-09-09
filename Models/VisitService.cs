using YAGOT_2._0.Services;

namespace YAGOT_2._0.Models
{
    public class VisitService : IVisitService
    {
        private readonly IVisitBackgroundQueue _queue;

        public VisitService(IVisitBackgroundQueue queue)
        {
            _queue = queue;
        }

        public Task SaveVisitAsync(HttpContext context, string? name = null)
        {
            try
            {
                string visitorName = !string.IsNullOrWhiteSpace(name) ? name! : "زائر";
                string userAgent = context.Request.Headers["User-Agent"].ToString();

                string? ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(ip))
                {
                    ip = ip.Split(',')[0].Trim();
                }
                else
                {
                    ip = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
                }

                _queue.QueueVisit(new VisitQueueItem(
                    ip,
                    userAgent,
                    visitorName,
                    DateTime.UtcNow));
            }
            catch
            {
                // Never allow visit tracking to disrupt web requests
            }

            return Task.CompletedTask;
        }
    }
}

