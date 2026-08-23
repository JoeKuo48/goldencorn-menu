using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GoldenCornOrder.Data;
using GoldenCornOrder.Models;

namespace GoldenCornOrder.Controllers
{
    [ApiController]
    [Route("api/settings")]
    public class SettingsApiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SettingsApiController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/settings
        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _context.StoreSettings.ToListAsync();
            var dict = settings.ToDictionary(s => s.Key, s => s.Value);
            return Ok(dict);
        }

        // POST: api/settings
        [HttpPost]
        public async Task<IActionResult> UpdateSettings([FromBody] Dictionary<string, string> updates)
        {
            if (updates == null || !updates.Any())
            {
                return BadRequest(new { message = "沒有提供更新設定" });
            }

            foreach (var kvp in updates)
            {
                var setting = await _context.StoreSettings.FirstOrDefaultAsync(s => s.Key == kvp.Key);
                if (setting != null)
                {
                    setting.Value = kvp.Value;
                    setting.UpdatedAt = DateTime.Now;
                }
                else
                {
                    _context.StoreSettings.Add(new StoreSetting
                    {
                        Key = kvp.Key,
                        Value = kvp.Value,
                        UpdatedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "設定儲存成功" });
        }
    }
}
