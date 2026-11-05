using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;
using GoldenCornOrder.Services;

namespace GoldenCornOrder.Controllers
{
    [ApiController]
    [Route("api/order")]
    public class OrderApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly LineNotificationService _lineService;

        public OrderApiController(AppDbContext context, LineNotificationService lineService)
        {
            _context = context;
            _lineService = lineService;
        }

        // POST: api/order
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            if (dto == null || dto.Items == null || dto.Items.Count == 0)
            {
                return BadRequest(new { message = "訂單品項不能為空" });
            }

            if (string.IsNullOrWhiteSpace(dto.CustomerName))
            {
                return BadRequest(new { message = "請填寫訂購人姓名" });
            }

            if (string.IsNullOrWhiteSpace(dto.CustomerPhone))
            {
                return BadRequest(new { message = "請填寫聯絡電話" });
            }

            // Check store open status
            var isOpenSetting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "IsOpen");
            if (isOpenSetting != null && isOpenSetting.Value == "false")
            {
                return BadRequest(new { message = "本店目前公休中，暫停線上點餐服務，敬請見諒！" });
            }

            var pickupTime = (dto.PickupTime ?? "").Trim();

            // Option B: Outside live business hours (Thu-Sat 16:30-22:30), immediate preparation ("儘速製作") is blocked.
            // Customers must schedule an advance pickup date and time.
            if (!TaiwanTimeHelper.IsLiveBusinessHours)
            {
                if (dto.DiningType == "DineIn")
                {
                    return BadRequest(new { message = "目前非現場營業時段（營業時段：週四至週六 16:30-22:30），僅接受預約外帶取餐，請改選「外帶自取」並指定預約取餐時間。" });
                }

                if (string.IsNullOrWhiteSpace(pickupTime) || pickupTime.Contains("儘速") || pickupTime.Contains("尽速"))
                {
                    return BadRequest(new { message = "目前非現場即時出餐時段（營業時段：週四至週六 16:30-22:30），無法選擇「儘速製作」。請填寫預計取餐日期與時間（例如：週五 18:00 或 10/24 17:30）。" });
                }
            }

            // 30分鐘出餐金額產能上限控管（例如每30分鐘限額 NT$ 2,000）
            if (!string.IsNullOrWhiteSpace(pickupTime) && !pickupTime.Contains("儘速") && !pickupTime.Contains("尽速") && !pickupTime.Contains("現場"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(pickupTime, @"(\d{1,2})[:：](\d{2})");
                if (!match.Success) match = System.Text.RegularExpressions.Regex.Match(pickupTime, @"(\d{2})(\d{2})");
                if (match.Success)
                {
                    int h = int.Parse(match.Groups[1].Value);
                    int m = int.Parse(match.Groups[2].Value);
                    int slotM = m < 30 ? 0 : 30;
                    string targetSlotKey = $"{h:D2}:{slotM:D2}";
                    int endH = slotM == 30 ? h + 1 : h;
                    int endM = slotM == 30 ? 0 : 30;
                    string slotLabel = $"{targetSlotKey} - {endH:D2}:{endM:D2}";

                    var limitSetting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "SlotAmountLimit");
                    var limitVal = decimal.TryParse(limitSetting?.Value, out var parsedLimit) ? parsedLimit : 2000;

                    var todayDate = TaiwanTimeHelper.Today;
                    var startUtc = todayDate.AddHours(-8);
                    var endUtc = startUtc.AddDays(1);

                    var slotOrders = await _context.Orders
                        .Where(o => o.OrderStatus != "Cancelled" &&
                               ((o.CreatedAt >= todayDate && o.CreatedAt < todayDate.AddDays(1)) ||
                                (o.CreatedAt >= startUtc && o.CreatedAt < endUtc)))
                        .ToListAsync();

                    decimal currentSlotSum = 0;
                    foreach (var so in slotOrders)
                    {
                        var soPickup = (so.PickupTime ?? "").Trim();
                        string? soSlot = null;
                        if (string.IsNullOrWhiteSpace(soPickup) || soPickup.Contains("儘速") || soPickup.Contains("尽速") || soPickup.Contains("現場"))
                        {
                            var tw = so.CreatedAt.Kind == DateTimeKind.Utc ? so.CreatedAt.AddHours(8) : so.CreatedAt;
                            int som = tw.Minute < 30 ? 0 : 30;
                            soSlot = $"{tw.Hour:D2}:{som:D2}";
                        }
                        else
                        {
                            var soMatch = System.Text.RegularExpressions.Regex.Match(soPickup, @"(\d{1,2})[:：](\d{2})");
                            if (!soMatch.Success) soMatch = System.Text.RegularExpressions.Regex.Match(soPickup, @"(\d{2})(\d{2})");
                            if (soMatch.Success)
                            {
                                int soh = int.Parse(soMatch.Groups[1].Value);
                                int som = int.Parse(soMatch.Groups[2].Value);
                                int soslotM = som < 30 ? 0 : 30;
                                soSlot = $"{soh:D2}:{soslotM:D2}";
                            }
                        }

                        if (soSlot == targetSlotKey)
                        {
                            currentSlotSum += so.TotalAmount;
                        }
                    }

                    if (currentSlotSum >= limitVal)
                    {
                        return BadRequest(new { message = $"很抱歉！預約取餐時段「{slotLabel}」名額已額滿，為維護慢火現烤出餐品質，請改選其他時段，謝謝您的體諒！" });
                    }
                }
            }

            // Generate Order Number: GC-yyyyMMdd-001 using Taiwan Timezone (UTC+8)
            var today = TaiwanTimeHelper.Today;
            var tomorrow = today.AddDays(1);
            var todayStartUtc = today.AddHours(-8);
            var tomorrowEndUtc = todayStartUtc.AddDays(1);
            var todayOrdersCount = await _context.Orders
                .CountAsync(o => (o.CreatedAt >= today && o.CreatedAt < tomorrow) || (o.CreatedAt >= todayStartUtc && o.CreatedAt < tomorrowEndUtc));
            
            var orderSeq = (todayOrdersCount + 1).ToString("D3");
            var orderNumber = $"GC-{today:yyyyMMdd}-{orderSeq}";

            var order = new Order
            {
                OrderNumber = orderNumber,
                CustomerName = dto.CustomerName.Trim(),
                CustomerPhone = dto.CustomerPhone.Trim(),
                DiningType = dto.DiningType == "DineIn" ? "DineIn" : "Takeout",
                TableNumber = dto.DiningType == "DineIn" ? dto.TableNumber?.Trim() : null,
                PickupTime = string.IsNullOrWhiteSpace(pickupTime) ? "儘速製作" : pickupTime,
                CustomerNote = dto.CustomerNote?.Trim(),
                PaymentMethod = dto.PaymentMethod,
                PaymentStatus = dto.PaymentMethod == "Cash" ? "Unpaid" : "Paid",
                TransferLast5 = dto.TransferLast5?.Trim(),
                OrderStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            decimal totalAmount = 0;

            foreach (var itemDto in dto.Items)
            {
                var menuItem = await _context.MenuItems
                    .Include(m => m.OptionGroups)
                        .ThenInclude(g => g.Options)
                    .FirstOrDefaultAsync(m => m.Id == itemDto.MenuItemId);

                if (menuItem == null)
                {
                    return BadRequest(new { message = $"找不到品項 ID: {itemDto.MenuItemId}" });
                }

                if (!menuItem.IsAvailable || (menuItem.StockQuantity.HasValue && menuItem.StockQuantity.Value <= 0))
                {
                    return BadRequest(new { message = $"餐點「{menuItem.Name}」今日已售完，無法下單！" });
                }

                // 檢查並扣除限量庫存配額
                if (menuItem.StockQuantity.HasValue && menuItem.StockQuantity.Value >= 0)
                {
                    if (menuItem.StockQuantity.Value < itemDto.Quantity)
                    {
                        return BadRequest(new { 
                            message = $"很抱歉！「{menuItem.Name}」今日僅剩 {menuItem.StockQuantity.Value} 份，您點了 {itemDto.Quantity} 份，數量不足，請調整數量後再送出！" 
                        });
                    }

                    menuItem.StockQuantity -= itemDto.Quantity;
                    if (menuItem.StockQuantity <= 0)
                    {
                        menuItem.StockQuantity = 0;
                        menuItem.IsAvailable = false; // 賣完最後一份自動標記為已售完
                    }
                }

                decimal itemBasePrice = menuItem.Price;
                decimal itemOptionsExtra = 0;
                var selectedOptionsSummaryList = new List<string>();
                var orderItemOptions = new List<OrderItemOption>();

                if (itemDto.SelectedOptionItemIds != null && itemDto.SelectedOptionItemIds.Count > 0)
                {
                    foreach (var optId in itemDto.SelectedOptionItemIds)
                    {
                        var optItem = await _context.OptionItems
                            .Include(o => o.OptionGroup)
                            .FirstOrDefaultAsync(o => o.Id == optId);

                        if (optItem != null)
                        {
                            if (!optItem.IsAvailable)
                            {
                                return BadRequest(new { message = $"配料「{optItem.Name}」今日已售完" });
                            }

                            itemOptionsExtra += optItem.ExtraPrice;
                            selectedOptionsSummaryList.Add($"{optItem.OptionGroup?.Name}: {optItem.Name}{(optItem.ExtraPrice > 0 ? $"(+${optItem.ExtraPrice})" : "")}");

                            orderItemOptions.Add(new OrderItemOption
                            {
                                OptionItemId = optItem.Id,
                                OptionName = optItem.Name,
                                ExtraPrice = optItem.ExtraPrice,
                                OptionGroupName = optItem.OptionGroup?.Name ?? ""
                            });
                        }
                    }
                }

                var itemUnitPrice = itemBasePrice + itemOptionsExtra;
                var itemSubTotal = itemUnitPrice * itemDto.Quantity;
                totalAmount += itemSubTotal;

                var orderItem = new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    MenuItemName = menuItem.Name,
                    UnitPrice = itemUnitPrice,
                    Quantity = itemDto.Quantity,
                    SubTotal = itemSubTotal,
                    ItemNote = itemDto.ItemNote?.Trim(),
                    SelectedOptionsSummary = string.Join(" | ", selectedOptionsSummaryList),
                    Options = orderItemOptions
                };

                order.Items.Add(orderItem);
            }

            order.TotalAmount = totalAmount;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // Send LINE Push Notification in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _lineService.SendNewOrderAlertAsync(order);
                }
                catch { }
            });

            var response = new OrderResponseDto
            {
                Success = true,
                Message = "訂單建立成功！",
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                TotalAmount = order.TotalAmount,
                OrderStatus = order.OrderStatus,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TransferLast5 = order.TransferLast5,
                CreatedAt = order.CreatedAt
            };

            return Ok(response);
        }

        // GET: api/order/{orderNumber}
        [HttpGet("{orderNumber}")]
        public async Task<IActionResult> GetOrderDetail(string orderNumber)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Options)
                .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

            if (order == null)
            {
                return NotFound(new { message = "找不到該訂單" });
            }

            var dto = new OrderDetailDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                CustomerPhone = order.CustomerPhone,
                DiningType = order.DiningType,
                TableNumber = order.TableNumber,
                PickupTime = order.PickupTime,
                CustomerNote = order.CustomerNote,
                KitchenNote = order.KitchenNote,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                TransferLast5 = order.TransferLast5,
                TotalAmount = order.TotalAmount,
                OrderStatus = order.OrderStatus,
                CreatedAt = order.CreatedAt,
                Items = order.Items.Select(i => new OrderItemDetailDto
                {
                    Id = i.Id,
                    MenuItemId = i.MenuItemId,
                    MenuItemName = i.MenuItemName,
                    UnitPrice = i.UnitPrice,
                    Quantity = i.Quantity,
                    SubTotal = i.SubTotal,
                    ItemNote = i.ItemNote,
                    SelectedOptionsSummary = i.SelectedOptionsSummary ?? string.Empty,
                    Options = i.Options.Select(o => new OrderItemOptionDetailDto
                    {
                        OptionItemId = o.OptionItemId,
                        OptionName = o.OptionName,
                        ExtraPrice = o.ExtraPrice,
                        OptionGroupName = o.OptionGroupName
                    }).ToList()
                }).ToList()
            };

            return Ok(dto);
        }

        // GET: api/order/lookup?phone=0912345678
        [HttpGet("lookup")]
        public async Task<IActionResult> LookupOrders([FromQuery] string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { message = "請提供查詢電話" });
            }

            var p = phone.Trim();
            var orders = await _context.Orders
                .Where(o => o.CustomerPhone == p)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.CustomerName,
                    o.DiningType,
                    o.TableNumber,
                    o.PickupTime,
                    o.TotalAmount,
                    o.OrderStatus,
                    o.PaymentMethod,
                    o.PaymentStatus,
                    o.TransferLast5,
                    o.CreatedAt
                })
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/order/{orderNumber}/status
        [HttpGet("{orderNumber}/status")]
        public async Task<IActionResult> GetOrderStatus(string orderNumber)
        {
            var order = await _context.Orders
                .Where(o => o.OrderNumber == orderNumber)
                .Select(o => new { o.OrderNumber, o.OrderStatus, o.PaymentStatus, o.UpdatedAt })
                .FirstOrDefaultAsync();

            if (order == null)
            {
                return NotFound(new { message = "找不到該訂單" });
            }

            return Ok(order);
        }

        public class TimeSlotDto
        {
            public string Time { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
            public bool IsFull { get; set; }
            public bool IsPast { get; set; }
        }

        // GET: api/order/slots?date=2026-11-06
        [HttpGet("slots")]
        public async Task<IActionResult> GetAvailableSlots([FromQuery] string? date)
        {
            var now = TaiwanTimeHelper.Now;
            DateTime targetDate = now.Date;
            if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
            {
                targetDate = parsedDate.Date;
            }

            var settings = await _context.StoreSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
            var limitStr = settings.GetValueOrDefault("SlotAmountLimit", "2000");
            if (!decimal.TryParse(limitStr, out var limit)) limit = 2000;

            var isBusyMode = settings.GetValueOrDefault("IsBusyMode", "false") == "true";
            var busyMinsStr = settings.GetValueOrDefault("BusyEstimatedMinutes", "45");
            if (!int.TryParse(busyMinsStr, out var busyMins)) busyMins = 45;

            var startOfDayUtc = targetDate.AddHours(-8);
            var endOfDayUtc = startOfDayUtc.AddDays(1);

            var orders = await _context.Orders
                .Where(o => o.OrderStatus != "Cancelled" &&
                       ((o.CreatedAt >= targetDate && o.CreatedAt < targetDate.AddDays(1)) ||
                        (o.CreatedAt >= startOfDayUtc && o.CreatedAt < endOfDayUtc)))
                .ToListAsync();

            var standardSlots = new (int h, int m)[]
            {
                (16, 30), (17, 0), (17, 30), (18, 0), (18, 30),
                (19, 0), (19, 30), (20, 0), (20, 30), (21, 0),
                (21, 30), (22, 0)
            };

            var slotAmounts = new Dictionary<string, decimal>();
            foreach (var s in standardSlots)
            {
                slotAmounts[$"{s.h:D2}:{s.m:D2}"] = 0;
            }

            foreach (var order in orders)
            {
                string? slotKey = null;
                var pickupStr = (order.PickupTime ?? "").Trim();
                if (string.IsNullOrWhiteSpace(pickupStr) || pickupStr.Contains("儘速") || pickupStr.Contains("尽速") || pickupStr.Contains("現場"))
                {
                    var tw = order.CreatedAt.Kind == DateTimeKind.Utc ? order.CreatedAt.AddHours(8) : order.CreatedAt;
                    int m = tw.Minute < 30 ? 0 : 30;
                    slotKey = $"{tw.Hour:D2}:{m:D2}";
                }
                else
                {
                    var match = System.Text.RegularExpressions.Regex.Match(pickupStr, @"(\d{1,2})[:：](\d{2})");
                    if (!match.Success)
                    {
                        match = System.Text.RegularExpressions.Regex.Match(pickupStr, @"(\d{2})(\d{2})");
                    }
                    if (match.Success)
                    {
                        int h = int.Parse(match.Groups[1].Value);
                        int m = int.Parse(match.Groups[2].Value);
                        int slotM = m < 30 ? 0 : 30;
                        slotKey = $"{h:D2}:{slotM:D2}";
                    }
                }

                if (slotKey != null && slotAmounts.ContainsKey(slotKey))
                {
                    slotAmounts[slotKey] += order.TotalAmount;
                }
            }

            var isToday = targetDate == now.Date;
            var currentTwMins = now.Hour * 60 + now.Minute;

            var result = standardSlots.Select(s =>
            {
                var timeKey = $"{s.h:D2}:{s.m:D2}";
                var endH = s.m == 30 ? s.h + 1 : s.h;
                var endM = s.m == 30 ? 0 : 30;
                var label = $"{s.h:D2}:{s.m:D2} - {endH:D2}:{endM:D2}";

                var curAmt = slotAmounts.GetValueOrDefault(timeKey, 0);
                var isFull = curAmt >= limit;
                var slotEndMins = endH * 60 + endM;
                var isPast = isToday && slotEndMins <= currentTwMins;

                return new TimeSlotDto
                {
                    Time = timeKey,
                    Label = label,
                    IsFull = isFull,
                    IsPast = isPast
                };
            }).ToList();

            var manualOpen = !settings.TryGetValue("IsOpen", out var isOpenVal) || isOpenVal != "false";
            var isLiveOpen = manualOpen && TaiwanTimeHelper.IsLiveBusinessHours;

            return Ok(new
            {
                date = targetDate.ToString("yyyy-MM-dd"),
                isLiveOpen,
                isBusyMode,
                busyEstimatedMinutes = busyMins,
                slots = result
            });
        }
    }
}
