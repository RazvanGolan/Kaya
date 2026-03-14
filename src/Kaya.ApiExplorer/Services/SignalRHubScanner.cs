using System.Reflection;
using Kaya.ApiExplorer.Helpers;
using Kaya.ApiExplorer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.ApiExplorer.Services;

public interface ISignalRHubScanner
{
    SignalRDocumentation ScanHubs(IServiceProvider serviceProvider);
}

public class SignalRHubScanner : ISignalRHubScanner
{
    public SignalRDocumentation ScanHubs(IServiceProvider serviceProvider)
    {
        var documentation = new SignalRDocumentation
        {
            Title = "SignalR Hubs",
            Version = "1.0.0",
            Description = "Available SignalR hubs and their methods"
        };

        // Get the actual hub routes from endpoint routing
        var hubRoutes = GetHubRoutes(serviceProvider);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !ReflectionHelper.IsSystemAssembly(a));

        foreach (var assembly in assemblies)
        {
            try
            {
                var hubTypes = assembly.GetTypes()
                    .Where(t => IsHubType(t) && !t.IsAbstract);

                foreach (var hubType in hubTypes)
                {
                    var hub = ScanHub(hubType, hubRoutes);
                    if (hub is not null)
                    {
                        documentation.Hubs.Add(hub);
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Skip assemblies that fail to load
            }
        }

        return documentation;
    }
    
    private static Dictionary<Type, string> GetHubRoutes(IServiceProvider serviceProvider)
    {
        var hubRoutes = new Dictionary<Type, string>();
        
        try
        {
            // Try to get the endpoint data source from the service provider
            var endpointDataSources = serviceProvider.GetServices<EndpointDataSource>();

            foreach (var dataSource in endpointDataSources)
            {
                foreach (var endpoint in dataSource.Endpoints)
                {
                    // Check if this is a hub endpoint
                    var hubMetadata = endpoint.Metadata.GetMetadata<HubMetadata>();
                    if (hubMetadata is null) 
                        continue;
                    
                    if (endpoint is RouteEndpoint routeEndpoint)
                    {
                        hubRoutes[hubMetadata.HubType] = routeEndpoint.RoutePattern.RawText ?? GetHubPathFromType(hubMetadata.HubType);
                    }
                }
            }
        }
        catch
        {
            // If we can't get the routes, we'll fall back to the default naming
        }

        return hubRoutes;
    }

    private static bool IsHubType(Type type)
    {
        var currentType = type.BaseType;
        while (currentType is not null)
        {
            if (currentType.IsGenericType && 
                currentType.GetGenericTypeDefinition().Name is "Hub`1" &&
                currentType.Namespace is "Microsoft.AspNetCore.SignalR")
            {
                return true;
            }
            if (currentType.Name is "Hub" && 
                currentType.Namespace is "Microsoft.AspNetCore.SignalR")
            {
                return true;
            }
            currentType = currentType.BaseType;
        }
        return false;
    }

    private static SignalRHub? ScanHub(Type hubType, Dictionary<Type, string> hubRoutes)
    {
        var (requiresAuth, roles) = AttributeHelper.GetAuthorizationInfo(hubType);
        var (isObsolete, obsoleteMessage) = AttributeHelper.GetObsoleteInfo(hubType);

        // Extract policies from Authorize attributes
        var policies = new List<string>();
        var authorizeAttrs = hubType.GetCustomAttributes<AuthorizeAttribute>();
        foreach (var attr in authorizeAttrs)
        {
            if (!string.IsNullOrEmpty(attr.Policy))
            {
                policies.Add(attr.Policy);
            }
        }

        // Get the actual route from the registered endpoints, or fall back to convention
        var hubPath = hubRoutes.TryGetValue(hubType, out var registeredPath) 
            ? registeredPath 
            : GetHubPathFromType(hubType);

        var hub = new SignalRHub
        {
            Name = hubType.Name,
            Namespace = hubType.Namespace ?? string.Empty,
            Path = hubPath,
            Description = GetHubDescription(hubType),
            RequiresAuthorization = requiresAuth,
            Roles = roles,
            Policies = [.. policies.Distinct()],
            IsObsolete = isObsolete,
            ObsoleteMessage = obsoleteMessage
        };

        var methods = hubType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.DeclaringType == hubType && 
                       !m.IsSpecialName && 
                       !IsInheritedHubMethod(m));

        foreach (var method in methods)
        {
            var hubMethod = ScanHubMethod(method, hubType);
            hub.Methods.Add(hubMethod);
        }

        return hub.Methods.Count > 0 ? hub : null;
    }

