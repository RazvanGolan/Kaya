using Kaya.ApiExplorer.Configuration;
using Kaya.ApiExplorer.Models;
using Kaya.ApiExplorer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.ApiExplorer.Tests;

public class EndpointScannerTests
{
    // Shared helper so every test gets a correctly-constructed scanner
    private static EndpointScanner CreateScanner(string title = "Kaya API Explorer", string version = "1.0.0")
        => new(new KayaApiExplorerOptions
        {
            Documentation = new DocumentationOptions { Title = title, Version = version }
        });

    private static IServiceProvider EmptyServiceProvider()
        => new ServiceCollection().BuildServiceProvider();

    [Fact]
    public void ScanEndpoints_ShouldFindControllerEndpoints()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        Assert.NotNull(result);
        Assert.Equal("Kaya API Explorer", result.Title);
        Assert.Equal("1.0.0", result.Version);
    }

    [Fact]
    public void ScanEndpoints_ShouldReturnValidDocumentation()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        Assert.IsType<ApiDocumentation>(result);
    }

    [Fact]
    public void ScanEndpoints_ShouldFindTestControllerEndpoints()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var testController = result.Controllers.FirstOrDefault(c => c.Name == "TestController");
        Assert.NotNull(testController);
        Assert.NotEmpty(testController.Endpoints);
        Assert.True(testController.Endpoints.Count >= 3);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectHttpMethods()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var testController = result.Controllers.FirstOrDefault(c => c.Name == "TestController");
        Assert.NotNull(testController);

        var getEndpoint = testController.Endpoints.FirstOrDefault(e => e.MethodName == "Get");
        Assert.NotNull(getEndpoint);
        Assert.Equal("GET", getEndpoint.HttpMethodType);

        var postEndpoint = testController.Endpoints.FirstOrDefault(e => e.MethodName == "Post");
        Assert.NotNull(postEndpoint);
        Assert.Equal("POST", postEndpoint.HttpMethodType);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectRouteParameters()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var testController = result.Controllers.FirstOrDefault(c => c.Name == "TestController");
        Assert.NotNull(testController);

        var getByIdEndpoint = testController.Endpoints.FirstOrDefault(e => e.MethodName == "GetById");
        Assert.NotNull(getByIdEndpoint);
        Assert.Contains("{id}", getByIdEndpoint.Path);

        var idParam = getByIdEndpoint.Parameters.FirstOrDefault(p => p.Name == "id");
        Assert.NotNull(idParam);
        Assert.Equal("Route", idParam.Source);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectRequestBody()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var testController = result.Controllers.FirstOrDefault(c => c.Name == "TestController");
        Assert.NotNull(testController);

        var postEndpoint = testController.Endpoints.FirstOrDefault(e => e.MethodName == "Post");
        Assert.NotNull(postEndpoint);
        Assert.NotNull(postEndpoint.RequestBody);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectComplexTypes()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var advancedController = result.Controllers.FirstOrDefault(c => c.Name == "AdvancedTestController");
        Assert.NotNull(advancedController);

        var createEndpoint = advancedController.Endpoints.FirstOrDefault(e => e.MethodName == "CreateUser");
        Assert.NotNull(createEndpoint);
        Assert.NotNull(createEndpoint.RequestBody);
        Assert.NotNull(createEndpoint.Response);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectMultipleHttpMethodsOnSameAction()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var advancedController = result.Controllers.FirstOrDefault(c => c.Name == "AdvancedTestController");
        Assert.NotNull(advancedController);

        var multiMethodEndpoints = advancedController.Endpoints.Where(e => e.MethodName == "MultiMethod").ToList();
        Assert.True(multiMethodEndpoints.Count >= 2);
    }

    // -------------------------------------------------------------------------
    // ProducesResponseType scanning
    // -------------------------------------------------------------------------

    [Fact]
    public void ScanEndpoints_ShouldScanProducesResponseType_StatusCodesPresent()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ProducesTestController");
        Assert.NotNull(controller);

        var getEndpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "GetItem");
        Assert.NotNull(getEndpoint);

        Assert.Contains(getEndpoint.ProducesResponses, r => r.StatusCode == 200);
        Assert.Contains(getEndpoint.ProducesResponses, r => r.StatusCode == 404);
    }

    [Fact]
    public void ScanEndpoints_ShouldScanProducesResponseType_TypeNameSet()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ProducesTestController");
        Assert.NotNull(controller);

        var getEndpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "GetItem");
        Assert.NotNull(getEndpoint);

        var ok = getEndpoint.ProducesResponses.FirstOrDefault(r => r.StatusCode == 200);
        Assert.NotNull(ok);
        Assert.False(string.IsNullOrWhiteSpace(ok.Type));
        Assert.Contains("TestUser", ok.Type);
    }

    [Fact]
    public void ScanEndpoints_ShouldScanProducesResponseType_NoBodyStatuses()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ProducesTestController");
        Assert.NotNull(controller);

        var getEndpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "GetItem");
        Assert.NotNull(getEndpoint);

        var notFound = getEndpoint.ProducesResponses.FirstOrDefault(r => r.StatusCode == 404);
        Assert.NotNull(notFound);
        Assert.True(string.IsNullOrWhiteSpace(notFound.Type));
    }

    [Fact]
    public void ScanEndpoints_ShouldScanProducesResponseType_ExampleGenerated()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ProducesTestController");
        Assert.NotNull(controller);

        var createEndpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "CreateItem");
        Assert.NotNull(createEndpoint);

        var created = createEndpoint.ProducesResponses.FirstOrDefault(r => r.StatusCode == 201);
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created.Example));
    }

    [Fact]
    public void ScanEndpoints_ShouldOrderProducesResponsesByStatusCode()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ProducesTestController");
        Assert.NotNull(controller);

        var endpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "CreateItem");
        Assert.NotNull(endpoint);

        var codes = endpoint.ProducesResponses.Select(r => r.StatusCode).ToList();
        Assert.Equal(codes.OrderBy(x => x).ToList(), codes);
    }

    [Fact]
    public void ScanEndpoints_ShouldHaveEmptyProducesResponses_WhenNoAttributes()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var testController = result.Controllers.FirstOrDefault(c => c.Name == "TestController");
        Assert.NotNull(testController);

        var getEndpoint = testController.Endpoints.FirstOrDefault(e => e.MethodName == "Get");
        Assert.NotNull(getEndpoint);

        Assert.Empty(getEndpoint.ProducesResponses);
    }

    [Fact]
    public void ScanEndpoints_ShouldSetDefaultDescription_WhenNoXmlDoc()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());

        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ProducesTestController");
        Assert.NotNull(controller);

        var getEndpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "GetItem");
        Assert.NotNull(getEndpoint);

        var ok = getEndpoint.ProducesResponses.FirstOrDefault(r => r.StatusCode == 200);
        Assert.NotNull(ok);
        Assert.False(string.IsNullOrWhiteSpace(ok.Description));

        var notFound = getEndpoint.ProducesResponses.FirstOrDefault(r => r.StatusCode == 404);
        Assert.NotNull(notFound);
        Assert.False(string.IsNullOrWhiteSpace(notFound.Description));
    }
}

