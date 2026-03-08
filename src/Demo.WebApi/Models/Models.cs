using System.ComponentModel.DataAnnotations;

namespace Demo.WebApi.Models;

// Enums
public enum UserRole
{
    Guest = 0,
    User = 1,
    Admin = 2,
    SuperAdmin = 3
}

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled,
    Refunded
}

public enum ProductCategory
{
    Electronics,
    Clothing,
    Books,
    Home,
    Sports,
    Automotive,
    Beauty,
    Toys
}

public enum Priority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

// Records
public record Address(
    string Street,
    string City,
    string State,
    string ZipCode,
    string Country
);

public record ContactInfo(
    string Email,
    string? Phone,
    Address Address
);

public record PaginationRequest(
    int Page = 1,
    int PageSize = 10,
    string? SortBy = null,
    bool SortDescending = false
);

public record ApiResponse<T>(
    bool Success,
    T? Data,
    string? Message,
    Dictionary<string, string[]>? Errors = null
);

// Complex Classes
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public ContactInfo? ContactInfo { get; set; }
    public UserPreferences Preferences { get; set; } = new();
    public List<string> Tags { get; set; } = [];
}

public class UserPreferences
{
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "en";
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
    public List<string> Interests { get; set; } = [];
}

public class CreateUserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.User;
    public ContactInfo? ContactInfo { get; set; }
    public UserPreferences? Preferences { get; set; }

    [MaxLength(20)]
    public List<string> Tags { get; set; } = [];
}

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public bool? IsActive { get; set; }
    public UserRole? Role { get; set; }
    public ContactInfo? ContactInfo { get; set; }
    public UserPreferences? Preferences { get; set; }
    public List<string>? Tags { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public ProductCategory Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public ProductDetails Details { get; set; } = new();
    public List<ProductImage> Images { get; set; } = [];
    public List<ProductReview> Reviews { get; set; } = [];
    public ProductMetrics Metrics { get; set; } = new();
}

public class ProductDetails
{
    public string? Sku { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public Dictionary<string, string> Specifications { get; set; } = new();
    public Dimensions? Dimensions { get; set; }
    public decimal? Weight { get; set; }
    public List<string> Colors { get; set; } = [];
    public List<string> Sizes { get; set; } = [];
}

public class Dimensions
{
    public decimal Length { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public string Unit { get; set; } = "cm";
}

public class ProductImage
{
    public int Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string AltText { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int Order { get; set; }
}

public class ProductReview
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsVerifiedPurchase { get; set; }
}

public class ProductMetrics
{
    public int ViewCount { get; set; }
    public int PurchaseCount { get; set; }
    public int ReviewCount { get; set; }
    public decimal AverageRating { get; set; }
    public int WishlistCount { get; set; }
}

public class CreateProductRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public ProductCategory Category { get; set; }
    public ProductDetails? Details { get; set; }

    [MaxLength(10)]
    public List<ProductImage> Images { get; set; } = [];
}

// Order-related models
public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public List<OrderItem> Items { get; set; } = [];
    public OrderShipping Shipping { get; set; } = new();
    public OrderBilling Billing { get; set; } = new();
    public OrderTotals Totals { get; set; } = new();
    public List<OrderNote> Notes { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public Dictionary<string, string> ProductOptions { get; set; } = new();
}

public class OrderShipping
{
    public Address Address { get; set; } = new("", "", "", "", "");
    public string Method { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Carrier { get; set; }
}

public class OrderBilling
{
    public Address Address { get; set; } = new("", "", "", "", "");
    public string PaymentMethod { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
}

public class OrderTotals
{
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Shipping { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
}

public class OrderNote
{
    public int Id { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
}

public class CreateOrderRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    [MinLength(1)]
    public List<OrderItemRequest> Items { get; set; } = [];

    public OrderShipping Shipping { get; set; } = new();
    public OrderBilling Billing { get; set; } = new();

    [StringLength(1000)]
    public string? Notes { get; set; }
}

public class OrderItemRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 10000)]
    public int Quantity { get; set; }

    public Dictionary<string, string> ProductOptions { get; set; } = new();
}

// Analytics and reporting models
public class AnalyticsReport
{
    public DateTime GeneratedAt { get; set; }
    public DateRange Period { get; set; } = new();
    public SalesMetrics Sales { get; set; } = new();
    public UserMetrics Users { get; set; } = new();
    public ProductMetrics Products { get; set; } = new();
    public Dictionary<string, decimal> CustomMetrics { get; set; } = new();
}

public class DateRange
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class SalesMetrics
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalCustomers { get; set; }
    public List<DailySales> DailySales { get; set; } = [];
}

public class UserMetrics
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int NewUsers { get; set; }
    public Dictionary<UserRole, int> UsersByRole { get; set; } = new();
}

public class DailySales
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
    public int Customers { get; set; }
}

// Complex search and filter models
public class ProductSearchRequest
{
    public string? Query { get; set; }
    public ProductCategory? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<string> Brands { get; set; } = [];
    public List<string> Colors { get; set; } = [];
    public List<string> Sizes { get; set; } = [];
    public int? MinRating { get; set; }
    public bool InStockOnly { get; set; } = false;
    public PaginationRequest Pagination { get; set; } = new();
    public Dictionary<string, List<string>> Filters { get; set; } = new();
}

public class ProductSearchResponse
{
    public List<Product> Products { get; set; } = [];
    public SearchMetadata Metadata { get; set; } = new();
    public Dictionary<string, List<FilterOption>> AvailableFilters { get; set; } = new();
}

public class SearchMetadata
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
}

public class FilterOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

// Notification models
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public Priority Priority { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public enum NotificationType
{
    System,
    Order,
    Product,
    User,
    Marketing,
    Security
}

public class CreateNotificationRequest
{
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public Dictionary<string, object> Data { get; set; } = new();
}

// Profile update form data model
public class ProfileUpdateFormData
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Bio { get; set; }
    public bool ReceiveNewsletter { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public IFormFile? ProfileImage { get; set; }
}

// Additional form data for address
public class AddressFormData
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
