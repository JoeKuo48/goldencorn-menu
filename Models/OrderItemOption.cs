namespace GoldenCornOrder.Models
{
    public class OrderItemOption
    {
        public int Id { get; set; }
        public int OrderItemId { get; set; }
        public OrderItem? OrderItem { get; set; }

        public int OptionGroupId { get; set; }
        public string OptionGroupName { get; set; } = string.Empty;
        public int OptionItemId { get; set; }
        public string OptionName { get; set; } = string.Empty;
        public string OptionEnglishName { get; set; } = string.Empty;
        public decimal ExtraPrice { get; set; } = 0;
    }
}
