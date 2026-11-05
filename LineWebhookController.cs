using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;
using GoldenCornOrder.Services;

namespace GoldenCornOrder.Controllers
{
    [ApiController]
    [Route("api/line/webhook")]
    public class LineWebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly LineNotificationService _lineService;
        private readonly ILogger<LineWebhookController> _logger;

        public LineWebhookController(AppDbContext context, LineNotificationService lineService, ILogger<LineWebhookController> logger)
        {
            _context = context;
            _lineService = lineService;
            _logger = logger;
        }

        // GET: api/line/webhook (for testing connection in browser)
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { status = "active", message = "LINE Webhook endpoint ready" });
        }

        // POST: api/line/webhook
        [HttpPost]
        public async Task<IActionResult> HandleWebhook([FromBody] JsonElement payload)
        {
            try
            {
                if (payload.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ev in events.EnumerateArray())
                    {
                        if (ev.TryGetProperty("source", out var source) && source.TryGetProperty("userId", out var userIdEl))
                        {
                            var userId = userIdEl.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(userId))
                            {
                                var existing = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "LineAdminUserId");
                                if (existing != null)
                                {
                                    existing.Value = userId;
                                    existing.UpdatedAt = TaiwanTimeHelper.Now;
                                }
                                else
                                {
                                    _context.StoreSettings.Add(new StoreSetting
                                    {
                                        Key = "LineAdminUserId",
                                        Value = userId,
                                        Description = "老闆的 LINE User ID",
                                        UpdatedAt = TaiwanTimeHelper.Now
                                    });
                                }
                                await _context.SaveChangesAsync();

                                _logger.LogInformation("Successfully bound LineAdminUserId via Webhook: {UserId}", userId);

                                // Trigger confirmation push message
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await Task.Delay(500);
                                        await _lineService.SendTestMessageAsync(null, userId);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Failed to send auto-reply after webhook binding");
                                    }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LINE Webhook");
            }

            return Ok();
        }
    }
}
