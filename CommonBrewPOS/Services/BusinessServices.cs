using CommonBrewPOS.Models;

namespace CommonBrewPOS.Services;

// ── Product Service ───────────────────────────────────────────
// ── Product Service ───────────────────────────────────────────
public class ProductService
{
    private readonly SupabaseService _db;

    public ProductService(SupabaseService db) => _db = db;

    public async Task<List<Product>> GetAllAsync()
    {
        var products = await _db.SelectAsync<Product>(
            "products",
            "select=*,categories(name)&is_available=eq.true&order=name.asc"
        );

        foreach (var p in products)
        {
            p.Recipes = await GetRecipesAsync(p.Id);
            p.UpsizeRecipes = await GetUpsizeRecipesAsync(p.Id);
        }

        return products;
    }

    public async Task<List<Product>> GetArchivedAsync()
        => await _db.SelectAsync<Product>(
            "products",
            "is_available=eq.false&select=*&order=name.asc"
        );

    public async Task RestoreAsync(string id)
        => await _db.UpdateAsync("products", "id", id, new
        {
            is_available = true
        });

    public async Task<List<Product>> GetByCategoryAsync(string categoryId)
        => await _db.SelectAsync<Product>(
            "products",
            $"category_id=eq.{categoryId}&is_available=eq.true&select=*"
        );

    public async Task<Product?> GetByIdAsync(string id)
    {
        var p = await _db.SelectSingleAsync<Product>(
            "products",
            $"id=eq.{id}&select=*"
        );

        if (p != null)
        {
            p.Recipes = await GetRecipesAsync(p.Id);
            p.UpsizeRecipes = await GetUpsizeRecipesAsync(p.Id);
        }

        return p;
    }

    public async Task<List<ProductRecipe>> GetRecipesAsync(string productId)
        => await _db.SelectAsync<ProductRecipe>(
            "product_recipes",
            $"product_id=eq.{productId}&select=*,inventory_items(name,unit)"
        );

    public async Task<List<ProductRecipe>> GetUpsizeRecipesAsync(string productId)
        => await _db.SelectAsync<ProductRecipe>(
            "product_upsize_recipes",
            $"product_id=eq.{productId}&select=*"
        );

    public async Task<Product?> CreateAsync(Product product, List<ProductRecipe> recipes)
    {
        var created = await _db.InsertAsync<Product>("products", new
        {
            name = product.Name,
            category_id = product.CategoryId,
            price = product.Price,
            image_url = product.ImageUrl,
            is_available = true,
            product_type = product.ProductType,
            upsize_price = product.UpsizePrice
        });

        if (created != null && recipes.Any())
        {
            foreach (var r in recipes)
            {
                await _db.InsertAsync<ProductRecipe>("product_recipes", new
                {
                    product_id = created.Id,
                    inventory_item_id = r.InventoryItemId,
                    quantity = r.Quantity
                });
            }
        }

        return created;
    }

    public async Task UpdateAsync(Product product, List<ProductRecipe> recipes)
    {
        await _db.UpdateAsync("products", "id", product.Id, new
        {
            name = product.Name,
            category_id = product.CategoryId,
            price = product.Price,
            image_url = product.ImageUrl,
            product_type = product.ProductType,
            upsize_price = product.UpsizePrice
        });

        await _db.DeleteAsync("product_recipes", "product_id", product.Id);

        foreach (var r in recipes)
        {
            await _db.InsertAsync<ProductRecipe>("product_recipes", new
            {
                product_id = product.Id,
                inventory_item_id = r.InventoryItemId,
                quantity = r.Quantity
            });
        }
    }

    public async Task DeleteAsync(string id)
        => await _db.UpdateAsync("products", "id", id, new
        {
            is_available = false
        });

    public async Task<List<Category>> GetCategoriesAsync()
        => await _db.SelectAsync<Category>(
            "categories",
            "select=*&order=name.asc"
        );