// ─── Test controller for ProducesResponseType scanning ────────────────────────

[ApiController]
[Route("api/produces-test")]
public class ProducesTestController : ControllerBase
{
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TestUser), 200)]
    [ProducesResponseType(404)]
    public ActionResult<TestUser> GetItem(int id) => Ok(new TestUser());

    [HttpPost]
    [ProducesResponseType(typeof(TestUser), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public ActionResult<TestUser> CreateItem([FromBody] TestUser user) => CreatedAtAction(nameof(GetItem), new { id = 1 }, user);

    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public IActionResult DeleteItem(int id) => NoContent();
}

// Test models
public class TestUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TestProduct
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public enum TestStatus
{
    Pending,
    Active,
    Completed
}

// Test controllers
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok("test");
    }

    [HttpPost]
    public IActionResult Post([FromBody] string data)
    {
        return Ok(data);
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        return Ok($"test {id}");
    }
}

[ApiController]
[Route("api/advanced")]
public class AdvancedTestController : ControllerBase
{
    [HttpPost("users")]
    public async Task<ActionResult<TestUser>> CreateUser([FromBody] TestUser user)
    {
        await Task.CompletedTask;
        return Ok(user);
    }

    [HttpGet("products/{id}")]
    public ActionResult<TestProduct> GetProduct(Guid id)
    {
        return Ok(new TestProduct());
    }

    [HttpPut("users/{id}")]
    public IActionResult UpdateUser(int id, [FromBody] TestUser user, [FromQuery] bool notify = false)
    {
        return Ok();
    }

    [HttpDelete("products/{id}")]
    public IActionResult DeleteProduct(Guid id)
    {
        return NoContent();
    }

    [HttpGet("status")]
    public ActionResult<TestStatus> GetStatus()
    {
        return Ok(TestStatus.Active);
    }

    [HttpGet("users")]
    public ActionResult<List<TestUser>> GetUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        return Ok(new List<TestUser>());
    }

    [HttpGet("dictionary")]
    public ActionResult<Dictionary<string, int>> GetDictionary()
    {
        return Ok(new Dictionary<string, int>());
    }

    [HttpGet]
    [HttpPost]
    [Route("multi")]
    public IActionResult MultiMethod()
    {
        return Ok();
    }

    [HttpGet("nullable/{id}")]
    public ActionResult<TestUser?> GetNullableUser(int? id)
    {
        return Ok(null);
    }
}
