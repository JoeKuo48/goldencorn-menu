using System;
using System.Collections.Generic;

namespace GoldenCornOrder.Models
{
    public class CreateOrderDto
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DiningType { get; set; } = "Takeout";
        public string? TableNumber { get; set; }
        public string? PickupTime { get; set; }
        public string? CustomerNote { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Cash, LinePay, BankTransfer
        public string? TransferLast5 { get; set; } // 轉帳後五碼
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }

    public class CreateOrderItemDto
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; } = 1;
        public string? ItemNote { get; set; }
        public List<int> SelectedOptionItemIds { get; set; } = new();
    }

    public class OrderResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = "Pending";
        public string PaymentMethod { get; set; } = "Cash";
        public string PaymentStatus { get; set; } = "Unpaid";
        public string? TransferLast5 { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class OrderDetailDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string DiningType { get; set; } = "Takeout";
        public string? TableNumber { get; set; }
        public string? PickupTime { get; set; }
        public string? CustomerNote { get; set; }
        public string? KitchenNote { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string PaymentStatus { get; set; } = "Unpaid";
        public string? TransferLast5 { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public List<OrderItemDetailDto> Items { get; set; } = new();
    }

    public class OrderItemDetailDto
    {
        public int Id { get; set; }
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal SubTotal { get; set; }
        public string? ItemNote { get; set; }
        public string SelectedOptionsSummary { get; set; } = string.Empty;
        public List<OrderItemOptionDetailDto> Options { get; set; } = new();
    }

    public class OrderItemOptionDetailDto
    {
        public int OptionItemId { get; set; }
        public string OptionName { get; set; } = string.Empty;
        public decimal ExtraPrice { get; set; }
        public string OptionGroupName { get; set; } = string.Empty;
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } = string.Empty; // Pending, Preparing, Ready, Completed, Cancelled
        public string? KitchenNote { get; set; }
    }

    public class MenuItemEditDto
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string EnglishName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string? Badge { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int DisplayOrder { get; set; }
        public bool RequiresPlateSides { get; set; }
    }

    public class DashboardStatsDto
    {
        public decimal TodayRevenue { get; set; }
        public int TodayOrdersCount { get; set; }
        public decimal AverageOrderAmount { get; set; }
        public int PendingOrdersCount { get; set; }
        public int PreparingOrdersCount { get; set; }
        public int ReadyOrdersCount { get; set; }
        public List<TopItemDto> TopSellingItems { get; set; } = new();
    }

    public class TopItemDto
    {
        public string ItemName { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public decimal TotalSales { get; set; }
    }
}
