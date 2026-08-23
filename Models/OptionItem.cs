namespace GoldenCornOrder.Models
{
    public class OptionItem
    {
        public int Id { get; set; }
        public int OptionGroupId { get; set; }
        public OptionGroup? OptionGroup { get; set; }

        public string Name { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;
        public decimal ExtraPrice { get; set; } = 0;
        public bool IsAvailable { get; set; } = true;
        public string? Tag { get; set; } // e.g. "限量加購", "售完為止"
        public int DisplayOrder { get; set; }
    }
}
