using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;

namespace GoldenCornOrder.Services
{
    public class LineNotificationService
    {
        private readonly HttpClient _httpClient;
        private readonly AppDbContext _context;
        private readonly ILogger<LineNotificationService> _logger;

        public LineNotificationService(HttpClient httpClient, AppDbContext context, ILogger<LineNotificationService> logger)
        {
            _httpClient = httpClient;
            _context = context;
            _logger = logger;
        }

        public async Task SendNewOrderAlertAsync(Order order)
        {
            try
            {
                var tokenSetting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "LineChannelAccessToken");
                var userIdSetting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "LineAdminUserId");

                var token = tokenSetting?.Value?.Trim();
                var userId = userIdSetting?.Value?.Trim();

                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userId))
                {
                    return; // LINE not configured yet, skip quietly
                }

                var itemsText = string.Join("\n", order.Items.Select(i =>
                {
                    var opt = string.IsNullOrWhiteSpace(i.SelectedOptionsSummary) ? "" : $"\n  配料: {i.SelectedOptionsSummary}";
                    var note = string.IsNullOrWhiteSpace(i.ItemNote) ? "" : $"\n  備註: {i.ItemNote}";
                    return $"• {i.MenuItemName} x{i.Quantity} (${i.SubTotal:0}){opt}{note}";
                }));

                var diningTypeText = order.DiningType == "DineIn" ? $"🍽️ 內用 (桌號: {order.TableNumber})" : "🥡 外帶自取";
                var paymentText = order.PaymentMethod switch
                {
                    "Cash" => "💵 現場付款",
                    "LinePay" => "💚 LINE Pay",
                    "BankTransfer" => $"🏦 銀行轉帳 (末5碼: {order.TransferLast5})",
                    _ => order.PaymentMethod
                };

                var message = $"🌽【Golden Corn 收到新訂單！】\n" +
                              $"━━━━━━━━━━━━━━━━\n" +
                              $"📋 單號：{order.OrderNumber}\n" +
                              $"👤 顧客：{order.CustomerName} ({order.CustomerPhone})\n" +
                              $"🍽️ 方式：{diningTypeText}\n" +
                              $"⏰ 取餐時間：{order.PickupTime}\n" +
                              $"💰 金額：${order.TotalAmount:0} ({paymentText})\n" +
                              $"━━━━━━━━━━━━━━━━\n" +
                              $"🍖 訂購餐點：\n{itemsText}\n" +
                              (string.IsNullOrWhiteSpace(order.CustomerNote) ? "" : $"📝 全單備註：{order.CustomerNote}\n") +
                              $"━━━━━━━━━━━━━━━━\n" +
                              $"👉 店家後台查看：\nhttps://goldencorn.onrender.com/admin.html";

                await PushMessageToLineAsync(token, userId, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send LINE order notification");
            }
        }

        public async Task<(bool Success, string Message)> SendTestMessageAsync(string? testToken = null, string? testUserId = null)
        {
            try
            {
                var token = testToken?.Trim();
                var userId = testUserId?.Trim();

                if (string.IsNullOrWhiteSpace(token))
                {
                    var setting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "LineChannelAccessToken");
                    token = setting?.Value?.Trim();
                }

                if (string.IsNullOrWhiteSpace(userId))
                {
                    var setting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "LineAdminUserId");
                    userId = setting?.Value?.Trim();
                }

                if (string.IsNullOrWhiteSpace(token))
                {
                    return (false, "請先輸入 LINE Channel Access Token");
                }

                if (string.IsNullOrWhiteSpace(userId))
                {
                    return (false, "請先輸入您的 LINE User ID");
                }

                var testMsg = $"🌽【Golden Corn 測試推播成功！】\n" +
                              $"━━━━━━━━━━━━━━━━\n" +
                              $"恭喜您！您的 LINE 已成功綁定 Golden Corn 點餐系統。\n" +
                              $"以後只要有顧客線上點餐，您的手機就會即時收到通知與明細！\n" +
                              $"發送時間：{TaiwanTimeHelper.FormattedNow}";

                var (success, error) = await PushMessageToLineAsync(token, userId, testMsg);
                if (success)
                {
                    return (true, "測試推播發送成功！請查看您的手機 LINE。");
                }
                return (false, $"LINE API 回傳錯誤: {error}");
            }
            catch (Exception ex)
            {
                return (false, $"連線發生例外錯誤: {ex.Message}");
            }
        }

        private async Task<(bool Success, string Error)> PushMessageToLineAsync(string token, string userId, string text)
        {
            var payload = new
            {
                to = userId,
                messages = new[]
                {
                    new { type = "text", text = text }
                }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/push");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }

            var errBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("LINE Push Error {StatusCode}: {ErrorBody}", response.StatusCode, errBody);
            return (false, $"{response.StatusCode} - {errBody}");
        }
    }
}
