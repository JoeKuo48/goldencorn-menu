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

            // Generate Order Number: GC-yyyyMMdd-001 using Taiwan Timezone (UTC+8)
            var today = TaiwanTimeHelper.Today;
            var tomorrow = today.AddDays(1);
            var todayOrdersCount = await _context.Orders
                .CountAsync(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);
            
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
                CreatedAt = TaiwanTimeHelper.Now
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

                if (!menuItem.IsAvailable)
                {
                    return BadRequest(new { message = $"餐點「{menuItem.Name}」今日已售完" });
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
    }
}
