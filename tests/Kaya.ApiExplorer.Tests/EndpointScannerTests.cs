using Kaya.ApiExplorer.Configuration;
using Kaya.ApiExplorer.Models;
using Kaya.ApiExplorer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

    // -------------------------------------------------------------------------
    // Documentation metadata: contact, license, servers, termsOfService
    // -------------------------------------------------------------------------

    private static EndpointScanner CreateScannerWithMetadata()
        => new(new KayaApiExplorerOptions
        {
            Documentation = new DocumentationOptions
            {
                Title = "Meta API",
                Version = "2.0.0",
                Description = "Full description",
                TermsOfService = "https://example.com/terms",
                Contact = new ContactOptions { Name = "John", Email = "john@example.com", Url = "https://john.com" },
                License = new LicenseOptions { Name = "MIT", Url = "https://opensource.org/licenses/MIT" },
                Servers = [new ServerOptions { Url = "https://api.example.com", Description = "Production" }]
            }
        });

    [Fact]
    public void ScanEndpoints_ShouldIncludeContactInfo_WhenProvided()
    {
        var result = CreateScannerWithMetadata().ScanEndpoints(EmptyServiceProvider());
        Assert.NotNull(result.Contact);
        Assert.Equal("John", result.Contact.Name);
        Assert.Equal("john@example.com", result.Contact.Email);
    }

    [Fact]
    public void ScanEndpoints_ShouldIncludeLicenseInfo_WhenProvided()
    {
        var result = CreateScannerWithMetadata().ScanEndpoints(EmptyServiceProvider());
        Assert.NotNull(result.License);
        Assert.Equal("MIT", result.License.Name);
    }

    [Fact]
    public void ScanEndpoints_ShouldIncludeServers_WhenProvided()
    {
        var result = CreateScannerWithMetadata().ScanEndpoints(EmptyServiceProvider());
        Assert.NotEmpty(result.Servers);
        Assert.Equal("https://api.example.com", result.Servers[0].Url);
    }

    [Fact]
    public void ScanEndpoints_ShouldIncludeTermsOfService_WhenProvided()
    {
        var result = CreateScannerWithMetadata().ScanEndpoints(EmptyServiceProvider());
        Assert.Equal("https://example.com/terms", result.TermsOfService);
    }

    // -------------------------------------------------------------------------
    // Authorization & obsolete detection on controllers
    // -------------------------------------------------------------------------

    [Fact]
    public void ScanEndpoints_ShouldDetectAuthorizationOnController()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "AuthTestController");
        Assert.NotNull(controller);
        Assert.True(controller.RequiresAuthorization);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectRolesOnController()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "AuthTestController");
        Assert.NotNull(controller);
        Assert.Contains("Admin", controller.Roles);
        Assert.Contains("Manager", controller.Roles);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectObsoleteController()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ObsoleteTestController");
        Assert.NotNull(controller);
        Assert.True(controller.IsObsolete);
        Assert.False(string.IsNullOrWhiteSpace(controller.ObsoleteMessage));
    }

    // -------------------------------------------------------------------------
    // Special parameter sources: Header, Form, File, Enum, Default
    // -------------------------------------------------------------------------

    [Fact]
    public void ScanEndpoints_ShouldDetectHeaderParameter()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ExtraTestController");
        Assert.NotNull(controller);
        var endpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "GetWithHeader");
        Assert.NotNull(endpoint);
        Assert.Contains(endpoint.Parameters, p => p.Source == "Header");
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectFileParameter()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ExtraTestController");
        Assert.NotNull(controller);
        var endpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "UploadFile");
        Assert.NotNull(endpoint);
        Assert.Contains(endpoint.Parameters, p => p.IsFile && p.Source == "File");
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectFormParameter()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ExtraTestController");
        Assert.NotNull(controller);
        var endpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "PostForm");
        Assert.NotNull(endpoint);
        Assert.Contains(endpoint.Parameters, p => p.Source == "Form");
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectEnumParameter()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ExtraTestController");
        Assert.NotNull(controller);
        var endpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "GetWithEnum");
        Assert.NotNull(endpoint);
        Assert.Contains(endpoint.Parameters, p => p.IsEnum);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectDefaultParameterValue()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "ExtraTestController");
        Assert.NotNull(controller);
        var endpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "GetWithDefault");
        Assert.NotNull(endpoint);
        var param = endpoint.Parameters.FirstOrDefault(p => p.Name == "count");
        Assert.NotNull(param);
        // int has a default value of 10; the scanner exposes it via DefaultValue
        Assert.Equal(10, param.DefaultValue);
    }

    [Fact]
    public void ScanEndpoints_ShouldDetectQueryParameter_WithDefaultValue()
    {
        var result = CreateScanner().ScanEndpoints(EmptyServiceProvider());
        var controller = result.Controllers.FirstOrDefault(c => c.Name == "AdvancedTestController");
        Assert.NotNull(controller);
        var endpoint = controller.Endpoints.FirstOrDefault(e => e.MethodName == "UpdateUser");
        Assert.NotNull(endpoint);
        var notifyParam = endpoint.Parameters.FirstOrDefault(p => p.Name == "notify");
        Assert.NotNull(notifyParam);
        Assert.Equal("Query", notifyParam.Source);
        // bool notify = false has a default value exposed by the scanner
        Assert.NotNull(notifyParam.DefaultValue);
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

// ─── Additional test controllers ──────────────────────────────────────────────

[ApiController]
[Route("api/extra-test")]
public class ExtraTestController : ControllerBase
{
    [HttpGet("with-header")]
    public IActionResult GetWithHeader([FromHeader(Name = "X-Custom-Header")] string customHeader) => Ok();

    [HttpPost("upload")]
    public IActionResult UploadFile(IFormFile file) => Ok();

    [HttpPost("form")]
    public IActionResult PostForm([FromForm] string name, [FromForm] int age) => Ok();

    [HttpGet("with-enum")]
    public IActionResult GetWithEnum([FromQuery] TestStatus status) => Ok();

    [HttpGet("with-default")]
    public IActionResult GetWithDefault([FromQuery] int count = 10) => Ok();
}

[ApiController]
[Route("api/auth-test")]
[Authorize(Roles = "Admin,Manager")]
public class AuthTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}

[ApiController]
[Route("api/obsolete-test")]
[Obsolete("Use the new V2 controller instead")]
public class ObsoleteTestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok();
}
