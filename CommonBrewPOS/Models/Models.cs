using System.Text.Json.Serialization;

namespace CommonBrewPOS.Models;

// ── User ─────────────────────────────────────────────────────
public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;   // "Administrator" | "Cashier"

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

// ── Category ──────────────────────────────────────────────────
public class Category
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// ── Inventory Item (Ingredient) ───────────────────────────────
public class InventoryItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("current_stock")]
    public decimal CurrentStock { get; set; }

    [JsonPropertyName("critical_level")]
    public decimal CriticalLevel { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Computed
    public bool IsLowStock => CurrentStock <= CriticalLevel;
    public string StatusLabel => IsLowStock ? "Low Stock" : "Good";
    public decimal StockPercent => CriticalLevel > 0
        ? Math.Min((CurrentStock / CriticalLevel) * 100m, 100m) : 100m;
}

// ── Product ───────────────────────────────────────────────────
public class Product
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category_id")]
    public string? CategoryId { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("is_available")]
    public bool IsAvailable { get; set; } = true;

    [JsonPropertyName("product_type")]
    public string ProductType { get; set; } = "Drink";

    [JsonPropertyName("upsize_price")]
    public decimal? UpsizePrice { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation (populated in service layer)
    public string CategoryName { get; set; } = string.Empty;
    public List<ProductRecipe> Recipes { get; set; } = new();
    public List<ProductRecipe> UpsizeRecipes { get; set; } = new();
}

// ── Product Recipe ────────────────────────────────────────────
public class ProductRecipe
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("inventory_item_id")]
    public string InventoryItemId { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    // Navigation
    public string InventoryItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
}

// ── Transaction ───────────────────────────────────────────────
public class Transaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("transaction_ref")]
    public string TransactionRef { get; set; } = string.Empty;

    [JsonPropertyName("cashier_id")]
    public string? CashierId { get; set; }

    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = "Cash";

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("total_amount")]
    public decimal TotalAmount { get; set; }

    [JsonPropertyName("cash_tendered")]
    public decimal? CashTendered { get; set; }

    [JsonPropertyName("change_amount")]
    public decimal? ChangeAmount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation
    public List<TransactionItem> Items { get; set; } = new();
    public string CashierName { get; set; } = string.Empty;
}

// ── Transaction Item ──────────────────────────────────────────
public class TransactionItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("product_id")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("product_name")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }
}

// ── Inventory Log ─────────────────────────────────────────────
public class InventoryLog
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("inventory_item_id")]
    public string InventoryItemId { get; set; } = string.Empty;

    [JsonPropertyName("change_type")]
    public string ChangeType { get; set; } = string.Empty;

    [JsonPropertyName("quantity_change")]
    public decimal QuantityChange { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("transaction_id")]
    public string? TransactionId { get; set; }

    [JsonPropertyName("performed_by")]
    public string? PerformedBy { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

// ── Analytics DTOs ────────────────────────────────────────────
public class DailyAnalytics
{
    public decimal TodayRevenue { get; set; }
    public int TodayTransactions { get; set; }
    public string TopSellingProduct { get; set; } = string.Empty;
    public int LowStockCount { get; set; }
    public List<WeeklySalesPoint> WeeklySales { get; set; } = new();
    public List<InventoryItem> LowStockItems { get; set; } = new();
    public List<ProductSalesSummary> TopProducts { get; set; } = new();
}

public class WeeklySalesPoint
{
    public string DayLabel { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
}

public class ProductSalesSummary
{
    public string ProductName { get; set; } = string.Empty;
    public int TotalSold { get; set; }
    public decimal TotalRevenue { get; set; }
}
