namespace GoldenCornOrder.Models
{
    public class OptionGroup
    {
        public int Id { get; set; }
        public int? MenuItemId { get; set; }
        public MenuItem? MenuItem { get; set; }

        // If MenuItemId is null, it can be a global group (e.g. Plate Sides for all BBQ plates)
        public bool IsGlobal { get; set; } = false;

        public string Name { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int MinSelect { get; set; } = 0;
        public int MaxSelect { get; set; } = 1;
        public bool IsRequired { get; set; } = false;
        public int DisplayOrder { get; set; }

        public List<OptionItem> Options { get; set; } = new();
    }
}
