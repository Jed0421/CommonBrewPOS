using CommonBrewPOS.Models;
using CommonBrewPOS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

// ════════════════════════════════════════════════════════════
// Auth Controller
// ════════════════════════════════════════════════════════════
namespace CommonBrewPOS.Controllers;

public class AuthController : Controller
{
    private readonly AuthService _auth;
    public AuthController(AuthService auth) => _auth = auth;

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(string username, string password, string role)
    {
        var user = await _auth.AuthenticateAsync(username, password);
        if (user == null || user.Role != role)
        {
            ViewBag.Error = "Invalid credentials or role mismatch.";
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.FullName),
            new("Username", user.Username),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return user.Role == "Administrator"
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Index", "POS");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();
}

// ════════════════════════════════════════════════════════════
// Dashboard Controller
// ════════════════════════════════════════════════════════════
[Authorize(Roles = "Administrator")]
public class DashboardController : Controller
{
    private readonly ReportService _report;
    private readonly InventoryService _inventory;

    public DashboardController(ReportService report, InventoryService inventory)
    {
        _report = report;
        _inventory = inventory;
    }

    public async Task<IActionResult> Index(string period = "Today")
    {
        var analytics = await _report.GetDashboardAnalyticsAsync(period);
        ViewBag.Period = period;
        return View(analytics);
    }

    [HttpGet]
    public async Task<IActionResult> GetAnalytics(string period = "Today")
    {
        var analytics = await _report.GetDashboardAnalyticsAsync(period);
        return Json(analytics);
    }
}

// ════════════════════════════════════════════════════════════
// POS Controller
// ════════════════════════════════════════════════════════════
[Authorize]
public class POSController : Controller
{
    private readonly ProductService _products;
    private readonly TransactionService _transactions;

    public POSController(ProductService products, TransactionService transactions)
    {
        _products = products;
        _transactions = transactions;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _products.GetAllAsync();
        var categories = await _products.GetCategoriesAsync();
        ViewBag.Categories = categories;
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts(string? categoryId = null)
    {
        var products = categoryId != null
            ? await _products.GetByCategoryAsync(categoryId)
            : await _products.GetAllAsync();

        return Json(products);
    }

    [HttpPost]
    public async Task<IActionResult> ProcessTransaction([FromBody] TransactionRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

        var transaction = new Transaction
        {
            PaymentMethod = req.PaymentMethod,
            Subtotal = req.Items.Sum(i => i.UnitPrice * i.Quantity),
            TotalAmount = req.Items.Sum(i => i.UnitPrice * i.Quantity),
            CashTendered = req.CashTendered,
            ChangeAmount = req.CashTendered.HasValue
                ? req.CashTendered.Value - req.Items.Sum(i => i.UnitPrice * i.Quantity)
                : null,
            Items = req.Items.Select(i => new TransactionItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity,
                Subtotal = i.UnitPrice * i.Quantity
            }).ToList()
        };

        var created = await _transactions.CreateAsync(transaction, userId);
        if (created == null)
            return Json(new { success = false, message = "Transaction failed." });

        return Json(new
        {
            success = true,
            transactionRef = created.TransactionRef,
            totalAmount = created.TotalAmount,
            change = created.ChangeAmount
        });
    }
}

public class TransactionRequest
{
    public string PaymentMethod { get; set; } = "Cash";
    public decimal? CashTendered { get; set; }
    public List<TransactionItemRequest> Items { get; set; } = new();
}

public class TransactionItemRequest
{
    public string ProductId { get; set; } = "";
    public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

// ════════════════════════════════════════════════════════════
// Inventory Controller
// ════════════════════════════════════════════════════════════
[Authorize(Roles = "Administrator")]
public class InventoryController : Controller
{
    private readonly InventoryService _inventory;

    public InventoryController(InventoryService inventory) => _inventory = inventory;

