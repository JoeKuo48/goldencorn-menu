namespace GoldenCornOrder.Models
{
    public class MenuItem
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        public string Name { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string? Badge { get; set; } // e.g. "招牌", "人氣", "限量"
        public bool IsAvailable { get; set; } = true;
        public int DisplayOrder { get; set; }

        // Whether this item is an American BBQ Plate (美式餐盤) requiring plate sides
        public bool RequiresPlateSides { get; set; } = false;

        public List<OptionGroup> OptionGroups { get; set; } = new();
    }
}