    public async Task SaveUpsizeRecipesAsync(string productId, List<ProductRecipe> recipes)
    {
        foreach (var r in recipes)
        {
            await _db.InsertAsync<object>("product_upsize_recipes", new
            {
                product_id = productId,
                inventory_item_id = r.InventoryItemId,
                quantity = r.Quantity
            });
        }
    }

    public async Task DeleteUpsizeRecipesAsync(string productId)
    {
        var old = await _db.SelectAsync<ProductRecipe>(
            "product_upsize_recipes",
            $"product_id=eq.{productId}&select=*"
        );

        foreach (var item in old)
        {
            await _db.DeleteAsync("product_upsize_recipes", "id", item.Id);
        }
    }
}

// ── Inventory Service ─────────────────────────────────────────
public class InventoryService
{
    private readonly SupabaseService _db;
    public InventoryService(SupabaseService db) => _db = db;

    public async Task<List<InventoryItem>> GetAllAsync()
        => await _db.SelectAsync<InventoryItem>("inventory_items",
            "select=*&order=name.asc");

public async Task<List<InventoryItem>> GetLowStockAsync()
{
    return await _db.SelectAsync<InventoryItem>(
        "v_low_stock",
        "select=id,name,unit,current_stock,critical_level"
    );
}

    public async Task<InventoryItem?> GetByIdAsync(string id)
        => await _db.SelectSingleAsync<InventoryItem>("inventory_items", $"id=eq.{id}&select=*");

    public async Task AddStockAsync(string itemId, decimal quantity, string? note, string userId)
    {
        var item = await GetByIdAsync(itemId);
        if (item == null) return;
        var newStock = item.CurrentStock + quantity;
        await _db.UpdateAsync("inventory_items", "id", itemId, new { current_stock = newStock });
        await _db.InsertAsync<InventoryLog>("inventory_logs", new
        {
            inventory_item_id = itemId,
            change_type = "stock_in",
            quantity_change = quantity,
            note,
            performed_by = userId
        });
    }
    public decimal? StockPct { get; set; }
    public async Task UpdateItemAsync(InventoryItem item)
        => await _db.UpdateAsync("inventory_items", "id", item.Id, new
        {
            name = item.Name,
            unit = item.Unit,
            current_stock = item.CurrentStock,
            critical_level = item.CriticalLevel
        });

    public async Task CreateItemAsync(InventoryItem item)
        => await _db.InsertAsync<InventoryItem>("inventory_items", new
        {
            name = item.Name,
            unit = item.Unit,
            current_stock = item.CurrentStock,
            critical_level = item.CriticalLevel
        });

    public async Task DeductForTransactionAsync(string transactionId,
        List<TransactionItem> items, string userId)
    {
        foreach (var ti in items)
        {
            var recipes = await _db.SelectAsync<ProductRecipe>("product_recipes",
                $"product_id=eq.{ti.ProductId}&select=*");
            foreach (var r in recipes)
            {
                var deduct = r.Quantity * ti.Quantity;
                var inv = await GetByIdAsync(r.InventoryItemId);
                if (inv == null) continue;
                var newStock = Math.Max(0, inv.CurrentStock - deduct);
                await _db.UpdateAsync("inventory_items", "id", r.InventoryItemId,
                    new { current_stock = newStock });
                await _db.InsertAsync<InventoryLog>("inventory_logs", new
                {
                    inventory_item_id = r.InventoryItemId,
                    change_type = "deduction",
                    quantity_change = -deduct,
                    note = $"Sale: {ti.ProductName} x{ti.Quantity}",
                    transaction_id = transactionId,
                    performed_by = userId
                });
            }
        }
    }
}

// ── Transaction Service ───────────────────────────────────────
public class TransactionService
{
    private readonly SupabaseService _db;
    private readonly InventoryService _inventory;

