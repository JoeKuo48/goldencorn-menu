using System;
using System.Collections.Generic;

namespace GoldenCornOrder.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        
        public string DiningType { get; set; } = "Takeout"; // Takeout, DineIn
        public string? TableNumber { get; set; }
        
        public string? PickupTime { get; set; }
        public string? CustomerNote { get; set; }
        public string? KitchenNote { get; set; }
        
        public string PaymentMethod { get; set; } = "Cash"; // Cash, LinePay, BankTransfer
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Paid, PendingVerification
        public string? TransferLast5 { get; set; } // 轉帳後五碼
        
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = "Pending"; // Pending, Preparing, Ready, Completed, Cancelled
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        
        public List<OrderItem> Items { get; set; } = new();
    }
}
