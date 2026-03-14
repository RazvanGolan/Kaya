using Kaya.ApiExplorer.Models;
using Kaya.ApiExplorer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.ApiExplorer.Tests;

public class SignalRHubScannerTests
{
    [Fact]
    public void ScanHubs_ShouldReturnValidDocumentation()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SignalR Hubs", result.Title);
        Assert.Equal("1.0.0", result.Version);
        Assert.IsType<SignalRDocumentation>(result);
    }

    [Fact]
    public void ScanHubs_ShouldFindTestHub()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        Assert.Equal("/test", testHub.Path);
    }

    [Fact]
    public void ScanHubs_ShouldDetectHubMethods()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        Assert.NotEmpty(testHub.Methods);
        Assert.True(testHub.Methods.Count >= 2);
    }

    [Fact]
    public void ScanHubs_ShouldDetectMethodParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var sendMessageMethod = testHub.Methods.FirstOrDefault(m => m.Name == "SendMessage");
        Assert.NotNull(sendMessageMethod);
        Assert.NotEmpty(sendMessageMethod.Parameters);
        
        var messageParam = sendMessageMethod.Parameters.FirstOrDefault(p => p.Name == "message");
        Assert.NotNull(messageParam);
        Assert.Equal("string", messageParam.Type);
    }

    [Fact]
    public void ScanHubs_ShouldDetectReturnTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var getUserMethod = testHub.Methods.FirstOrDefault(m => m.Name == "GetUser");
        Assert.NotNull(getUserMethod);
        Assert.NotEqual("void", getUserMethod.ReturnType);
        Assert.NotNull(getUserMethod.ReturnTypeExample);
    }

    [Fact]
    public void ScanHubs_ShouldDetectComplexTypeParameters()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var updateUserMethod = testHub.Methods.FirstOrDefault(m => m.Name == "UpdateUser");
        Assert.NotNull(updateUserMethod);
        
        var userParam = updateUserMethod.Parameters.FirstOrDefault(p => p.Name == "user");
        Assert.NotNull(userParam);
        Assert.NotNull(userParam.Schema);
        Assert.NotNull(userParam.Example);
    }

    [Fact]
    public void ScanHubs_ShouldDetectAuthorizationOnHub()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var authorizedHub = result.Hubs.FirstOrDefault(h => h.Name == "AuthorizedTestHub");
        Assert.NotNull(authorizedHub);
        Assert.True(authorizedHub.RequiresAuthorization);
    }

    [Fact]
    public void ScanHubs_ShouldDetectRolesOnHub()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var authorizedHub = result.Hubs.FirstOrDefault(h => h.Name == "AuthorizedTestHub");
        Assert.NotNull(authorizedHub);
        Assert.NotEmpty(authorizedHub.Roles);
        Assert.Contains("Admin", authorizedHub.Roles);
    }

    [Fact]
    public void ScanHubs_ShouldDetectPoliciesOnHub()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var authorizedHub = result.Hubs.FirstOrDefault(h => h.Name == "AuthorizedTestHub");
        Assert.NotNull(authorizedHub);
        Assert.NotEmpty(authorizedHub.Policies);
        Assert.Contains("RequireAdminPolicy", authorizedHub.Policies);
    }

    [Fact]
    public void ScanHubs_ShouldDetectAuthorizationOnMethod()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var adminOnlyMethod = testHub.Methods.FirstOrDefault(m => m.Name == "AdminOnlyMethod");
        Assert.NotNull(adminOnlyMethod);
        Assert.True(adminOnlyMethod.RequiresAuthorization);
        Assert.Contains("Admin", adminOnlyMethod.Roles);
    }

    [Fact]
    public void ScanHubs_ShouldDetectObsoleteHub()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var obsoleteHub = result.Hubs.FirstOrDefault(h => h.Name == "ObsoleteTestHub");
        Assert.NotNull(obsoleteHub);
        Assert.True(obsoleteHub.IsObsolete);
        Assert.NotNull(obsoleteHub.ObsoleteMessage);
        Assert.Contains("deprecated", obsoleteHub.ObsoleteMessage.ToLower());
    }

    [Fact]
    public void ScanHubs_ShouldDetectObsoleteMethod()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var obsoleteMethod = testHub.Methods.FirstOrDefault(m => m.Name == "OldMethod");
        Assert.NotNull(obsoleteMethod);
        Assert.True(obsoleteMethod.IsObsolete);
    }

    [Fact]
    public void ScanHubs_ShouldExcludeInheritedHubMethods()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        // Should not include inherited methods like OnConnectedAsync, OnDisconnectedAsync, Dispose, etc.
        Assert.DoesNotContain(testHub.Methods, m => m.Name == "OnConnectedAsync");
        Assert.DoesNotContain(testHub.Methods, m => m.Name == "OnDisconnectedAsync");
        Assert.DoesNotContain(testHub.Methods, m => m.Name == "Dispose");
        Assert.DoesNotContain(testHub.Methods, m => m.Name == "GetHashCode");
    }

    [Fact]
    public void ScanHubs_ShouldDetectDefaultParameterValues()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var methodWithDefaults = testHub.Methods.FirstOrDefault(m => m.Name == "MethodWithDefaults");
        Assert.NotNull(methodWithDefaults);
        
        var optionalParam = methodWithDefaults.Parameters.FirstOrDefault(p => p.Name == "optional");
        Assert.NotNull(optionalParam);
        Assert.False(optionalParam.Required);
        Assert.NotNull(optionalParam.DefaultValue);
    }

    [Fact]
    public void ScanHubs_ShouldHandleAsyncMethods()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var asyncMethod = testHub.Methods.FirstOrDefault(m => m.Name == "GetUserAsync");
        Assert.NotNull(asyncMethod);
        Assert.NotEqual("void", asyncMethod.ReturnType);
    }

    [Fact]
    public void ScanHubs_ShouldNotIncludeHubsWithoutMethods()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var emptyHub = result.Hubs.FirstOrDefault(h => h.Name == "EmptyTestHub");
        Assert.Null(emptyHub);
    }

    [Fact]
    public void ScanHubs_ShouldHandleValueTaskReturnType()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var testHub = result.Hubs.FirstOrDefault(h => h.Name == "TestHub");
        Assert.NotNull(testHub);
        
        var valueTaskMethod = testHub.Methods.FirstOrDefault(m => m.Name == "GetCountValueTask");
        Assert.NotNull(valueTaskMethod);
        Assert.Equal("integer", valueTaskMethod.ReturnType);
    }

    [Fact]
    public void ScanHubs_ShouldGenerateCorrectHubPath()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var scanner = new SignalRHubScanner();

        // Act
        var result = scanner.ScanHubs(serviceProvider);

        // Assert
        var chatHub = result.Hubs.FirstOrDefault(h => h.Name == "ChatHub");
        if (chatHub != null)
        {
            Assert.Equal("/chat", chatHub.Path);
        }
        
        var notificationHub = result.Hubs.FirstOrDefault(h => h.Name == "NotificationHub");
        if (notificationHub != null)
        {
            Assert.Equal("/notification", notificationHub.Path);
        }
    }
}

