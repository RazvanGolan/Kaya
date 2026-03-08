using System.Reflection;
using System.Text.RegularExpressions;
using Kaya.ApiExplorer.Configuration;
using Kaya.ApiExplorer.Helpers;
using Kaya.ApiExplorer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Kaya.ApiExplorer.Services;

public interface IEndpointScanner
{
    ApiDocumentation ScanEndpoints(IServiceProvider serviceProvider);
}

public class EndpointScanner(KayaApiExplorerOptions options) : IEndpointScanner
{
    public ApiDocumentation ScanEndpoints(IServiceProvider serviceProvider)
    {
        var doc = options.Documentation;
        var documentation = new ApiDocumentation
        {
            Title = string.IsNullOrWhiteSpace(doc.Title) ? "API Documentation" : doc.Title,
            Version = string.IsNullOrWhiteSpace(doc.Version) ? "1.0.0" : doc.Version,
            Description = doc.Description,
            TermsOfService = doc.TermsOfService,
            Contact = doc.Contact is null ? null : new ApiDocumentationContact
            {
                Name = doc.Contact.Name,
                Email = doc.Contact.Email,
                Url = doc.Contact.Url
            },
            License = doc.License is null ? null : new ApiDocumentationLicense
            {
                Name = doc.License.Name,
                Url = doc.License.Url
            },
            Servers = [.. doc.Servers.Select(s => new ApiDocumentationServer
            {
                Url = s.Url,
                Description = s.Description
            })]
        };

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !ReflectionHelper.IsSystemAssembly(a)).ToList();

        var controllerGroups = new Dictionary<string, List<ApiEndpoint>>();

        foreach (var assembly in assemblies)
        {
            var controllerTypes = assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract);

