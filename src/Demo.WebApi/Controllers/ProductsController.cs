using Demo.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Demo.WebApi.Controllers;

/// <summary>
/// Product catalog and inventory management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private static readonly List<Product> _products =
    [
        new Product 
        { 
            Id = 1, 
            Name = "Gaming Laptop", 
            Description = "High-performance gaming laptop with RTX 4060", 
            Price = 1299.99m, 
            StockQuantity = 10, 
            Category = ProductCategory.Electronics, 
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            Details = new ProductDetails
            {
                Sku = "LAP-001",
                Brand = "TechCorp",
                Model = "GameMaster Pro",
                Specifications = new Dictionary<string, string>
                {
                    ["CPU"] = "Intel i7-12700H",
                    ["GPU"] = "RTX 4060 8GB",
                    ["RAM"] = "16GB DDR4",
                    ["Storage"] = "1TB NVMe SSD"
                }
            }
        },
        new Product 
        { 
            Id = 2, 
            Name = "Wireless Mouse", 
            Description = "Ergonomic wireless gaming mouse", 
            Price = 79.99m, 
            StockQuantity = 50, 
            Category = ProductCategory.Electronics, 
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            Details = new ProductDetails
            {
                Sku = "MOU-001",
                Brand = "GameTech",
                Model = "Pro Click"
            }
        },
        new Product 
        { 
            Id = 3, 
            Name = "Coffee Mug", 
            Description = "Ceramic coffee mug", 
            Price = 12.99m, 
            StockQuantity = 25, 
            Category = ProductCategory.Home, 
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        }
    ];

    /// <summary>
    /// Gets all products with optional filtering
    /// </summary>
    /// <param name="category">Filter by category</param>
    /// <param name="minPrice">Minimum price filter</param>
    /// <param name="maxPrice">Maximum price filter</param>
    /// <returns>List of products</returns>
    /// <response code="200">Returns the list of products matching the filters</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    public ActionResult<IEnumerable<Product>> GetProducts(
        [FromQuery] ProductCategory? category = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null)
    {
        var products = _products.AsEnumerable();

        if (category.HasValue)
        {
            products = products.Where(p => p.Category == category.Value);
        }

        if (minPrice.HasValue)
        {
            products = products.Where(p => p.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            products = products.Where(p => p.Price <= maxPrice.Value);
        }

        return Ok(products);
    }

    /// <summary>
    /// Gets a specific product by ID
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <returns>Product details</returns>
    /// <response code="200">Returns the requested product</response>
    /// <response code="404">Product with the specified ID was not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(404)]
    public ActionResult<Product> GetProduct(int id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return NotFound($"Product with ID {id} not found");
        }

        return Ok(product);
    }

    /// <summary>
    /// Creates a new product
    /// </summary>
    /// <param name="request">Product creation data</param>
    /// <returns>Created product</returns>
    /// <response code="201">Product created successfully</response>
    /// <response code="400">Validation failed — product name is required</response>
    /// <response code="401">Authentication required</response>
    /// <response code="403">Only Admin role can create products</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(Product), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public ActionResult<Product> CreateProduct([FromBody] CreateProductRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return BadRequest("Product name is required");
        }

        var product = new Product
        {
            Id = _products.Max(p => p.Id) + 1,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            Category = request.Category,
            CreatedAt = DateTime.UtcNow,
            Details = request.Details ?? new ProductDetails()
        };

        _products.Add(product);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    /// <summary>
    /// Updates product stock quantity
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="quantity">New stock quantity</param>
    /// <returns>Updated product</returns>
    /// <response code="200">Returns the product with updated stock</response>
    /// <response code="401">Authentication required</response>
    /// <response code="403">Only Admin or Manager role can update stock</response>
    /// <response code="404">Product with the specified ID was not found</response>
    [HttpPatch("{id}/stock")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(Product), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public ActionResult<Product> UpdateStock(int id, [FromBody] int quantity)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return NotFound($"Product with ID {id} not found");
        }

        product.StockQuantity = quantity;
        return Ok(product);
    }

    /// <summary>
    /// Gets all product categories
    /// </summary>
    /// <returns>List of categories</returns>
    [HttpGet("categories")]
    public ActionResult<IEnumerable<ProductCategory>> GetCategories()
    {
        var categories = Enum.GetValues<ProductCategory>().OrderBy(c => c.ToString());
        return Ok(categories);
    }

    /// <summary>
    /// Searches products by name or description
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>Matching products</returns>
    /// <response code="200">Returns matching products</response>
    /// <response code="400">Search query is required</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<Product>), 200)]
    [ProducesResponseType(400)]
    public ActionResult<IEnumerable<Product>> SearchProducts([FromQuery] string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return BadRequest("Search query is required");
        }

        var products = _products.Where(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

        return Ok(products);
    }

    /// <summary>
    /// Uploads a product image
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="file">Image file to upload</param>
    /// <returns>Upload result</returns>
    /// <response code="200">Image uploaded successfully</response>
    /// <response code="400">Invalid file — wrong type or exceeds 5 MB limit</response>
    /// <response code="401">Authentication required</response>
    /// <response code="404">Product with the specified ID was not found</response>
    [HttpPost("{id}/image")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public ActionResult UploadProductImage(int id, IFormFile file)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return NotFound($"Product with ID {id} not found");
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        // Validate file type
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest("Invalid file type. Only image files are allowed.");
        }

        // Validate file size (max 5MB)
        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest("File size exceeds 5MB limit");
        }

        return Ok(new
        {
            productId = id,
            fileName = file.FileName,
            fileSize = file.Length,
            contentType = file.ContentType,
            uploadedAt = DateTime.UtcNow,
            message = "Image uploaded successfully"
        });
    }

    /// <summary>
    /// Imports products from a CSV file
    /// </summary>
    /// <param name="file">CSV file containing product data</param>
    /// <returns>Import result</returns>
    [HttpPost("import")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ImportProducts(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv")
        {
            return BadRequest("Invalid file type. Only CSV files are allowed.");
        }

        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        return Ok(new
        {
            fileName = file.FileName,
            fileSize = file.Length,
            linesProcessed = lines.Length,
            importedAt = DateTime.UtcNow,
            message = $"Processed {lines.Length} lines from CSV file"
        });
    }
}