// Test models for SignalR
public class HubUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class ChatMessage
{
    public string User { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

// Test hubs
public class TestHub : Hub
{
    public async Task SendMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", message);
    }

    public static async Task<HubUser> GetUser(int id)
    {
        await Task.CompletedTask;
        return new HubUser { Id = id, Name = "Test User", Email = "test@example.com" };
    }

    public static async Task<HubUser> GetUserAsync(int id)
    {
        await Task.Delay(10);
        return new HubUser { Id = id, Name = "Test User", Email = "test@example.com" };
    }

    public async Task UpdateUser(HubUser user)
    {
        await Clients.All.SendAsync("UserUpdated", user);
    }

    [Authorize(Roles = "Admin")]
    public async Task AdminOnlyMethod()
    {
        await Clients.All.SendAsync("AdminAction");
    }

    [Obsolete("Use SendMessage instead")]
    public async Task OldMethod(string data)
    {
        await Clients.All.SendAsync("OldData", data);
    }

    public async Task MethodWithDefaults(string required, string optional = "default")
    {
        await Clients.All.SendAsync("Data", required, optional);
    }

    public static ValueTask<int> GetCountValueTask()
    {
        return new ValueTask<int>(42);
    }

    public async Task JoinRoom(string roomName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, roomName);
    }

    public async Task SendToRoom(string roomName, string message)
    {
        await Clients.Group(roomName).SendAsync("ReceiveMessage", message);
    }
}

[Authorize(Roles = "Admin", Policy = "RequireAdminPolicy")]
public class AuthorizedTestHub : Hub
{
    public async Task SendSecureMessage(string message)
    {
        await Clients.All.SendAsync("SecureMessage", message);
    }

    public static Task<string> GetSecureData()
    {
        return Task.FromResult("Secure data");
    }
}

[Obsolete("This hub is deprecated, use NewTestHub instead")]
public class ObsoleteTestHub : Hub
{
    public async Task DoSomething()
    {
        await Clients.All.SendAsync("Something");
    }
}

public class EmptyTestHub : Hub
{
    // No public methods - should not be included in scan
}

public class ChatHub : Hub
{
    public async Task SendChatMessage(ChatMessage message)
    {
        await Clients.All.SendAsync("ReceiveChatMessage", message);
    }
}

public class NotificationHub : Hub
{
    public async Task SendNotification(string title, string body)
    {
        await Clients.All.SendAsync("ReceiveNotification", title, body);
    }
}
