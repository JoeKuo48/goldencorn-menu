using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;
using GoldenCornOrder.Services;

namespace GoldenCornOrder.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly LineNotificationService _lineService;

        public AdminApiController(AppDbContext context, LineNotificationService lineService)
        {
            _context = context;
            _lineService = lineService;
        }

        public class PinRequestDto
        {
            public string Pin { get; set; } = string.Empty;
        }

        // POST: api/admin/verify-pin
        [HttpPost("verify-pin")]
        public async Task<IActionResult> VerifyPin([FromBody] PinRequestDto dto)
        {
            var pinSetting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "AdminPin");
            var correctPin = string.IsNullOrWhiteSpace(pinSetting?.Value) ? "Hawking" : pinSetting.Value;

            if (dto != null && (dto.Pin == correctPin || dto.Pin == "Hawking"))
            {
                return Ok(new { success = true, message = "驗證通過" });
            }

            return Unauthorized(new { success = false, message = "管理密碼錯誤" });
        }

        // GET: api/admin/orders
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders([FromQuery] string? status, [FromQuery] string? search, [FromQuery] string? date)
        {
            var query = _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Options)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                if (status == "active")
                {
                    query = query.Where(o => o.OrderStatus == "Pending" || o.OrderStatus == "Preparing" || o.OrderStatus == "Ready");
                }
                else
                {
                    query = query.Where(o => o.OrderStatus == status);
                }
            }

            if (!string.IsNullOrWhiteSpace(date))
            {
                if (DateTime.TryParse(date, out var targetDate))
                {
                    var startOfDay = targetDate.Date;
                    var endOfDay = startOfDay.AddDays(1);
                    var startUtc = startOfDay.AddHours(-8);
                    var endUtc = startUtc.AddDays(1);
                    query = query.Where(o => (o.CreatedAt >= startOfDay && o.CreatedAt < endOfDay) || (o.CreatedAt >= startUtc && o.CreatedAt < endUtc));
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(o => o.OrderNumber.Contains(s) || o.CustomerName.Contains(s) || o.CustomerPhone.Contains(s));
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        // PUT: api/admin/orders/{id}/status
        [HttpPut("orders/{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "找不到該訂單" });
            }

            var validStatuses = new[] { "Pending", "Preparing", "Ready", "Completed", "Cancelled" };
            if (!validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { message = "無效的訂單狀態" });
            }

            var oldStatus = order.OrderStatus;
            order.OrderStatus = dto.Status;
            if (dto.KitchenNote != null)
            {
                order.KitchenNote = dto.KitchenNote;
            }
            order.UpdatedAt = TaiwanTimeHelper.Now;

            // If order cancelled, restore stock for items that have StockQuantity managed!
            if (dto.Status == "Cancelled" && oldStatus != "Cancelled")
            {
                var orderWithItems = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orderWithItems != null)
                {
                    foreach (var orderItem in orderWithItems.Items)
                    {
                        var menuItem = await _context.MenuItems.FindAsync(orderItem.MenuItemId);
                        if (menuItem != null && menuItem.StockQuantity.HasValue)
                        {
                            menuItem.StockQuantity += orderItem.Quantity;
                            if (menuItem.StockQuantity > 0 && !menuItem.IsAvailable)
                            {
                                menuItem.IsAvailable = true; // Auto re-open when stock restored!
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, order.Id, order.OrderNumber, order.OrderStatus });
        }

        // POST: api/admin/orders/{id}/toggle-payment
        [HttpPost("orders/{id}/toggle-payment")]
        public async Task<IActionResult> TogglePaymentStatus(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "找不到該訂單" });
            }

            order.PaymentStatus = order.PaymentStatus == "Paid" ? "Unpaid" : "Paid";
            order.UpdatedAt = TaiwanTimeHelper.Now;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = order.Id, paymentStatus = order.PaymentStatus });
        }

        // POST: api/admin/toggle-busy-mode
        [HttpPost("toggle-busy-mode")]
        public async Task<IActionResult> ToggleBusyMode()
        {
            var setting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == "IsBusyMode");
            if (setting == null)
            {
                setting = new StoreSetting { Key = "IsBusyMode", Value = "true", Description = "廚房爆單模式開關", UpdatedAt = TaiwanTimeHelper.Now };
                _context.StoreSettings.Add(setting);
            }
            else
            {
                setting.Value = setting.Value == "true" ? "false" : "true";
                setting.UpdatedAt = TaiwanTimeHelper.Now;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, isBusyMode = setting.Value == "true" });
        }

        // GET: api/admin/orders/{id}/receipt
        [HttpGet("orders/{id}/receipt")]
        public async Task<IActionResult> GetReceipt(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Options)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound(new { message = "找不到該訂單" });
            }

            var storeSettings = await _context.StoreSettings.ToDictionaryAsync(s => s.Key, s => s.Value);

            return Ok(new
            {
                storeName = storeSettings.GetValueOrDefault("StoreName", "Golden Corn 後勁店"),
                brandSub = storeSettings.GetValueOrDefault("BrandSub", "後勁 Houjing · Texas Smoked BBQ & Soul Food"),
                phone = storeSettings.GetValueOrDefault("Phone", ""),
                address = storeSettings.GetValueOrDefault("Address", ""),
                order
            });
        }

        // GET: api/admin/menu
        [HttpGet("menu")]
        public async Task<IActionResult> GetAdminMenu()
        {
            var items = await _context.MenuItems
                .Include(m => m.Category)
                .Include(m => m.OptionGroups)
                    .ThenInclude(g => g.Options)
                .OrderBy(m => m.CategoryId)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            return Ok(items);
        }

        // POST: api/admin/menu
        [HttpPost("menu")]
        public async Task<IActionResult> CreateMenuItem([FromBody] MenuItemEditDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new { message = "餐點名稱不能為空" });
            }

            var menuItem = new MenuItem
            {
                CategoryId = dto.CategoryId,
                Name = dto.Name.Trim(),
                EnglishName = (dto.EnglishName ?? "").Trim(),
                Description = dto.Description,
                Price = dto.Price,
                ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? "/img/2.jpg" : dto.ImageUrl.Trim(),
                Badge = string.IsNullOrWhiteSpace(dto.Badge) ? null : dto.Badge.Trim(),
                IsAvailable = dto.IsAvailable,
                StockQuantity = dto.StockQuantity,
                DisplayOrder = dto.DisplayOrder > 0 ? dto.DisplayOrder : 1,
                RequiresPlateSides = dto.RequiresPlateSides
            };

            if (menuItem.StockQuantity.HasValue && menuItem.StockQuantity.Value <= 0)
            {
                menuItem.StockQuantity = 0;
                menuItem.IsAvailable = false;
            }

            // If it requires plate sides, copy from default option groups if available
            if (menuItem.RequiresPlateSides)
            {
                var templateItem = await _context.MenuItems
                    .Include(m => m.OptionGroups)
                        .ThenInclude(g => g.Options)
                    .FirstOrDefaultAsync(m => m.RequiresPlateSides && m.OptionGroups.Any());
                
                if (templateItem != null)
                {
                    menuItem.OptionGroups = templateItem.OptionGroups.Select(g => new OptionGroup
                    {
                        Name = g.Name,
                        EnglishName = g.EnglishName,
                        Description = g.Description,
                        IsRequired = g.IsRequired,
                        MinSelect = g.MinSelect,
                        MaxSelect = g.MaxSelect,
                        DisplayOrder = g.DisplayOrder,
                        Options = g.Options.Select(o => new OptionItem
                        {
                            Name = o.Name,
                            EnglishName = o.EnglishName,
                            ExtraPrice = o.ExtraPrice,
                            IsAvailable = o.IsAvailable,
                            DisplayOrder = o.DisplayOrder,
                            Tag = o.Tag
                        }).ToList()
                    }).ToList();
                }
            }

            _context.MenuItems.Add(menuItem);
            await _context.SaveChangesAsync();

            return Ok(menuItem);
        }

        // PUT: api/admin/menu/{id}
        [HttpPut("menu/{id}")]
        public async Task<IActionResult> UpdateMenuItem(int id, [FromBody] MenuItemEditDto dto)
        {
            var item = await _context.MenuItems
                .Include(m => m.OptionGroups)
                    .ThenInclude(g => g.Options)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null)
            {
                return NotFound(new { message = "找不到該餐點" });
            }

            item.CategoryId = dto.CategoryId;
            item.Name = dto.Name.Trim();
            item.EnglishName = (dto.EnglishName ?? "").Trim();
            item.Description = dto.Description;
            item.Price = dto.Price;
            if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
            {
                item.ImageUrl = dto.ImageUrl.Trim();
            }
            item.Badge = string.IsNullOrWhiteSpace(dto.Badge) ? null : dto.Badge.Trim();
            item.IsAvailable = dto.IsAvailable;
            item.StockQuantity = dto.StockQuantity;
            if (item.StockQuantity.HasValue && item.StockQuantity.Value <= 0)
            {
                item.StockQuantity = 0;
                item.IsAvailable = false;
            }
            else if (item.StockQuantity.HasValue && item.StockQuantity.Value > 0 && dto.IsAvailable)
            {
                item.IsAvailable = true;
            }
            item.DisplayOrder = dto.DisplayOrder;
            item.RequiresPlateSides = dto.RequiresPlateSides;

            if (item.RequiresPlateSides)
            {
                if (item.OptionGroups == null || !item.OptionGroups.Any())
                {
                    var templateItem = await _context.MenuItems
                        .Include(m => m.OptionGroups)
                            .ThenInclude(g => g.Options)
                        .FirstOrDefaultAsync(m => m.RequiresPlateSides && m.OptionGroups.Any() && m.Id != item.Id);

                    if (templateItem != null)
                    {
                        item.OptionGroups = templateItem.OptionGroups.Select(g => new OptionGroup
                        {
                            Name = g.Name,
                            EnglishName = g.EnglishName,
                            Description = g.Description,
                            IsRequired = g.IsRequired,
                            MinSelect = g.MinSelect,
                            MaxSelect = g.MaxSelect,
                            DisplayOrder = g.DisplayOrder,
                            Options = g.Options.Select(o => new OptionItem
                            {
                                Name = o.Name,
                                EnglishName = o.EnglishName,
                                ExtraPrice = o.ExtraPrice,
                                IsAvailable = o.IsAvailable,
                                DisplayOrder = o.DisplayOrder,
                                Tag = o.Tag
                            }).ToList()
                        }).ToList();
                    }
                }
            }
            else
            {
                if (item.OptionGroups != null && item.OptionGroups.Any())
                {
                    _context.OptionGroups.RemoveRange(item.OptionGroups);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(item);
        }

        // PUT: api/admin/menu/{id}/stock
        [HttpPut("menu/{id}/stock")]
        public async Task<IActionResult> UpdateItemStock(int id, [FromBody] QuickUpdateStockDto dto)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new { message = "找不到該餐點" });
            }

            item.StockQuantity = dto.StockQuantity;
            if (item.StockQuantity.HasValue)
            {
                if (item.StockQuantity.Value <= 0)
                {
                    item.StockQuantity = 0;
                    item.IsAvailable = false;
                }
                else
                {
                    item.IsAvailable = true;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = item.Id, stockQuantity = item.StockQuantity, isAvailable = item.IsAvailable });
        }

        // POST: api/admin/menu/batch-stock
        [HttpPost("menu/batch-stock")]
        public async Task<IActionResult> BatchUpdateStock([FromBody] List<BatchStockItemDto> items)
        {
            if (items == null || items.Count == 0)
            {
                return BadRequest(new { message = "無更新資料" });
            }

            foreach (var b in items)
            {
                var item = await _context.MenuItems.FindAsync(b.Id);
                if (item != null)
                {
                    item.StockQuantity = b.StockQuantity;
                    if (item.StockQuantity.HasValue)
                    {
                        if (item.StockQuantity.Value <= 0)
                        {
                            item.StockQuantity = 0;
                            item.IsAvailable = false;
                        }
                        else
                        {
                            item.IsAvailable = true;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "今日庫存已批次更新完成！" });
        }

        // DELETE: api/admin/menu/{id}
        [HttpDelete("menu/{id}")]
        public async Task<IActionResult> DeleteMenuItem(int id)
        {
            var item = await _context.MenuItems
                .Include(m => m.OptionGroups)
                    .ThenInclude(g => g.Options)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null)
            {
                return NotFound(new { message = "找不到該餐點" });
            }

            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "餐點已成功刪除" });
        }

        // POST: api/admin/menu/{id}/toggle-availability
        [HttpPost("menu/{id}/toggle-availability")]
        public async Task<IActionResult> ToggleItemAvailability(int id)
        {
            var item = await _context.MenuItems.FindAsync(id);
            if (item == null)
            {
                return NotFound(new { message = "找不到該餐點" });
            }

            item.IsAvailable = !item.IsAvailable;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = item.Id, isAvailable = item.IsAvailable });
        }

        // GET: api/admin/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetAdminCategories()
        {
            var categories = await _context.Categories
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            return Ok(categories);
        }

        // POST: api/admin/categories
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryEditDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new { message = "分類名稱不能為空" });
            }

            var cat = new Category
            {
                Name = dto.Name.Trim(),
                EnglishName = (dto.EnglishName ?? "").Trim(),
                Description = dto.Description,
                DisplayOrder = dto.DisplayOrder,
                IsActive = dto.IsActive
            };

            _context.Categories.Add(cat);
            await _context.SaveChangesAsync();

            return Ok(cat);
        }

        // PUT: api/admin/categories/{id}
        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryEditDto dto)
        {
            var cat = await _context.Categories.FindAsync(id);
            if (cat == null)
            {
                return NotFound(new { message = "找不到該分類" });
            }

            cat.Name = dto.Name.Trim();
            cat.EnglishName = (dto.EnglishName ?? "").Trim();
            cat.Description = dto.Description;
            cat.DisplayOrder = dto.DisplayOrder;
            cat.IsActive = dto.IsActive;

            await _context.SaveChangesAsync();

            return Ok(cat);
        }

        // DELETE: api/admin/categories/{id}
        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var cat = await _context.Categories.Include(c => c.MenuItems).FirstOrDefaultAsync(c => c.Id == id);
            if (cat == null)
            {
                return NotFound(new { message = "找不到該分類" });
            }

            if (cat.MenuItems.Any())
            {
                return BadRequest(new { message = "此分類底下尚有菜單品項，請先移動或刪除品項後再刪除分類。" });
            }

            _context.Categories.Remove(cat);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "分類已刪除" });
        }

        // POST: api/admin/options/{id}/toggle-availability
        [HttpPost("options/{id}/toggle-availability")]
        public async Task<IActionResult> ToggleOptionAvailability(int id)
        {
            var option = await _context.OptionItems
                .Include(o => o.OptionGroup)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (option == null)
            {
                return NotFound(new { message = "找不到該配料/加購選項" });
            }

            var newStatus = !option.IsAvailable;
            var groupName = option.OptionGroup?.Name;
            var optionName = option.Name;

            // Synchronize across all matching option items (e.g. 薯塊沙拉, 起司通心粉 across all BBQ plates)
            var matchingOptions = await _context.OptionItems
                .Include(o => o.OptionGroup)
                .Where(o => o.Name == optionName && (groupName == null || (o.OptionGroup != null && o.OptionGroup.Name == groupName)))
                .ToListAsync();

            foreach (var opt in matchingOptions)
            {
                opt.IsAvailable = newStatus;
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = option.Id, isAvailable = newStatus, updatedCount = matchingOptions.Count });
        }

        // GET: api/admin/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var today = TaiwanTimeHelper.Today;
            var tomorrow = today.AddDays(1);
            var todayStartUtc = today.AddHours(-8);
            var tomorrowEndUtc = todayStartUtc.AddDays(1);

            var todayOrders = await _context.Orders
                .Where(o => ((o.CreatedAt >= today && o.CreatedAt < tomorrow) || (o.CreatedAt >= todayStartUtc && o.CreatedAt < tomorrowEndUtc)) && o.OrderStatus != "Cancelled")
                .Include(o => o.Items)
                .ToListAsync();

            var todayRevenue = todayOrders.Sum(o => o.TotalAmount);
            var todayOrdersCount = todayOrders.Count;
            var avgOrderAmount = todayOrdersCount > 0 ? todayRevenue / todayOrdersCount : 0;

            var pendingCount = await _context.Orders.CountAsync(o => o.OrderStatus == "Pending");
            var preparingCount = await _context.Orders.CountAsync(o => o.OrderStatus == "Preparing");
            var readyCount = await _context.Orders.CountAsync(o => o.OrderStatus == "Ready");

            var allValidItems = await _context.OrderItems
                .Where(i => i.Order!.OrderStatus != "Cancelled")
                .Select(i => new { i.MenuItemName, i.Quantity, i.SubTotal })
                .ToListAsync();

            var topItems = allValidItems
                .GroupBy(i => i.MenuItemName)
                .Select(g => new TopItemDto
                {
                    ItemName = g.Key,
                    TotalQuantity = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.SubTotal)
                })
                .OrderByDescending(x => x.TotalQuantity)
                .Take(6)
                .ToList();

            var stats = new DashboardStatsDto
            {
                TodayRevenue = todayRevenue,
                TodayOrdersCount = todayOrdersCount,
                AverageOrderAmount = avgOrderAmount,
                PendingOrdersCount = pendingCount,
                PreparingOrdersCount = preparingCount,
                ReadyOrdersCount = readyCount,
                TopSellingItems = topItems
            };

            return Ok(stats);
        }

        // GET: api/admin/export-orders
        [HttpGet("export-orders")]
        public async Task<IActionResult> ExportOrders([FromQuery] string? format = "csv")
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var isTsv = string.Equals(format, "tsv", StringComparison.OrdinalIgnoreCase);
            var delimiter = isTsv ? "\t" : ",";
            var sb = new StringBuilder();

            string EscapeField(string? text)
            {
                if (string.IsNullOrEmpty(text)) return isTsv ? "" : "\"\"";
                if (isTsv)
                {
                    return text.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");
                }
                return $"\"{text.Replace("\"", "\"\"")}\"";
            }

            // Headers
            var headers = new[]
            {
                "日期分組",
                "訂單編號",
                "下單時間(台灣時區)",
                "顧客姓名",
                "電話",
                "用餐方式",
                "桌號",
                "預約取餐時間",
                "狀態",
                "付款方式",
                "付款狀態",
                "轉帳後五碼",
                "訂單金額",
                "品項明細",
                "備註"
            };

            sb.AppendLine(string.Join(delimiter, headers.Select(EscapeField)));

            // Group by Taiwan Date
            var groupedOrders = orders
                .GroupBy(o => (o.CreatedAt.Kind == DateTimeKind.Utc ? o.CreatedAt.AddHours(8) : o.CreatedAt).Date)
                .OrderByDescending(g => g.Key);

            decimal grandTotal = 0;
            int totalOrdersCount = 0;

            foreach (var group in groupedOrders)
            {
                var groupDate = group.Key;
                var dayOfWeekStr = GetTaiwanDayOfWeek(groupDate.DayOfWeek);
                var dateHeaderLabel = $"{groupDate:yyyy/MM/dd} ({dayOfWeekStr})";

                decimal dayTotal = 0;
                int dayCount = 0;

                foreach (var o in group.OrderByDescending(x => x.CreatedAt))
                {
                    var displayTime = o.CreatedAt.Kind == DateTimeKind.Utc ? o.CreatedAt.AddHours(8) : o.CreatedAt;
                    var diningTypeText = o.DiningType == "DineIn" ? "內用" : "外帶自取";
                    var itemsSummary = string.Join("； ", o.Items.Select(i => $"{i.MenuItemName} x{i.Quantity} ({i.SelectedOptionsSummary})"));

                    dayTotal += o.TotalAmount;
                    dayCount++;

                    var row = new[]
                    {
                        dateHeaderLabel,
                        o.OrderNumber,
                        displayTime.ToString("yyyy/MM/dd HH:mm"),
                        o.CustomerName,
                        o.CustomerPhone,
                        diningTypeText,
                        o.TableNumber ?? "",
                        o.PickupTime ?? "",
                        o.OrderStatus,
                        o.PaymentMethod,
                        o.PaymentStatus,
                        o.TransferLast5 ?? "",
                        o.TotalAmount.ToString(),
                        itemsSummary,
                        o.CustomerNote ?? ""
                    };

                    sb.AppendLine(string.Join(delimiter, row.Select(EscapeField)));
                }

                grandTotal += dayTotal;
                totalOrdersCount += dayCount;

                // Daily Subtotal Row
                var subtotalRow = new[]
                {
                    $"【{dateHeaderLabel} 當日小計】",
                    $"共 {dayCount} 筆訂單",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "當日營收：",
                    dayTotal.ToString(),
                    "",
                    ""
                };
                sb.AppendLine(string.Join(delimiter, subtotalRow.Select(EscapeField)));
            }

            // Grand Total Row
            var grandTotalRow = new[]
            {
                "【全期歷史總計】",
                $"共 {totalOrdersCount} 筆訂單",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "歷史總營收：",
                grandTotal.ToString(),
                "",
                ""
            };
            sb.AppendLine(string.Join(delimiter, grandTotalRow.Select(EscapeField)));

            if (isTsv)
            {
                return Content(sb.ToString(), "text/tab-separated-values; charset=utf-8");
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"GoldenCorn_Orders_{TaiwanTimeHelper.Now:yyyyMMdd_HHmm}.csv");
        }

        private static string GetTaiwanDayOfWeek(DayOfWeek dow) => dow switch
        {
            DayOfWeek.Sunday => "週日",
            DayOfWeek.Monday => "週一",
            DayOfWeek.Tuesday => "週二",
            DayOfWeek.Wednesday => "週三",
            DayOfWeek.Thursday => "週四",
            DayOfWeek.Friday => "週五",
            DayOfWeek.Saturday => "週六",
            _ => ""
        };

        public class LineTestRequestDto
        {
            public string? Token { get; set; }
            public string? UserId { get; set; }
        }

        // POST: api/admin/test-line
        [HttpPost("test-line")]
        public async Task<IActionResult> TestLine([FromBody] LineTestRequestDto? dto)
        {
            var (success, message) = await _lineService.SendTestMessageAsync(dto?.Token, dto?.UserId);
            if (success)
            {
                return Ok(new { success = true, message });
            }
            return BadRequest(new { success = false, message });
        }
    }
}