    public async Task<IActionResult> Index()
        => View(await _inventory.GetAllAsync());

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var item = await _inventory.GetByIdAsync(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(InventoryItem item)
    {
        await _inventory.UpdateItemAsync(item);
        TempData["Success"] = "Inventory item updated.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult AddStock(string id)
    {
        ViewBag.ItemId = id;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AddStock(string itemId, decimal quantity, string? note)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        await _inventory.AddStockAsync(itemId, quantity, note, userId);
        TempData["Success"] = $"Added {quantity} units to stock.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult Create() => View(new InventoryItem());

    [HttpPost]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        await _inventory.CreateItemAsync(item);
        TempData["Success"] = "Inventory item added.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> AddStockAjax([FromBody] AddStockRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        await _inventory.AddStockAsync(req.ItemId, req.Quantity, req.Note, userId);
        return Json(new { success = true });
    }
}

public class AddStockRequest
{
    public string ItemId { get; set; } = "";
    public decimal Quantity { get; set; }
    public string? Note { get; set; }
}

// ════════════════════════════════════════════════════════════
// Products Controller
// ════════════════════════════════════════════════════════════
[Authorize(Roles = "Administrator")]
public class ProductsController : Controller
{
    private readonly ProductService _products;
    private readonly InventoryService _inventory;

    public ProductsController(ProductService products, InventoryService inventory)
    {
        _products = products;
        _inventory = inventory;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _products.GetAllAsync();
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _products.GetCategoriesAsync();
        ViewBag.InventoryItems = await _inventory.GetAllAsync();
        return View(new Product());
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Product product,
        List<string> ingredientIds,
        List<decimal> ingredientQtys,
        List<string> upsizeIngredientIds,
        List<decimal> upsizeIngredientQtys)
    {
        var recipes = ingredientIds
            .Select((id, i) => new ProductRecipe
            {
                InventoryItemId = id,
                Quantity = i < ingredientQtys.Count ? ingredientQtys[i] : 0
            })
            .Where(r => !string.IsNullOrEmpty(r.InventoryItemId) && r.Quantity > 0)
            .ToList();

        var created = await _products.CreateAsync(product, recipes);

        if (created != null &&
            product.ProductType == "Drink" &&
            upsizeIngredientIds != null &&
            upsizeIngredientQtys != null)
        {
            var upsizeRecipes = upsizeIngredientIds
                .Select((id, i) => new ProductRecipe
                {
                    InventoryItemId = id,
                    Quantity = i < upsizeIngredientQtys.Count ? upsizeIngredientQtys[i] : 0
                })
                .Where(r => !string.IsNullOrEmpty(r.InventoryItemId) && r.Quantity > 0)
                .ToList();

            await _products.SaveUpsizeRecipesAsync(created.Id, upsizeRecipes);
        }

        TempData["Success"] = "Product added.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var product = await _products.GetByIdAsync(id);
        if (product == null) return NotFound();

        ViewBag.Categories = await _products.GetCategoriesAsync();
        ViewBag.InventoryItems = await _inventory.GetAllAsync();
        return View(product);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        Product product,
        List<string> ingredientIds,
        List<decimal> ingredientQtys,
        List<string> upsizeIngredientIds,
        List<decimal> upsizeIngredientQtys)
    {
        var recipes = ingredientIds
            .Select((id, i) => new ProductRecipe
            {
                InventoryItemId = id,
                Quantity = i < ingredientQtys.Count ? ingredientQtys[i] : 0
            })
            .Where(r => !string.IsNullOrEmpty(r.InventoryItemId) && r.Quantity > 0)
            .ToList();

        await _products.UpdateAsync(product, recipes);
        await _products.DeleteUpsizeRecipesAsync(product.Id);

        if (product.ProductType == "Drink" &&
            upsizeIngredientIds != null &&
            upsizeIngredientQtys != null)
        {
            var upsizeRecipes = upsizeIngredientIds
                .Select((id, i) => new ProductRecipe
                {
                    InventoryItemId = id,
                    Quantity = i < upsizeIngredientQtys.Count ? upsizeIngredientQtys[i] : 0
                })
                .Where(r => !string.IsNullOrEmpty(r.InventoryItemId) && r.Quantity > 0)
                .ToList();

            await _products.SaveUpsizeRecipesAsync(product.Id, upsizeRecipes);
        }

        TempData["Success"] = "Product updated.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        await _products.DeleteAsync(id);
        TempData["Success"] = "Product removed.";
        return RedirectToAction("Index");
    }
}

// ════════════════════════════════════════════════════════════
// Reports Controller
// ════════════════════════════════════════════════════════════
[Authorize(Roles = "Administrator")]
public class ReportsController : Controller
{
    private readonly TransactionService _transactions;

    public ReportsController(TransactionService transactions) => _transactions = transactions;

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var start = from ?? DateTime.Today.AddDays(-30);
        var end = to ?? DateTime.Today;
        var txns = await _transactions.GetByDateRangeAsync(start, end);
        ViewBag.From = start.ToString("yyyy-MM-dd");
        ViewBag.To = end.ToString("yyyy-MM-dd");
        return View(txns);
    }

    [HttpGet]
    public async Task<IActionResult> GetTransactions(DateTime? from, DateTime? to)
    {
        var start = from ?? DateTime.Today;
        var end = to ?? DateTime.Today;
        var txns = await _transactions.GetByDateRangeAsync(start, end);
        return Json(txns);
    }
}
// ════════════════════════════════════════════════════════════
// Archive Controller
// ════════════════════════════════════════════════════════════
[Authorize(Roles = "Administrator")]
public class ArchiveController : Controller
{
    private readonly ProductService _products;
    private readonly AuthService _auth;

