using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;

namespace GoldenCornOrder.Controllers
{
    [ApiController]
    [Route("api/order")]
    public class OrderApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrderApiController(AppDbContext context)
        {
            _context = context;
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
                return BadRequest(new { message = "本店今日已打烊，暫停線上點餐服務，敬請見諒！" });
            }

            // Generate Order Number: GC-yyyyMMdd-001
            var today = DateTime.Today;
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
                PickupTime = string.IsNullOrWhiteSpace(dto.PickupTime) ? "儘速製作" : dto.PickupTime.Trim(),
                CustomerNote = dto.CustomerNote?.Trim(),
                PaymentMethod = dto.PaymentMethod,
                PaymentStatus = dto.PaymentMethod == "Cash" ? "Unpaid" : "Paid",
                TransferLast5 = dto.TransferLast5?.Trim(),
                OrderStatus = "Pending",
                CreatedAt = DateTime.Now
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