            foreach (var controllerType in controllerTypes)
            {
                var endpoints = ScanController(controllerType);
                if (endpoints.Count <= 0) 
                    continue;
                
                var controllerName = controllerType.Name;
                if (!controllerGroups.TryGetValue(controllerName, out _))
                {
                    controllerGroups[controllerName] = [];
                }
                
                controllerGroups[controllerName].AddRange(endpoints);
            }
        }

        foreach (var group in controllerGroups)
        {
            var controllerType = assemblies
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == group.Key);
            
            var (requiresAuth, roles) = AttributeHelper.GetAuthorizationInfo(controllerType);
            var (isObsolete, obsoleteMessage) = AttributeHelper.GetObsoleteInfo(controllerType);
            
            var controller = new ApiController
            {
                Name = group.Key,
                Description = controllerType is not null ? GetControllerDescription(controllerType) : GetControllerDescription(group.Key),
                Endpoints = group.Value,
                RequiresAuthorization = requiresAuth,
                Roles = roles,
                IsObsolete = isObsolete,
                ObsoleteMessage = obsoleteMessage
            };
            documentation.Controllers.Add(controller);
        }

        // Scan Minimal API endpoints
        foreach (var minimalController in ScanMinimalApiEndpoints(serviceProvider))
        {
            documentation.Controllers.Add(minimalController);
        }

        return documentation;
    }

    private static List<ApiController> ScanMinimalApiEndpoints(IServiceProvider serviceProvider)
    {
        var endpointSources = serviceProvider.GetServices<EndpointDataSource>();
        var groups = new Dictionary<string, List<ApiEndpoint>>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in endpointSources)
        {
            foreach (var endpoint in source.Endpoints)
            {
                if (endpoint is not RouteEndpoint routeEndpoint)
                    continue;

                // Skip controller / Razor Pages action endpoints
                if (routeEndpoint.Metadata.GetMetadata<ActionDescriptor>() is not null)
                    continue;

                // Skip SignalR hub endpoints
                if (routeEndpoint.Metadata.Any(m => m.GetType().Name is "HubMetadata" or "NegotiateMetadata"))
                    continue;

                var httpMethodMeta = routeEndpoint.Metadata.GetMetadata<HttpMethodMetadata>();
                if (httpMethodMeta is null || httpMethodMeta.HttpMethods.Count is 0)
                    continue;

                var rawText = routeEndpoint.RoutePattern.RawText?.TrimStart('/') ?? string.Empty;
                // Strip route constraints: {id:int} → {id}
                var cleanText = Regex.Replace(rawText, @"\{(\w+):[^}]+\}", "{$1}");
                var pattern = "/" + cleanText;

                // Group by first tag, or derive from route prefix
                var tagsMetadata = routeEndpoint.Metadata.GetMetadata<ITagsMetadata>();
                var groupName = tagsMetadata?.Tags?.FirstOrDefault() ?? GetMinimalApiGroupFromPath(pattern);

                // Summary / description
                var summaryAttr = routeEndpoint.Metadata.GetMetadata<EndpointSummaryAttribute>();
                var description = summaryAttr?.Summary ?? routeEndpoint.DisplayName ?? pattern;

                // Authorization
                var allowAnon = routeEndpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
                var authorizeData = routeEndpoint.Metadata.GetMetadata<IAuthorizeData>();
                var requiresAuth = !allowAnon && authorizeData is not null;
                var roles = authorizeData?.Roles
                    ?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList() ?? [];

                // Obsolete
                var obsolete = routeEndpoint.Metadata.GetMetadata<ObsoleteAttribute>();

                // Endpoint name (set via .WithName(...))
                var nameMetadata = routeEndpoint.Metadata.GetMetadata<EndpointNameMetadata>();
                var endpointName = nameMetadata?.EndpointName ?? pattern;

                var parameters = BuildMinimalApiParameters(routeEndpoint);
                var requestBody = BuildMinimalApiRequestBody(routeEndpoint, description);

                foreach (var httpMethod in httpMethodMeta.HttpMethods)
                {
                    var apiEndpoint = new ApiEndpoint
                    {
                        Path = pattern,
                        HttpMethodType = httpMethod,
                        MethodName = endpointName,
                        Description = description,
                        Parameters = parameters,
                        RequestBody = requestBody,
                        RequiresAuthorization = requiresAuth,
                        Roles = roles,
                        IsObsolete = obsolete is not null,
                        ObsoleteMessage = obsolete?.Message
                    };

                    if (!groups.TryGetValue(groupName, out var list))
                    {
                        list = [];
                        groups[groupName] = list;
                    }
                    list.Add(apiEndpoint);
                }
            }
        }

        return groups.Select(g => new ApiController
        {
            Name = g.Key,
            Description = $"{g.Key} endpoints (Minimal API)",
            Endpoints = g.Value
        }).ToList();
    }

    private static string GetMinimalApiGroupFromPath(string path)
    {
        var segments = path.TrimStart('/').Split('/');
        var first = segments.FirstOrDefault(s => !string.IsNullOrEmpty(s) && !s.StartsWith('{'));
        if (first is null) return "MinimalApi";
        return char.ToUpper(first[0]) + first[1..].ToLower();
    }

    private static ApiRequestBody? BuildMinimalApiRequestBody(RouteEndpoint endpoint, string endpointDescription)
    {
        var acceptsMeta = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();
        if (acceptsMeta?.RequestType is null) return null;

        var bodyType = acceptsMeta.RequestType;
        // Unwrap nullable wrapper if present (e.g. CreateTodoRequest? → CreateTodoRequest)
        var underlyingType = Nullable.GetUnderlyingType(bodyType) ?? bodyType;

        var typeName = ReflectionHelper.GetFriendlyTypeName(underlyingType);
        var schemas = new Dictionary<string, ApiSchema>();
        var processedTypes = new HashSet<Type>();
        var example = ReflectionHelper.GenerateExampleJson(underlyingType, schemas, processedTypes);

        // Use .WithDescription() metadata if set, otherwise derive from the endpoint summary or type name
        var descAttr = endpoint.Metadata.GetMetadata<EndpointDescriptionAttribute>();
        var bodyDescription = descAttr?.Description
            ?? (!string.IsNullOrWhiteSpace(endpointDescription) ? endpointDescription : $"Request body of type {typeName}");

        var schemaType = UnwrapCollectionType(underlyingType);

        return new ApiRequestBody
        {
            Type = typeName,
            Description = bodyDescription,
            Example = example,
            Schema = ReflectionHelper.GenerateSchemaForType(schemaType)
        };
    }

    private static List<ApiParameter> BuildMinimalApiParameters(RouteEndpoint endpoint)
    {
        var routeParamNames = endpoint.RoutePattern.Parameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bodyType = endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType;
        var bodyUnderlying = bodyType is not null ? (Nullable.GetUnderlyingType(bodyType) ?? bodyType) : null;

        var parameters = new List<ApiParameter>();

        // Route parameters
        foreach (var routeParam in endpoint.RoutePattern.Parameters)
        {
            parameters.Add(new ApiParameter
            {
                Name = routeParam.Name,
                Type = ResolveRouteParamType(routeParam),
                Source = "Route",
                Required = !routeParam.IsOptional
            });
        }

        // Query parameters — discovered via IParameterBindingMetadata (ASP.NET Core 8+)
        var bindingMetadata = endpoint.Metadata.GetOrderedMetadata<IParameterBindingMetadata>();
        foreach (var meta in bindingMetadata)
        {
            // Skip route parameters (already added above)
            if (routeParamNames.Contains(meta.Name)) continue;

            var paramType = meta.ParameterInfo?.ParameterType;
            if (paramType is null) continue;

            var underlyingType = Nullable.GetUnderlyingType(paramType) ?? paramType;

            // Skip the body type (handled separately as RequestBody)
            if (bodyUnderlying is not null && (underlyingType == bodyUnderlying || paramType == bodyType)) continue;

            // Skip ASP.NET Core / BCL injected types (HttpContext, CancellationToken, etc.)
            if (IsFrameworkType(underlyingType)) continue;

            // Complex types that are not the body type are DI services — skip them
            if (ReflectionHelper.IsComplexType(underlyingType)) continue;

            parameters.Add(new ApiParameter
            {
                Name = meta.Name,
                Type = ReflectionHelper.GetFriendlyTypeName(underlyingType),
                Source = "Query",
                Required = !meta.IsOptional
            });
        }

        return parameters;
    }

    private static bool IsFrameworkType(Type type)
    {
        var fullName = type.FullName ?? string.Empty;
        return type == typeof(CancellationToken)
            || fullName.StartsWith("Microsoft.AspNetCore.Http")
            || fullName.StartsWith("System.IO.Stream")
            || fullName.StartsWith("System.IO.Pipelines");
    }

    private static string ResolveRouteParamType(RoutePatternParameterPart param)
    {
        var policies = param.ParameterPolicies.Select(p => p.Content).ToList();
        if (policies.Any(p => p is "int" or "long" or "short" or "byte")) return "integer";
        if (policies.Any(p => p is "guid")) return "guid";
        if (policies.Any(p => p is "bool")) return "boolean";
        if (policies.Any(p => p is "decimal" or "double" or "float")) return "decimal";
        return "string";
    }

    private static List<ApiEndpoint> ScanController(Type controllerType)
    {
        var endpoints = new List<ApiEndpoint>();
        var controllerName = controllerType.Name.Replace("Controller", "");
        var routeAttribute = controllerType.GetCustomAttribute<RouteAttribute>();
        var controllerRoute = routeAttribute?.Template ?? "api/[controller]";
        
        controllerRoute = controllerRoute.Replace("[controller]", controllerName);

        var methods = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.IsPublic && !m.IsSpecialName && m.DeclaringType == controllerType);

        foreach (var method in methods)
        {
            var httpAttributes = GetHttpMethodAttributes(method);
            
            foreach (var httpAttr in httpAttributes)
            {
                foreach (var httpMethod in httpAttr.HttpMethods)
                {
                    var methodRoute = httpAttr.Template ?? string.Empty;
                    var fullPath = ReflectionHelper.CombineRoutes(controllerRoute, methodRoute);
                    
                    var (requiresAuth, roles) = AttributeHelper.GetAuthorizationInfo(method, controllerType);
                    var (isObsolete, obsoleteMessage) = AttributeHelper.GetObsoleteInfo(method);
                    
                    var endpoint = new ApiEndpoint
                    {
                        Path = fullPath,
                        HttpMethodType = httpMethod,
                        MethodName = method.Name,
                        Description = GetMethodDescription(method),
                        Parameters = GetMethodParameters(method, fullPath),
                        RequestBody = GetMethodRequestBody(method),
                        Response = GetMethodResponse(method),
                        RequiresAuthorization = requiresAuth,
                        Roles = roles,
                        IsObsolete = isObsolete,
                        ObsoleteMessage = obsoleteMessage
                    };

                    endpoints.Add(endpoint);
                }
            }
        }

        return endpoints;
    }

    private static string GetControllerDescription(string controllerName)
    {
        return $"{controllerName.Replace("Controller", "")} management";
    }

    private static string GetControllerDescription(Type controllerType)
    {
        var xmlSummary = XmlDocumentationHelper.GetTypeSummary(controllerType);
        if (!string.IsNullOrWhiteSpace(xmlSummary))
        {
            return xmlSummary;
        }

        var controllerName = controllerType.Name;
        return controllerName switch
        {
            "UsersController" => "Manage user accounts and profiles",
            "ProductsController" => "Product catalog management", 
            "OrdersController" => "Order processing and management",
            _ => $"{controllerName.Replace("Controller", "")} management"
        };
    }

    private static ApiRequestBody? GetMethodRequestBody(MethodInfo method)
    {
        var bodyParam = method.GetParameters()
            .FirstOrDefault(p => {
                if (IsFileParameter(p.ParameterType)) return false;
                
                return p.GetCustomAttribute<FromBodyAttribute>() is not null ||
                       (!p.ParameterType.IsPrimitive && 
                        p.ParameterType != typeof(string) && 
                        p.ParameterType != typeof(DateTime) && 
                        p.ParameterType != typeof(Guid) &&
                        p.GetCustomAttribute<FromQueryAttribute>() is null &&
                        p.GetCustomAttribute<FromRouteAttribute>() is null &&
                        p.GetCustomAttribute<FromHeaderAttribute>() is null &&
                        p.GetCustomAttribute<FromFormAttribute>() is null);
            });

        if (bodyParam is null) return null;

        var typeName = ReflectionHelper.GetFriendlyTypeName(bodyParam.ParameterType);
        var schemas = new Dictionary<string, ApiSchema>();
        var processedTypes = new HashSet<Type>();
        var example = ReflectionHelper.GenerateExampleJson(bodyParam.ParameterType, schemas, processedTypes);

        // Unwrap collection to element type for schema, so List<User> gives us the User schema
        var schemaType = UnwrapCollectionType(bodyParam.ParameterType);

        var bodyDescription = XmlDocumentationHelper.GetParameterDescription(method, bodyParam.Name ?? string.Empty)
            ?? $"Request body containing {bodyParam.Name} data";

        return new ApiRequestBody
        {
            Type = typeName,
            Description = bodyDescription,
            Example = example,
            Schema = ReflectionHelper.GenerateSchemaForType(schemaType)
        };
    }

    private static ApiResponse? GetMethodResponse(MethodInfo method)
    {
        var returnType = method.ReturnType;
        
        if (returnType.IsGenericType)
        {
            var genericTypeDefinition = returnType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(Task<>) || 
                genericTypeDefinition == typeof(ValueTask<>))
            {
                returnType = returnType.GetGenericArguments().FirstOrDefault() ?? typeof(void);
            }
            else if (returnType == typeof(Task) || returnType == typeof(ValueTask))
            {
                returnType = typeof(void);
            }
        }

        if (returnType == typeof(void))
        {
            return null;
        }

        var actualReturnType = returnType;
        if (returnType.Name.Contains("ActionResult") || returnType.Name.Contains("IActionResult"))
        {
            if (returnType.IsGenericType)
            {
                var genericArg = returnType.GetGenericArguments().FirstOrDefault();
                if (genericArg is not null)
                {
                    actualReturnType = genericArg;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        var typeName = ReflectionHelper.GetFriendlyTypeName(actualReturnType);
        var schemas = new Dictionary<string, ApiSchema>();
        var processedTypes = new HashSet<Type>();
        var example = ReflectionHelper.GenerateExampleJson(actualReturnType, schemas, processedTypes);

        // Unwrap collection to element type for schema, so List<User> gives us the User schema
        var schemaType = UnwrapCollectionType(actualReturnType);

        return new ApiResponse
        {
            Type = typeName,
            Example = example,
            Description = XmlDocumentationHelper.GetReturnsDescription(method) ?? string.Empty,
            Schema = ReflectionHelper.GenerateSchemaForType(schemaType)
        };
    }

    private static Type UnwrapCollectionType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType() ?? type;
        if (type.IsGenericType && ReflectionHelper.IsEnumerableType(type))
        {
            var elementType = type.GetGenericArguments().FirstOrDefault();
            if (elementType is not null)
                return elementType;
        }
        return type;
    }
    
    private static List<HttpMethodAttribute> GetHttpMethodAttributes(MethodInfo method)
    {
        var httpAttributes = new List<HttpMethodAttribute>();
        
        var allHttpAttrs = method.GetCustomAttributes()
            .Where(attr => typeof(HttpMethodAttribute).IsAssignableFrom(attr.GetType()))
            .Cast<HttpMethodAttribute>();
            
        httpAttributes.AddRange(allHttpAttrs);
        
        if (httpAttributes.Count is 0)
        {
            var routeAttr = method.GetCustomAttribute<RouteAttribute>();
            if (routeAttr is not null)
            {
                httpAttributes.Add(new HttpGetAttribute(routeAttr.Template));
            }
            else
            {
                httpAttributes.Add(new HttpGetAttribute(method.Name.ToLower()));
            }
        }
        
        return httpAttributes;
    }
    
    private static string GetMethodDescription(MethodInfo method)
    {
        var xmlSummary = XmlDocumentationHelper.GetMethodSummary(method);
        return !string.IsNullOrWhiteSpace(xmlSummary) ? xmlSummary : $"{method.Name} action in {method.DeclaringType?.Name}";
    }

    private static bool IsFileParameter(Type parameterType)
    {
        // Check for IFormFile by fully qualified name to avoid dependency
        if (parameterType.FullName is "Microsoft.AspNetCore.Http.IFormFile")
            return true;

        // Check for IFormFileCollection
        if (parameterType.FullName is "Microsoft.AspNetCore.Http.IFormFileCollection")
            return true;

        // Check if type implements IFormFile interface
        var iFormFileType = parameterType.GetInterfaces()
            .FirstOrDefault(i => i.FullName is "Microsoft.AspNetCore.Http.IFormFile");
        if (iFormFileType is not null)
            return true;

        // Check for collections/arrays of IFormFile (List<IFormFile>, IEnumerable<IFormFile>, etc.)
        if (parameterType.IsGenericType)
        {
            var genericArgs = parameterType.GetGenericArguments();
            if (genericArgs.Any(t => IsFileParameter(t)))
                return true;
        }

        // Check for array of IFormFile
        if (parameterType.IsArray)
        {
            var elementType = parameterType.GetElementType();
            if (elementType is not null && IsFileParameter(elementType))
                return true;
        }

        return false;
    }

    private static List<ApiParameter> GetMethodParameters(MethodInfo method, string routePath)
    {
        var parameters = new List<ApiParameter>();

        foreach (var param in method.GetParameters())
        {
            var isFileParameter = IsFileParameter(param.ParameterType);
            var parameterSource = isFileParameter ? "File" : DetermineParameterSource(param, routePath);
            var typeName = ReflectionHelper.GetFriendlyTypeName(param.ParameterType);
            
            // Get the actual header name if specified in FromHeader attribute
            string? headerName = null;
            if (parameterSource is "Header")
            {
                var fromHeaderAttr = param.GetCustomAttribute<FromHeaderAttribute>();
                headerName = fromHeaderAttr?.Name;
            }
            
            var underlyingParamType = Nullable.GetUnderlyingType(param.ParameterType) ?? param.ParameterType;
            var apiParam = new ApiParameter
            {
                Name = param.Name ?? "unknown",
                Type = typeName,
                Required = param is { HasDefaultValue: false, ParameterType.IsValueType: false } ||
                          (param.ParameterType.IsValueType && Nullable.GetUnderlyingType(param.ParameterType) is null),
                DefaultValue = param.HasDefaultValue ? param.DefaultValue : null,
                Source = parameterSource,
                IsFile = isFileParameter,
                IsEnum = underlyingParamType.IsEnum,
                HeaderName = headerName,
                Description = XmlDocumentationHelper.GetParameterDescription(method, param.Name ?? "unknown") ?? string.Empty,
                Constraints = ReflectionHelper.GetParameterConstraints(param)
            };

            if (!isFileParameter && ReflectionHelper.IsComplexType(param.ParameterType))
            {
                apiParam.Schema = ReflectionHelper.GenerateSchemaForType(param.ParameterType);
            }

            parameters.Add(apiParam);
        }

        return parameters;
    }

    private static string DetermineParameterSource(ParameterInfo param, string routePath)
    {
        var fromBodyAttr = param.GetCustomAttribute<FromBodyAttribute>();
        if (fromBodyAttr is not null) return "Body";

        var fromQueryAttr = param.GetCustomAttribute<FromQueryAttribute>();
        if (fromQueryAttr is not null) return "Query";

        var fromRouteAttr = param.GetCustomAttribute<FromRouteAttribute>();
        if (fromRouteAttr is not null) return "Route";

        var fromHeaderAttr = param.GetCustomAttribute<FromHeaderAttribute>();
        if (fromHeaderAttr is not null) return "Header";

        var fromFormAttr = param.GetCustomAttribute<FromFormAttribute>();
        if (fromFormAttr is not null) return "Form";

        if (!string.IsNullOrEmpty(param.Name) && routePath.Contains($"{{{param.Name}}}"))
        {
            return "Route";
        }

        // Default logic based on type
        if (param.ParameterType.IsPrimitive || param.ParameterType == typeof(string) || 
            param.ParameterType == typeof(DateTime) || param.ParameterType == typeof(Guid))
        {
            return "Query";
        }

        return "Body";
    }
}
