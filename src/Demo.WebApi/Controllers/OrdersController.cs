using Demo.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private static readonly List<Order> _orders = [];
    private static int _nextOrderId = 1;

    /// <summary>
    /// Gets orders with complex filtering and pagination
    /// </summary>
    /// <param name="status">Filter by order status</param>
    /// <param name="userId">Filter by user ID</param>
    /// <param name="startDate">Start date for date range filter</param>
    /// <param name="endDate">End date for date range filter</param>
    /// <param name="pagination">Pagination parameters</param>
    /// <returns>Paginated list of orders with metadata</returns>
    /// <response code="200">Returns the paginated list of orders</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<Order>>), 200)]
    public ActionResult<ApiResponse<ProductSearchResponse>> GetOrders(
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int? userId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] PaginationRequest pagination = null!)
    {
        pagination ??= new PaginationRequest();
        
        var filteredOrders = _orders.AsEnumerable();

        if (status.HasValue)
            filteredOrders = filteredOrders.Where(o => o.Status == status.Value);

        if (userId.HasValue)
            filteredOrders = filteredOrders.Where(o => o.UserId == userId.Value);

        if (startDate.HasValue)
            filteredOrders = filteredOrders.Where(o => o.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            filteredOrders = filteredOrders.Where(o => o.CreatedAt <= endDate.Value);

        var totalCount = filteredOrders.Count();
        var pagedOrders = filteredOrders
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToList();

        var response = new ApiResponse<List<Order>>(
            Success: true,
            Data: pagedOrders,
            Message: $"Retrieved {pagedOrders.Count} orders"
        );

        return Ok(response);
    }

    /// <summary>
    /// Gets a specific order with all related data
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <returns>Complete order details</returns>
    /// <response code="200">Returns the complete order</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">Order with the specified ID was not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<Order>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public ActionResult<ApiResponse<Order>> GetOrder(int id)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            return NotFound(new ApiResponse<Order>(
                Success: false,
                Data: null,
                Message: $"Order with ID {id} not found"
            ));
        }

        return Ok(new ApiResponse<Order>(
            Success: true,
            Data: order,
            Message: "Order retrieved successfully"
        ));
    }

    /// <summary>
    /// Creates a new order with complex validation
    /// </summary>
    /// <param name="request">Order creation request with items, shipping, and billing</param>
    /// <returns>Created order with calculated totals</returns>
    /// <response code="201">Order created successfully with calculated totals</response>
    /// <response code="400">Validation failed — order must contain at least one item</response>
    /// <response code="401">Authentication required</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<Order>), 201)]
    [ProducesResponseType(typeof(ApiResponse<Order>), 400)]
    [ProducesResponseType(401)]
    public ActionResult<ApiResponse<Order>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        if (!request.Items.Any())
        {
            return BadRequest(new ApiResponse<Order>(
                Success: false,
                Data: null,
                Message: "Order must contain at least one item",
                Errors: new Dictionary<string, string[]>
                {
                    ["Items"] = ["At least one item is required"]
                }
            ));
        }

        var order = new Order
        {
            Id = _nextOrderId++,
            UserId = request.UserId,
            OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{_nextOrderId:D6}",
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = [.. request.Items.Select(item => new OrderItem
            {
                Id = item.ProductId,
                ProductId = item.ProductId,
                ProductName = $"Product {item.ProductId}",
                UnitPrice = 29.99m,
                Quantity = item.Quantity,
                TotalPrice = 29.99m * item.Quantity,
                ProductOptions = item.ProductOptions
            })],
            Shipping = request.Shipping,
            Billing = request.Billing,
            Totals = new OrderTotals
            {
                Subtotal = request.Items.Sum(i => 29.99m * i.Quantity),
                Tax = request.Items.Sum(i => 29.99m * i.Quantity) * 0.08m,
                Shipping = 9.99m,
                Discount = 0m,
                Total = request.Items.Sum(i => 29.99m * i.Quantity) * 1.08m + 9.99m
            },
            Notes = []
        };

        if (!string.IsNullOrEmpty(request.Notes))
        {
            order.Notes.Add(new OrderNote
            {
                Id = 1,
                Note = request.Notes,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System",
                IsPublic = false
            });
        }

        _orders.Add(order);

        return CreatedAtAction(
            nameof(GetOrder),
            new { id = order.Id },
            new ApiResponse<Order>(
                Success: true,
                Data: order,
                Message: "Order created successfully"
            )
        );
    }

    /// <summary>
    /// Updates order status with complex business logic
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="newStatus">New order status</param>
    /// <param name="note">Optional note for the status change</param>
    /// <returns>Updated order</returns>
    /// <response code="200">Returns the order with updated status</response>
    /// <response code="400">Invalid status transition for the current order state</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">Order with the specified ID was not found</response>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<Order>), 200)]
    [ProducesResponseType(typeof(ApiResponse<Order>), 400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public ActionResult<ApiResponse<Order>> UpdateOrderStatus(
        int id,
        [FromBody] OrderStatus newStatus,
        [FromQuery] string? note = null)
    {
        var order = _orders.FirstOrDefault(o => o.Id == id);
        if (order == null)
        {
            return NotFound(new ApiResponse<Order>(
                Success: false,
                Data: null,
                Message: $"Order with ID {id} not found"
            ));
        }

        // Business logic for status transitions
        var validTransitions = GetValidStatusTransitions(order.Status);
        if (!validTransitions.Contains(newStatus))
        {
            return BadRequest(new ApiResponse<Order>(
                Success: false,
                Data: null,
                Message: $"Cannot transition from {order.Status} to {newStatus}",
                Errors: new Dictionary<string, string[]>
                {
                    ["Status"] = [$"Invalid status transition from {order.Status} to {newStatus}"]
                }
            ));
        }

        order.Status = newStatus;

        // Update timestamps based on status
        switch (newStatus)
        {
            case OrderStatus.Shipped:
                order.ShippedAt = DateTime.UtcNow;
                break;
            case OrderStatus.Delivered:
                order.DeliveredAt = DateTime.UtcNow;
                break;
        }

        // Add status change note
        if (!string.IsNullOrEmpty(note))
        {
            order.Notes.Add(new OrderNote
            {
                Id = order.Notes.Count + 1,
                Note = $"Status changed to {newStatus}: {note}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Admin",
                IsPublic = true
            });
        }

        return Ok(new ApiResponse<Order>(
            Success: true,
            Data: order,
            Message: $"Order status updated to {newStatus}"
        ));
    }

    /// <summary>
    /// Gets analytics report with complex aggregations
    /// </summary>
    /// <param name="startDate">Report start date</param>
    /// <param name="endDate">Report end date</param>
    /// <param name="groupBy">Group results by day, week, or month</param>
    /// <returns>Analytics report with sales metrics</returns>
    /// <response code="200">Returns the analytics report for the requested period</response>
    /// <response code="401">Authentication required</response>
    /// <response code="403">Only Admin role can access analytics</response>
    [HttpGet("analytics")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<AnalyticsReport>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public ActionResult<ApiResponse<AnalyticsReport>> GetAnalytics(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string groupBy = "day")
    {
        var filteredOrders = _orders
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
            .ToList();

        var report = new AnalyticsReport
        {
            GeneratedAt = DateTime.UtcNow,
            Period = new DateRange
            {
                StartDate = startDate,
                EndDate = endDate
            },
            Sales = new SalesMetrics
            {
                TotalRevenue = filteredOrders.Sum(o => o.Totals.Total),
                TotalOrders = filteredOrders.Count,
                AverageOrderValue = filteredOrders.Any() ? filteredOrders.Average(o => o.Totals.Total) : 0,
                TotalCustomers = filteredOrders.Select(o => o.UserId).Distinct().Count(),
                DailySales = GenerateDailySales(filteredOrders, startDate, endDate)
            },
            Users = new UserMetrics
            {
                TotalUsers = filteredOrders.Select(o => o.UserId).Distinct().Count(),
                ActiveUsers = filteredOrders.Select(o => o.UserId).Distinct().Count(),
                NewUsers = 0,
                UsersByRole = new Dictionary<UserRole, int>
                {
                    [UserRole.User] = filteredOrders.Select(o => o.UserId).Distinct().Count(),
                    [UserRole.Admin] = 0
                }
            },
            CustomMetrics = new Dictionary<string, decimal>
            {
                ["conversion_rate"] = 0.85m,
                ["cart_abandonment_rate"] = 0.15m,
                ["repeat_customer_rate"] = 0.45m
            }
        };

        return Ok(new ApiResponse<AnalyticsReport>(
            Success: true,
            Data: report,
            Message: "Analytics report generated successfully"
        ));
    }

    /// <summary>
    /// Bulk update multiple orders
    /// </summary>
    /// <param name="updates">Dictionary of order IDs and their new status</param>
    /// <returns>Results of bulk update operation</returns>
    /// <response code="200">Returns per-order update results</response>
    /// <response code="401">Authentication required</response>
    /// <response code="403">Only Admin or Manager role can bulk update orders</response>
    [HttpPatch("bulk-update")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<int, string>>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [Obsolete("This endpoint will be removed in v2.0. Use the new batch processing API instead.")]
    public ActionResult<ApiResponse<Dictionary<int, string>>> BulkUpdateOrders(
        [FromBody] Dictionary<int, OrderStatus> updates)
    {
        var results = new Dictionary<int, string>();

        foreach (var (orderId, newStatus) in updates)
        {
            var order = _orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null)
            {
                results[orderId] = $"Order {orderId} not found";
                continue;
            }

            var validTransitions = GetValidStatusTransitions(order.Status);
            if (!validTransitions.Contains(newStatus))
            {
                results[orderId] = $"Invalid status transition from {order.Status} to {newStatus}";
                continue;
            }

            order.Status = newStatus;
            results[orderId] = "Updated successfully";
        }

        return Ok(new ApiResponse<Dictionary<int, string>>(
            Success: true,
            Data: results,
            Message: $"Processed {updates.Count} order updates"
        ));
    }

    private static List<OrderStatus> GetValidStatusTransitions(OrderStatus currentStatus)
    {
        return currentStatus switch
        {
            OrderStatus.Pending => [OrderStatus.Processing, OrderStatus.Cancelled],
            OrderStatus.Processing => [OrderStatus.Shipped, OrderStatus.Cancelled],
            OrderStatus.Shipped => [OrderStatus.Delivered],
            OrderStatus.Delivered => [OrderStatus.Refunded],
            OrderStatus.Cancelled => [],
            OrderStatus.Refunded => [],
            _ => []
        };
    }

    private static List<DailySales> GenerateDailySales(List<Order> orders, DateTime startDate, DateTime endDate)
    {
        var dailySales = new List<DailySales>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            var dayOrders = orders.Where(o => o.CreatedAt.Date == currentDate).ToList();

            dailySales.Add(new DailySales
            {
                Date = currentDate,
                Revenue = dayOrders.Sum(o => o.Totals.Total),
                Orders = dayOrders.Count,
                Customers = dayOrders.Select(o => o.UserId).Distinct().Count()
            });

            currentDate = currentDate.AddDays(1);
        }

        return dailySales;
    }
}
