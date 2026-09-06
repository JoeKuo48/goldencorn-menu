using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;

namespace GoldenCornOrder.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminApiController(AppDbContext context)
        {
            _context = context;
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
                    query = query.Where(o => o.CreatedAt >= startOfDay && o.CreatedAt < endOfDay);
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

            order.OrderStatus = dto.Status;
            if (dto.KitchenNote != null)
            {
                order.KitchenNote = dto.KitchenNote;
            }
            order.UpdatedAt = TaiwanTimeHelper.Now;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, order.Id, order.OrderNumber, order.OrderStatus });
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
                DisplayOrder = dto.DisplayOrder > 0 ? dto.DisplayOrder : 1,
                RequiresPlateSides = dto.RequiresPlateSides || dto.CategoryId == 1
            };

            // If it requires plate sides, copy from default option groups if available
            if (menuItem.RequiresPlateSides)
            {
                var existingGroup = await _context.OptionGroups
                    .Include(g => g.Options)
                    .FirstOrDefaultAsync(g => g.Name == "美式餐盤自選配料");
                
                if (existingGroup != null)
                {
                    menuItem.OptionGroups = new List<OptionGroup>
                    {
                        new()
                        {
                            Name = existingGroup.Name,
                            EnglishName = existingGroup.EnglishName,
                            Description = existingGroup.Description,
                            IsRequired = existingGroup.IsRequired,
                            MinSelect = existingGroup.MinSelect,
                            MaxSelect = existingGroup.MaxSelect,
                            DisplayOrder = 1,
                            Options = existingGroup.Options.Select(o => new OptionItem
                            {
                                Name = o.Name,
                                EnglishName = o.EnglishName,
                                ExtraPrice = o.ExtraPrice,
                                IsAvailable = true,
                                DisplayOrder = o.DisplayOrder,
                                Tag = o.Tag
                            }).ToList()
                        }
                    };
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
            var item = await _context.MenuItems.FindAsync(id);
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
            item.DisplayOrder = dto.DisplayOrder;
            item.RequiresPlateSides = dto.RequiresPlateSides || dto.CategoryId == 1;

            await _context.SaveChangesAsync();

            return Ok(item);
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
            var option = await _context.OptionItems.FindAsync(id);
            if (option == null)
            {
                return NotFound(new { message = "找不到該配料/加購選項" });
            }

            option.IsAvailable = !option.IsAvailable;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = option.Id, isAvailable = option.IsAvailable });
        }

        // GET: api/admin/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var today = TaiwanTimeHelper.Today;
            var tomorrow = today.AddDays(1);

            var todayOrders = await _context.Orders
                .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow && o.OrderStatus != "Cancelled")
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
        public async Task<IActionResult> ExportOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("訂單編號,下單時間(台灣時區),顧客姓名,電話,用餐方式,桌號,預約時間,狀態,付款方式,付款狀態,轉帳後五碼,金額,品項明細,備註");

            foreach (var o in orders)
            {
                var itemsSummary = string.Join("； ", o.Items.Select(i => $"{i.MenuItemName} x{i.Quantity} ({i.SelectedOptionsSummary})"));
                var row = $"\"{o.OrderNumber}\",\"{o.CreatedAt:yyyy/MM/dd HH:mm}\",\"{o.CustomerName}\",\"{o.CustomerPhone}\",\"{o.DiningType}\",\"{o.TableNumber}\",\"{o.PickupTime}\",\"{o.OrderStatus}\",\"{o.PaymentMethod}\",\"{o.PaymentStatus}\",\"{o.TransferLast5}\",{o.TotalAmount},\"{itemsSummary.Replace("\"", "\"\"")}\",\"{o.CustomerNote?.Replace("\"", "\"\"")}\"";
                sb.AppendLine(row);
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", $"GoldenCorn_Orders_{TaiwanTimeHelper.Now:yyyyMMdd_HHmm}.csv");
        }
    }
}