    public TransactionService(SupabaseService db, InventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<Transaction?> CreateAsync(Transaction transaction, string userId)
    {
        var created = await _db.InsertAsync<Transaction>("transactions", new
        {
            transaction_ref = "", // trigger fills this
            cashier_id = userId,
            payment_method = transaction.PaymentMethod,
            subtotal = transaction.Subtotal,
            total_amount = transaction.TotalAmount,
            cash_tendered = transaction.CashTendered,
            change_amount = transaction.ChangeAmount,
            status = "completed"
        });
        if (created == null) return null;

        foreach (var item in transaction.Items)
            await _db.InsertAsync<TransactionItem>("transaction_items", new
            {
                transaction_id = created.Id,
                product_id = item.ProductId,
                product_name = item.ProductName,
                unit_price = item.UnitPrice,
                quantity = item.Quantity,
                subtotal = item.Subtotal
            });

        await _inventory.DeductForTransactionAsync(created.Id, transaction.Items, userId);
        return created;
    }

    public async Task<List<Transaction>> GetRecentAsync(int limit = 50)
    {
        var txns = await _db.SelectAsync<Transaction>("transactions",
            $"select=*&order=created_at.desc&limit={limit}");
        foreach (var t in txns)
            t.Items = await _db.SelectAsync<TransactionItem>("transaction_items",
                $"transaction_id=eq.{t.Id}&select=*");
        return txns;
    }

    public async Task<List<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to)
    {
        var f = from.ToString("yyyy-MM-ddT00:00:00");
        var t2 = to.ToString("yyyy-MM-ddT23:59:59");
        var txns = await _db.SelectAsync<Transaction>("transactions",
            $"created_at=gte.{f}&created_at=lte.{t2}&status=eq.completed&order=created_at.desc&select=*");
        foreach (var t in txns)
            t.Items = await _db.SelectAsync<TransactionItem>("transaction_items",
                $"transaction_id=eq.{t.Id}&select=*");
        return txns;
    }
}

// ── Report Service ────────────────────────────────────────────
public class ReportService
{
    private readonly TransactionService _txn;
    private readonly InventoryService _inventory;

    public ReportService(TransactionService txn, InventoryService inventory)
    {
        _txn = txn;
        _inventory = inventory;
    }

    public async Task<DailyAnalytics> GetDashboardAnalyticsAsync(string period = "Today")
    {
        var (from, to) = GetDateRange(period);
        var transactions = await _txn.GetByDateRangeAsync(from, to);
        var allInventory = await _inventory.GetAllAsync();

        var lowStock = allInventory.Where(i => i.IsLowStock).ToList();

        // Top selling product
        var productSales = transactions
            .SelectMany(t => t.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Qty)
            .FirstOrDefault();

        // Weekly sales (last 7 days)
        var weeklySales = new List<WeeklySalesPoint>();
        var today = DateTime.Today;
        for (int i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var dayTxns = await _txn.GetByDateRangeAsync(day, day);
            weeklySales.Add(new WeeklySalesPoint
            {
                DayLabel = day.ToString("ddd"),
                Revenue = dayTxns.Sum(t => t.TotalAmount)
            });
        }

        // Top products
        var topProducts = transactions
            .SelectMany(t => t.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new ProductSalesSummary
            {
                ProductName = g.Key,
                TotalSold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Subtotal)
            })
            .OrderByDescending(x => x.TotalSold)
            .Take(5)
            .ToList();

        return new DailyAnalytics
        {
            TodayRevenue = transactions.Sum(t => t.TotalAmount),
            TodayTransactions = transactions.Count,
            TopSellingProduct = productSales?.Name ?? "—",
            LowStockCount = lowStock.Count,
            WeeklySales = weeklySales,
            LowStockItems = lowStock,
            TopProducts = topProducts
        };
    }

    private static (DateTime from, DateTime to) GetDateRange(string period) => period switch
    {
        "Weekly" => (DateTime.Today.AddDays(-6), DateTime.Today),
        "Monthly" => (new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today),
        "Yearly" => (new DateTime(DateTime.Today.Year, 1, 1), DateTime.Today),
        _ => (DateTime.Today, DateTime.Today)  // Today
    };
}