    private static bool IsInheritedHubMethod(MethodInfo method)
    {
        // Exclude methods inherited from Hub base class
        var inheritedMethods = new[]
        {
            "OnConnectedAsync",
            "OnDisconnectedAsync",
            "Dispose",
            "Equals",
            "GetHashCode",
            "GetType",
            "ToString"
        };
        
        return inheritedMethods.Contains(method.Name);
    }

    private static SignalRMethod ScanHubMethod(MethodInfo method, Type hubType)
    {
        var (requiresAuth, roles) = AttributeHelper.GetAuthorizationInfo(method, hubType);
        var (isObsolete, obsoleteMessage) = AttributeHelper.GetObsoleteInfo(method);

        // Extract policies
        var policies = new List<string>();
        var authorizeAttrs = method.GetCustomAttributes<AuthorizeAttribute>();
        foreach (var attr in authorizeAttrs)
        {
            if (!string.IsNullOrEmpty(attr.Policy))
            {
                policies.Add(attr.Policy);
            }
        }

        var returnType = method.ReturnType;
        var returnTypeName = "void";
        string? returnExample = null;

        if (returnType != typeof(void) && returnType != typeof(Task))
        {
            if (returnType.IsGenericType)
            {
                var genericTypeDefinition = returnType.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Task<>) || 
                    genericTypeDefinition == typeof(ValueTask<>))
                {
                    var actualReturnType = returnType.GetGenericArguments().FirstOrDefault();
                    if (actualReturnType is not null)
                    {
                        returnTypeName = ReflectionHelper.GetFriendlyTypeName(actualReturnType);
                        var schemas = new Dictionary<string, ApiSchema>();
                        var processedTypes = new HashSet<Type>();
                        returnExample = ReflectionHelper.GenerateExampleJson(actualReturnType, schemas, processedTypes);
                    }
                }
            }
            else
            {
                returnTypeName = ReflectionHelper.GetFriendlyTypeName(returnType);
                var schemas = new Dictionary<string, ApiSchema>();
                var processedTypes = new HashSet<Type>();
                returnExample = ReflectionHelper.GenerateExampleJson(returnType, schemas, processedTypes);
            }
        }

        var hubMethod = new SignalRMethod
        {
            Name = method.Name,
            Description = GetMethodDescription(method),
            ReturnType = returnTypeName,
            ReturnTypeExample = returnExample,
            RequiresAuthorization = requiresAuth,
            Roles = roles,
            Policies = [.. policies.Distinct()],
            IsObsolete = isObsolete,
            ObsoleteMessage = obsoleteMessage
        };

        foreach (var param in method.GetParameters())
        {
            var parameter = ScanParameter(param);
            hubMethod.Parameters.Add(parameter);
        }

        return hubMethod;
    }

    private static SignalRParameter ScanParameter(ParameterInfo param)
    {
        var paramType = param.ParameterType;
        var typeName = ReflectionHelper.GetFriendlyTypeName(paramType);
        
        var schemas = new Dictionary<string, ApiSchema>();
        var processedTypes = new HashSet<Type>();
        var example = ReflectionHelper.GenerateExampleJson(paramType, schemas, processedTypes);

        var parameter = new SignalRParameter
        {
            Name = param.Name ?? "unknown",
            Type = typeName,
            Required = !param.HasDefaultValue && 
                      paramType.IsValueType && Nullable.GetUnderlyingType(paramType) is null,
            DefaultValue = param.HasDefaultValue ? param.DefaultValue : null,
            Example = example
        };

        if (ReflectionHelper.IsComplexType(paramType))
        {
            parameter.Schema = ReflectionHelper.GenerateSchemaForType(paramType);
        }

        return parameter;
    }

    private static string GetHubPathFromType(Type hubType)
    {
        // Fallback: SignalR hubs typically use the hub name without "Hub" suffix
        var hubName = hubType.Name;
        if (hubName.EndsWith("Hub"))
        {
            hubName = hubName[..^3]; // Remove "Hub" suffix
        }
        
        // Convert to camelCase for the path
        return $"/{char.ToLowerInvariant(hubName[0])}{hubName[1..]}";
    }

    private static string GetHubDescription(Type hubType)
    {
        var xmlSummary = XmlDocumentationHelper.GetTypeSummary(hubType);
        if (!string.IsNullOrWhiteSpace(xmlSummary))
        {
            return xmlSummary;
        }

        var hubName = hubType.Name;
        if (hubName.EndsWith("Hub"))
        {
            hubName = hubName[..^3];
        }
        
        return $"{hubName} SignalR hub for real-time communication";
    }

    private static string GetMethodDescription(MethodInfo method)
    {
        var xmlSummary = XmlDocumentationHelper.GetMethodSummary(method);
        if (!string.IsNullOrWhiteSpace(xmlSummary))
        {
            return xmlSummary;
        }

        return $"Invoke {method.Name} method on hub";
    }
}