    public ArchiveController(ProductService products, AuthService auth)
    {
        _products = products;
        _auth = auth;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.ArchivedProducts = await _products.GetArchivedAsync();
        ViewBag.ArchivedUsers = await _auth.GetArchivedUsersAsync();
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> RestoreProduct(string id)
    {
        await _products.RestoreAsync(id);
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> RestoreUser(string id)
    {
        await _auth.RestoreUserAsync(id);
        return RedirectToAction("Index");
    }
}

// ════════════════════════════════════════════════════════════
// Settings (User Management) Controller
// ════════════════════════════════════════════════════════════
[Authorize(Roles = "Administrator")]
public class SettingsController : Controller
{
    private readonly AuthService _auth;
    public SettingsController(AuthService auth) => _auth = auth;

    public async Task<IActionResult> Index()
        => View(await _auth.GetAllUsersAsync());

    [HttpGet]
    public IActionResult CreateUser() => View();

    [HttpPost]
    public async Task<IActionResult> CreateUser(string fullName, string username, string password, string role)
    {
        var user = new CommonBrewPOS.Models.User
        {
            FullName = fullName,
            Username = username,
            Role = role
        };

        await _auth.CreateUserAsync(user, password);
        TempData["Success"] = "User created.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteUser(string id)
    {
        await _auth.DeleteUserAsync(id);
        TempData["Success"] = "User deactivated.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string id, string newPassword)
    {
        await _auth.ChangePasswordAsync(id, newPassword);
        TempData["Success"] = "Password updated.";
        return RedirectToAction("Index");
    }
}

// ════════════════════════════════════════════════════════════
// AI Chatbot API Controller
// ════════════════════════════════════════════════════════════
[Authorize]
[Route("api/chatbot")]
[ApiController]
public class ChatbotApiController : ControllerBase
{
    private readonly AiChatbotService _ai;
    public ChatbotApiController(AiChatbotService ai) => _ai = ai;

    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest req)
    {
        var reply = await _ai.AskAsync(req.Question);
        return Ok(new { reply });
    }
}

public class ChatRequest
{
    public string Question { get; set; } = "";
}