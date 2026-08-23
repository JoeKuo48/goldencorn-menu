using System.Collections.Generic;

namespace GoldenCornOrder.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public string MenuItemEnglishName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal OptionsTotal { get; set; } = 0;
        public decimal SubTotal { get; set; }
        public string? SelectedOptionsSummary { get; set; }
        public string? ItemNote { get; set; }

        public List<OrderItemOption> Options { get; set; } = new();
    }
}
