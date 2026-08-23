using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;

namespace GoldenCornOrder.Controllers
{
    [ApiController]
    [Route("api/menu")]
    public class MenuApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MenuApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/menu
        [HttpGet]
        public async Task<IActionResult> GetMenu()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Include(c => c.MenuItems)
                    .ThenInclude(m => m.OptionGroups)
                        .ThenInclude(g => g.Options)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.EnglishName,
                    c.Description,
                    c.DisplayOrder,
                    MenuItems = c.MenuItems
                        .OrderBy(m => m.DisplayOrder)
                        .Select(m => new
                        {
                            m.Id,
                            m.CategoryId,
                            m.Name,
                            m.EnglishName,
                            m.Description,
                            m.Price,
                            m.ImageUrl,
                            m.Badge,
                            m.IsAvailable,
                            m.RequiresPlateSides,
                            m.DisplayOrder,
                            OptionGroups = m.OptionGroups
                                .OrderBy(g => g.DisplayOrder)
                                .Select(g => new
                                {
                                    g.Id,
                                    g.Name,
                                    g.EnglishName,
                                    g.Description,
                                    g.IsRequired,
                                    g.MinSelect,
                                    g.MaxSelect,
                                    g.DisplayOrder,
                                    Options = g.Options
                                        .OrderBy(o => o.DisplayOrder)
                                        .Select(o => new
                                        {
                                            o.Id,
                                            o.Name,
                                            o.EnglishName,
                                            o.ExtraPrice,
                                            o.IsAvailable,
                                            o.Tag,
                                            o.DisplayOrder
                                        })
                                })
                        })
                })
                .ToListAsync();

            return Ok(categories);
        }

        // GET: api/menu/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .Select(c => new { c.Id, c.Name, c.EnglishName, c.Description })
                .ToListAsync();

            return Ok(categories);
        }

        // GET: api/menu/item/{id}
        [HttpGet("item/{id}")]
        public async Task<IActionResult> GetMenuItem(int id)
        {
            var item = await _context.MenuItems
                .Include(m => m.OptionGroups)
                    .ThenInclude(g => g.Options)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (item == null) return NotFound(new { message = "找不到該餐點" });

            return Ok(item);
        }
    }
}
