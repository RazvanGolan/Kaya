using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Kaya.ApiExplorer.Models;

namespace Kaya.ApiExplorer.Helpers;

public static class ReflectionHelper
{
    public static bool IsSystemAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name ?? "";
        return name.StartsWith("System.") || 
               name.StartsWith("Microsoft.") ||
               name.StartsWith("netstandard") ||
               name == "mscorlib";
    }

    public static string GetFriendlyTypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "integer";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(DateTime)) return "datetime";
        if (type == typeof(Guid)) return "guid";
        if (type == typeof(object)) return "object";
        if (type == typeof(byte[])) return "byte";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        
        var nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType != null)
        {
            return GetFriendlyTypeName(nullableType) + "?";
        }
        
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            var typeName = genericTypeDefinition.Name;
            
            // Remove the generic arity suffix (e.g., `1, `2, etc.)
            var backtickIndex = typeName.IndexOf('`');
            if (backtickIndex >= 0)
            {
                typeName = typeName[..backtickIndex];
            }
            
            // Handle Dictionary types specifically
            if (genericTypeDefinition == typeof(Dictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                return $"Dictionary<{GetFriendlyTypeName(keyType)}, {GetFriendlyTypeName(valueType)}>";
            }
            
            if (IsEnumerableType(type))
            {
                var elementType = type.GetGenericArguments().FirstOrDefault();
                return elementType != null
                    ? $"{GetFriendlyTypeName(elementType)}[]"
                    : "object[]";
            }
            
            var genericArgs = type.GetGenericArguments();
            var argNames = genericArgs.Select(GetFriendlyTypeName);
            return $"{typeName}<{string.Join(", ", argNames)}>";
        }

        return type.Name;
    }

    public static bool IsComplexType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            return false;
        }
        
        return !type.IsPrimitive && 
               type != typeof(string) && 
               type != typeof(DateTime) && 
               type != typeof(Guid) && 
               type != typeof(decimal) && 
               type != typeof(double) && 
               type != typeof(float) &&
               !type.IsEnum &&
               Nullable.GetUnderlyingType(type) == null;
    }

    public static bool IsEnumerableType(Type type)
    {
        if (type.IsArray) return true;
        
        if (type.IsGenericType)
        {
            var genericTypeDefinition = type.GetGenericTypeDefinition();
            return genericTypeDefinition == typeof(List<>) || 
                   genericTypeDefinition == typeof(IEnumerable<>) ||
                   genericTypeDefinition == typeof(ICollection<>) ||
                   genericTypeDefinition == typeof(IList<>) ||
                   genericTypeDefinition == typeof(IReadOnlyCollection<>) ||
                   genericTypeDefinition == typeof(IReadOnlyList<>);
        }
        
        return typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
    }

    private static bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    private static bool IsWholeNumberType(Type type)
    {
        return type == typeof(short)
               || type == typeof(ushort)
               || type == typeof(int)
               || type == typeof(uint)
               || type == typeof(long)
               || type == typeof(ulong);
    }

    private static bool IsByteLikeType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte);
    }

    public static string GenerateExampleJson(Type type, Dictionary<string, ApiSchema> schemas, HashSet<Type> processedTypes)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string)) return "\"string value\"";
        if (IsWholeNumberType(underlyingType)) return "123";
        if (underlyingType == typeof(bool)) return "true";
        if (underlyingType == typeof(DateTime)) return "\"2023-07-13T10:30:00Z\"";
        if (underlyingType == typeof(Guid)) return "\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"";
        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float)) return "12.34";
        if (IsByteLikeType(underlyingType)) return "0";
        // TODO: find a better example for enum types
        if (underlyingType.IsEnum)
            return "0";

        // Handle Dictionary types specifically
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];

            var keyExample = keyType == typeof(string) ? "\"key1\"" : GenerateExampleJson(keyType, schemas, processedTypes);
            var valueExample = GenerateExampleJson(valueType, schemas, processedTypes);

            var jsonKey = keyExample.StartsWith('"') ? keyExample : $"\"{keyExample.Trim('"')}\"";

            return $"{{{jsonKey}: {valueExample}}}";
        }

        if (IsEnumerableType(type))
        {
            var elementType = type.GetGenericArguments().FirstOrDefault();
            if (elementType != null)
            {
                var elementExample = GenerateExampleJson(elementType, schemas, processedTypes);
                return $"[{elementExample}]";
            }
        }
        
        if (IsComplexType(type))
        {
            GenerateSchemaForType(type, schemas, processedTypes);
            return GenerateExampleFromSchema(type, schemas);
        }

        return "{}";
    }

    public static ApiSchema? GenerateSchemaForType(Type type)
    {
        var schemas = new Dictionary<string, ApiSchema>();
        var processedTypes = new HashSet<Type>();
        
        GenerateSchemaForType(type, schemas, processedTypes);
        
        var typeName = GetFriendlyTypeName(type);
        // Fall back to type.Name so closed-generic types are found correctly.
        return schemas.GetValueOrDefault(typeName)
            ?? schemas.GetValueOrDefault(type.Name);
    }

    private static void GenerateSchemaForType(Type type, Dictionary<string, ApiSchema> schemas, HashSet<Type> processedTypes)
    {
        if (processedTypes.Contains(type) || schemas.ContainsKey(type.Name))
            return;

        processedTypes.Add(type);

        var schema = new ApiSchema
        {
            Type = "object"
        };

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true });

        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            var isRequired = !IsNullableType(propertyType)
                || property.GetCustomAttribute<RequiredAttribute>() is not null;
            
            // Add property to schema
            var apiProperty = new ApiProperty
            {
                Type = GetFriendlyTypeName(underlyingType),
                Required = isRequired,
                DefaultValue = property.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>()?.Value,
                Constraints = GetPropertyConstraints(property)
            };
            
            if (IsComplexType(underlyingType) && !processedTypes.Contains(underlyingType))
            {
                GenerateSchemaForType(underlyingType, schemas, processedTypes);
                var nestedTypeName = GetFriendlyTypeName(underlyingType);
                if (schemas.ContainsKey(nestedTypeName))
                {
                    apiProperty.NestedSchema = schemas[nestedTypeName];
                }
            }
            
            schema.Properties[property.Name] = apiProperty;
            
            if (isRequired)
            {
                schema.Required.Add(property.Name);
            }
        }

        schema.Example = GenerateExampleFromSchema(type, schemas);
        schemas[type.Name] = schema;
    }

    private static string GenerateExampleFromSchema(Type type, Dictionary<string, ApiSchema> schemas, HashSet<Type>? processedTypes = null)
    {
        processedTypes ??= [];

        var example = new Dictionary<string, object>();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var property in properties)
        {
            var propertyType = property.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType == typeof(string))
            {
                example[property.Name] = GenerateStringExample(property.Name);
            }
            else if (IsWholeNumberType(underlyingType))
            {
                example[property.Name] = GenerateIntExample(property.Name);
            }
            else if (underlyingType == typeof(bool))
            {
                example[property.Name] = true;
            }
            else if (underlyingType == typeof(DateTime))
            {
                example[property.Name] = DateTime.UtcNow;
            }
            else if (underlyingType == typeof(Guid))
            {
                example[property.Name] = Guid.NewGuid();
            }
            else if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
            {
                example[property.Name] = GenerateDecimalExample(property.Name);
            }
            else if (IsByteLikeType(underlyingType))
            {
                example[property.Name] = 0;
            }
            else if (underlyingType == typeof(object))
            {
                example[property.Name] = "object";
            }
            else if (underlyingType.IsEnum)
            {
                var enumValues = Enum.GetValues(underlyingType);
                example[property.Name] = enumValues.GetValue(0) ?? 0;
            }
            else if (underlyingType.IsGenericType)
            {
                var genericTypeDefinition = underlyingType.GetGenericTypeDefinition();
                
                // Handle Dictionary types first
                if (genericTypeDefinition == typeof(Dictionary<,>))
                {
                    var keyType = underlyingType.GetGenericArguments()[0];
                    var valueType = underlyingType.GetGenericArguments()[1];

                    var keyString = keyType == typeof(string) ? "key1" : (GenerateSimpleExample(keyType, processedTypes).ToString() ?? "key1");
                    var valueExample = GenerateSimpleExample(valueType, processedTypes);

                    var dictionaryExample = new Dictionary<string, object>();
                    dictionaryExample[keyString] = valueExample;

                    example[property.Name] = dictionaryExample;
                }
                else if (IsEnumerableType(underlyingType))
                {
                    var elementType = underlyingType.GetGenericArguments().FirstOrDefault();
                    if (elementType != null)
                    {
                        var elementExample = GenerateSimpleExample(elementType, processedTypes);
                        example[property.Name] = new[] { elementExample };
                    }
                    else
                    {
                        example[property.Name] = new object[] { };
                    }
                }
                // Handle other generic types (like ApiResponse<T>, etc.)
                else
                {
                    if (processedTypes.Contains(underlyingType))
                    {
                        example[property.Name] = new { };
                    }
                    else
                    {
                        try
                        {
                            var nestedExample = GenerateExampleFromSchema(underlyingType, schemas, processedTypes);
                            var parsedExample = JsonSerializer.Deserialize<object>(nestedExample);
                            example[property.Name] = parsedExample ?? new { };
                        }
                        catch
                        {
                            example[property.Name] = new { };
                        }
                    }
                }
            }
            else if (IsComplexType(underlyingType))
            {
                if (processedTypes.Contains(underlyingType))
                {
                    example[property.Name] = new { };
                }
                else
                {
                    // For complex nested objects, generate a nested example
                    var nestedExample = GenerateExampleFromSchema(underlyingType, schemas, processedTypes);
                    try
                    {
                        var parsedExample = JsonSerializer.Deserialize<object>(nestedExample);
                        example[property.Name] = parsedExample ?? new { };
                    }
                    catch
                    {
                        example[property.Name] = new { };
                    }
                }
            }
            else
            {
                example[property.Name] = "{}";
            }
        }

        return JsonSerializer.Serialize(example, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static object GenerateSimpleExample(Type type, HashSet<Type>? processedTypes = null)
    {
        processedTypes ??= [];
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (processedTypes.Contains(underlyingType))
        {
            return new { };
        }

        if (underlyingType == typeof(string)) return "sample text";
        if (IsWholeNumberType(underlyingType)) return 123;
        if (underlyingType == typeof(bool)) return true;
        if (underlyingType == typeof(DateTime)) return DateTime.UtcNow;
        if (underlyingType == typeof(Guid)) return Guid.NewGuid();
        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float)) return 12.34;
        if (IsByteLikeType(underlyingType)) return 0;
        if (underlyingType == typeof(object)) return "object";

        if (underlyingType.IsEnum)
        {
            var enumValues = Enum.GetValues(underlyingType);
            return enumValues.GetValue(0) ?? 0;
        }

        var nestedProcessedTypes = new HashSet<Type>(processedTypes) { underlyingType };

        // Handle arrays and collections
        if (IsEnumerableType(underlyingType))
        {
            var elementType = underlyingType.GetGenericArguments().FirstOrDefault();
            if (elementType != null)
            {
                var elementExample = GenerateSimpleExample(elementType, nestedProcessedTypes);
                return new[] { elementExample };
            }
            return Array.Empty<object>();
        }

        // Handle complex types (both generic and non-generic)
        if (IsComplexType(underlyingType) || underlyingType.IsGenericType)
        {
            var schemas = new Dictionary<string, ApiSchema>();
            try
            {
                var exampleJson = GenerateExampleFromSchema(underlyingType, schemas, nestedProcessedTypes);
                return JsonSerializer.Deserialize<object>(exampleJson) ?? new { };
            }
            catch
            {
                return new { };
            }
        }
        
        return new { };
    }

    private static string GenerateStringExample(string propertyName)
    {
        if (propertyName.Contains("email", StringComparison.OrdinalIgnoreCase))
            return "user@example.com";
        if (propertyName.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("mobile", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("tel", StringComparison.OrdinalIgnoreCase))
            return "+1-555-123-4567";
        if (propertyName.Contains("url", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("website", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("link", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("href", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("uri", StringComparison.OrdinalIgnoreCase))
            return "https://example.com";
        if (string.Equals(propertyName, "firstname", StringComparison.OrdinalIgnoreCase) ||
            propertyName.EndsWith("firstname", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "first", StringComparison.OrdinalIgnoreCase))
            return "Alex";
        if (string.Equals(propertyName, "lastname", StringComparison.OrdinalIgnoreCase) ||
            propertyName.EndsWith("lastname", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("surname", StringComparison.OrdinalIgnoreCase))
            return "Johnson";
        if (string.Equals(propertyName, "name", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("fullname", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("displayname", StringComparison.OrdinalIgnoreCase))
            return "Alex Johnson";
        if (propertyName.Contains("name", StringComparison.OrdinalIgnoreCase))
            return "Sample Name";
        if (propertyName.Contains("username", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("handle", StringComparison.OrdinalIgnoreCase))
            return "alexjohnson";
        if (propertyName.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("pwd", StringComparison.OrdinalIgnoreCase))
            return "P@ssw0rd!";
        if (propertyName.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("accesskey", StringComparison.OrdinalIgnoreCase))
            return "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9";
        if (string.Equals(propertyName, "id", StringComparison.OrdinalIgnoreCase) ||
            propertyName.EndsWith("id", StringComparison.OrdinalIgnoreCase) ||
            propertyName.EndsWith("guid", StringComparison.OrdinalIgnoreCase))
            return "3fa85f64-5717-4562-b3fc-2c963f66afa6";
        if (propertyName.Contains("description", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("bio", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("summary", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("about", StringComparison.OrdinalIgnoreCase))
            return "A brief description of the item.";
        if (propertyName.Contains("content", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("body", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("message", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("note", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("comment", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("text", StringComparison.OrdinalIgnoreCase))
            return "Sample content text.";
        if (propertyName.Contains("title", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("subject", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("heading", StringComparison.OrdinalIgnoreCase))
            return "Sample Title";
        if (propertyName.Contains("street", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("address", StringComparison.OrdinalIgnoreCase))
            return "123 Main Street";
        if (propertyName.Contains("city", StringComparison.OrdinalIgnoreCase))
            return "New York";
        if (propertyName.Contains("state", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("province", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("region", StringComparison.OrdinalIgnoreCase))
            return "NY";
        if (propertyName.Contains("country", StringComparison.OrdinalIgnoreCase))
            return "United States";
        if (propertyName.Contains("zip", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("postal", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("postcode", StringComparison.OrdinalIgnoreCase))
            return "10001";
        if (propertyName.Contains("color", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("colour", StringComparison.OrdinalIgnoreCase))
            return "#FF5733";
        if (propertyName.Contains("currency", StringComparison.OrdinalIgnoreCase))
            return "USD";
        if (propertyName.Contains("code", StringComparison.OrdinalIgnoreCase))
            return "ABC-123";
        if (propertyName.Contains("tag", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("label", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("category", StringComparison.OrdinalIgnoreCase))
            return "example-tag";
        if (propertyName.Contains("path", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("directory", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("folder", StringComparison.OrdinalIgnoreCase))
            return "/path/to/resource";
        if (propertyName.Contains("filename", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "file", StringComparison.OrdinalIgnoreCase))
            return "document.pdf";
        if (propertyName.Contains("host", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("server", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("domain", StringComparison.OrdinalIgnoreCase))
            return "example.com";
        if (propertyName.Contains("ipaddress", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "ip", StringComparison.OrdinalIgnoreCase))
            return "192.168.1.1";
        if (propertyName.Contains("version", StringComparison.OrdinalIgnoreCase))
            return "1.0.0";
        if (propertyName.Contains("locale", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("language", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "lang", StringComparison.OrdinalIgnoreCase))
            return "en-US";
        if (propertyName.Contains("timezone", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("zone", StringComparison.OrdinalIgnoreCase))
            return "UTC";
        if (propertyName.Contains("mimetype", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("contenttype", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(propertyName, "format", StringComparison.OrdinalIgnoreCase))
            return "application/json";

        return "sample text";
    }

    private static int GenerateIntExample(string propertyName)
    {
        if (string.Equals(propertyName, "age", StringComparison.OrdinalIgnoreCase))
            return 30;
        if (propertyName.Contains("year", StringComparison.OrdinalIgnoreCase))
            return DateTime.UtcNow.Year;
        if (propertyName.Contains("month", StringComparison.OrdinalIgnoreCase))
            return 6;
        if (propertyName.Contains("day", StringComparison.OrdinalIgnoreCase))
            return 15;
        if (propertyName.Contains("count", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("quantity", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("qty", StringComparison.OrdinalIgnoreCase))
            return 10;
        if (string.Equals(propertyName, "max", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("maximum", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("limit", StringComparison.OrdinalIgnoreCase))
            return 100;
        if (string.Equals(propertyName, "min", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("minimum", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (propertyName.Contains("size", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("length", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("width", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("height", StringComparison.OrdinalIgnoreCase))
            return 50;
        if (propertyName.Contains("port", StringComparison.OrdinalIgnoreCase))
            return 8080;
        if (propertyName.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return 30;
        if (propertyName.Contains("retry", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("attempt", StringComparison.OrdinalIgnoreCase))
            return 3;
        if (string.Equals(propertyName, "page", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("pagenumber", StringComparison.OrdinalIgnoreCase))
            return 1;
        if (propertyName.Contains("skip", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("offset", StringComparison.OrdinalIgnoreCase))
            return 0;
        if (propertyName.Contains("take", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("pagesize", StringComparison.OrdinalIgnoreCase))
            return 20;
        if (propertyName.Contains("order", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("index", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("rank", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("position", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("priority", StringComparison.OrdinalIgnoreCase))
            return 1;

        return 123;
    }

    private static double GenerateDecimalExample(string propertyName)
    {
        if (propertyName.Contains("price", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("cost", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("fee", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("amount", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("total", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("balance", StringComparison.OrdinalIgnoreCase))
            return 99.99;
        if (propertyName.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("percent", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("ratio", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("discount", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("tax", StringComparison.OrdinalIgnoreCase))
            return 0.15;
        if (propertyName.Contains("lat", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("latitude", StringComparison.OrdinalIgnoreCase))
            return 40.7128;
        if (propertyName.Contains("lon", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("lng", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("longitude", StringComparison.OrdinalIgnoreCase))
            return -74.0060;
        if (propertyName.Contains("weight", StringComparison.OrdinalIgnoreCase))
            return 72.5;
        if (propertyName.Contains("height", StringComparison.OrdinalIgnoreCase))
            return 1.75;
        if (propertyName.Contains("width", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("depth", StringComparison.OrdinalIgnoreCase))
            return 10.5;
        if (propertyName.Contains("score", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("rating", StringComparison.OrdinalIgnoreCase))
            return 4.5;
        if (propertyName.Contains("temp", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("temperature", StringComparison.OrdinalIgnoreCase))
            return 22.5;

        return 12.34;
    }

    public static ApiConstraints? GetPropertyConstraints(PropertyInfo property)
    {
        var c = new ApiConstraints();
        var hasAny = false;

        var stringLen = property.GetCustomAttribute<StringLengthAttribute>();
        if (stringLen is not null)
        {
            if (stringLen.MaximumLength > 0) { c.MaxLength = stringLen.MaximumLength; hasAny = true; }
            if (stringLen.MinimumLength > 0) { c.MinLength = stringLen.MinimumLength; hasAny = true; }
        }

        var minLen = property.GetCustomAttribute<MinLengthAttribute>();
        if (minLen is not null) { c.MinLength ??= minLen.Length; hasAny = true; }

        var maxLen = property.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLen is not null) { c.MaxLength ??= maxLen.Length; hasAny = true; }

        var range = property.GetCustomAttribute<RangeAttribute>();
        if (range is not null)
        {
            c.Minimum = range.Minimum is double d1 ? d1 : Convert.ToDouble(range.Minimum);
            c.Maximum = range.Maximum is double d2 ? d2 : Convert.ToDouble(range.Maximum);
            hasAny = true;
        }

        var regex = property.GetCustomAttribute<RegularExpressionAttribute>();
        if (regex is not null) { c.Pattern = regex.Pattern; hasAny = true; }

        if (property.GetCustomAttribute<EmailAddressAttribute>() is not null)      { c.Format = "email"; hasAny = true; }
        else if (property.GetCustomAttribute<UrlAttribute>() is not null)          { c.Format = "url"; hasAny = true; }
        else if (property.GetCustomAttribute<PhoneAttribute>() is not null)        { c.Format = "phone"; hasAny = true; }
        else if (property.GetCustomAttribute<CreditCardAttribute>() is not null)   { c.Format = "credit-card"; hasAny = true; }

        return hasAny ? c : null;
    }

    public static ApiConstraints? GetParameterConstraints(ParameterInfo parameter)
    {
        var c = new ApiConstraints();
        var hasAny = false;

        var stringLen = parameter.GetCustomAttribute<StringLengthAttribute>();
        if (stringLen is not null)
        {
            if (stringLen.MaximumLength > 0) { c.MaxLength = stringLen.MaximumLength; hasAny = true; }
            if (stringLen.MinimumLength > 0) { c.MinLength = stringLen.MinimumLength; hasAny = true; }
        }

        var minLen = parameter.GetCustomAttribute<MinLengthAttribute>();
        if (minLen is not null) { c.MinLength ??= minLen.Length; hasAny = true; }

        var maxLen = parameter.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLen is not null) { c.MaxLength ??= maxLen.Length; hasAny = true; }

        var range = parameter.GetCustomAttribute<RangeAttribute>();
        if (range is not null)
        {
            c.Minimum = range.Minimum is double d1 ? d1 : Convert.ToDouble(range.Minimum);
            c.Maximum = range.Maximum is double d2 ? d2 : Convert.ToDouble(range.Maximum);
            hasAny = true;
        }

        var regex = parameter.GetCustomAttribute<RegularExpressionAttribute>();
        if (regex is not null) { c.Pattern = regex.Pattern; hasAny = true; }

        if (parameter.GetCustomAttribute<EmailAddressAttribute>() is not null)    { c.Format = "email"; hasAny = true; }
        else if (parameter.GetCustomAttribute<UrlAttribute>() is not null)        { c.Format = "url"; hasAny = true; }
        else if (parameter.GetCustomAttribute<PhoneAttribute>() is not null)      { c.Format = "phone"; hasAny = true; }
        else if (parameter.GetCustomAttribute<CreditCardAttribute>() is not null) { c.Format = "credit-card"; hasAny = true; }

        return hasAny ? c : null;
    }

    public static string CombineRoutes(string baseRoute, string additionalRoute)
    {
        if (string.IsNullOrEmpty(additionalRoute))
        {
            return "/" + baseRoute.TrimStart('/');
        }

        if (additionalRoute.StartsWith('/'))
        {
            return additionalRoute;
        }

        return $"/{baseRoute.TrimStart('/')}/{additionalRoute.TrimStart('/')}";
    }
}
