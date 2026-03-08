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

    private static bool IsEnumerableType(Type type)
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

    public static string GenerateExampleJson(Type type, Dictionary<string, ApiSchema> schemas, HashSet<Type> processedTypes)
    {
        if (type == typeof(string)) return "\"string value\"";
        if (type == typeof(int)) return "123";
        if (type == typeof(bool)) return "true";
        if (type == typeof(DateTime)) return "\"2023-07-13T10:30:00Z\"";
        if (type == typeof(Guid)) return "\"3fa85f64-5717-4562-b3fc-2c963f66afa6\"";
        if (type == typeof(decimal) || type == typeof(double) || type == typeof(float)) return "12.34";
        if (type == typeof(byte)) return "0";
        // TODO: find a better example for enum types
        if (type.IsEnum) 
            return "0";

        // Handle Dictionary types specifically
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var keyType = type.GetGenericArguments()[0];
            var valueType = type.GetGenericArguments()[1];
            
            var keyExample = GenerateExampleJson(keyType, schemas, processedTypes);
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
        return schemas.GetValueOrDefault(typeName);
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

    // TODO: see if this can be enhanced
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
                // TODO: add more realistic examples based on property name like id, email, etc.
                example[property.Name] = property.Name.ToLower().Contains("email") ? "email@example.com" : "sample text";
            }
            else if (underlyingType == typeof(int) || underlyingType == typeof(long))
            {
                example[property.Name] = 123;
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
                example[property.Name] = 12.34;
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
                    
                    var keyExample = GenerateSimpleExample(keyType, processedTypes);
                    var valueExample = GenerateSimpleExample(valueType, processedTypes);
                    
                    var dictionaryExample = new Dictionary<string, object>();
                    var keyString = keyExample.ToString() ?? "key";
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
        if (underlyingType == typeof(int) || underlyingType == typeof(long)) return 123;
        if (underlyingType == typeof(bool)) return true;
        if (underlyingType == typeof(DateTime)) return DateTime.UtcNow;
        if (underlyingType == typeof(Guid)) return Guid.NewGuid();
        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float)) return 12.34;
        if (underlyingType == typeof(byte)) return (byte)0;
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
