using System.Collections.Concurrent;
using System.Reflection;
using System.Xml.Linq;

namespace Kaya.ApiExplorer.Helpers;

/// <summary>
/// Helper class for reading XML documentation comments from assemblies
/// </summary>
public static class XmlDocumentationHelper
{
    private static readonly ConcurrentDictionary<Assembly, XDocument?> _xmlDocCache = new();

    /// <summary>
    /// Gets the summary documentation for a type
    /// </summary>
    public static string? GetTypeSummary(Type type)
    {
        var xmlDoc = GetXmlDocumentation(type.Assembly);
        if (xmlDoc is null) return null;

        var memberName = $"T:{type.FullName}";
        return GetSummary(xmlDoc, memberName);
    }

    /// <summary>
    /// Gets the summary documentation for a method
    /// </summary>
    public static string? GetMethodSummary(MethodInfo method)
    {
        var xmlDoc = GetXmlDocumentation(method.DeclaringType?.Assembly);
        if (xmlDoc is null) return null;

        var memberName = GetMethodMemberName(method);
        return GetSummary(xmlDoc, memberName);
    }

    /// <summary>
    /// Gets the summary documentation for a property
    /// </summary>
    public static string? GetPropertySummary(PropertyInfo property)
    {
        var xmlDoc = GetXmlDocumentation(property.DeclaringType?.Assembly);
        if (xmlDoc is null) return null;

        var memberName = $"P:{property.DeclaringType?.FullName}.{property.Name}";
        return GetSummary(xmlDoc, memberName);
    }

    /// <summary>
    /// Gets the summary documentation for a parameter
    /// </summary>
    public static string? GetParameterDescription(MethodInfo method, string parameterName)
    {
        var xmlDoc = GetXmlDocumentation(method.DeclaringType?.Assembly);
        if (xmlDoc is null) return null;

        var memberName = GetMethodMemberName(method);
        var member = xmlDoc.Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

        if (member is null) return null;

        var paramElement = member.Elements("param")
            .FirstOrDefault(p => p.Attribute("name")?.Value == parameterName);

        return CleanDocumentationText(paramElement?.Value);
    }

    /// <summary>
    /// Gets the returns documentation for a method
    /// </summary>
    public static string? GetReturnsDescription(MethodInfo method)
    {
        var xmlDoc = GetXmlDocumentation(method.DeclaringType?.Assembly);
        if (xmlDoc is null) return null;

        var memberName = GetMethodMemberName(method);
        var member = xmlDoc.Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

        if (member is null) return null;

        var returnsElement = member.Element("returns");
        return CleanDocumentationText(returnsElement?.Value);
    }

    /// <summary>
    /// Gets the response description for a specific HTTP status code from XML doc
    /// <response code="200">Success</response> tags
    /// </summary>
    public static string? GetResponseDescription(MethodInfo method, int statusCode)
    {
        var xmlDoc = GetXmlDocumentation(method.DeclaringType?.Assembly);
        if (xmlDoc is null) return null;

        var memberName = GetMethodMemberName(method);
        var member = xmlDoc.Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

        if (member is null) return null;

        var responseElement = member.Elements("response")
            .FirstOrDefault(r => r.Attribute("code")?.Value == statusCode.ToString());

        return CleanDocumentationText(responseElement?.Value);
    }

    private static string? GetSummary(XDocument xmlDoc, string memberName)
    {
        try
        {
            var member = xmlDoc.Descendants("member")
                .FirstOrDefault(m => m.Attribute("name")?.Value == memberName);

            if (member is null) return null;

            var summary = member.Element("summary");
            return CleanDocumentationText(summary?.Value);
        }
        catch
        {
            return null;
        }
    }

    private static string? CleanDocumentationText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // Remove leading/trailing whitespace and normalize line breaks
        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line));

        var result = string.Join(" ", lines);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static string GetMethodMemberName(MethodInfo method)
    {
        var declaringType = method.DeclaringType?.FullName;
        var parameters = method.GetParameters();

        if (parameters.Length is 0)
        {
            return $"M:{declaringType}.{method.Name}";
        }

        var paramTypes = string.Join(",", parameters.Select(p => GetTypeName(p.ParameterType)));
        return $"M:{declaringType}.{method.Name}({paramTypes})";
    }

    private static string GetTypeName(Type type)
    {
        // Handle generic types
        if (type.IsGenericType)
        {
            var genericTypeName = type.GetGenericTypeDefinition().FullName;
            if (genericTypeName != null)
            {
                // Remove the `1, `2, etc. suffix
                var backtickIndex = genericTypeName.IndexOf('`');
                if (backtickIndex >= 0)
                {
                    genericTypeName = genericTypeName[..backtickIndex];
                }

                var genericArgs = type.GetGenericArguments();
                var genericArgNames = string.Join(",", genericArgs.Select(GetTypeName));
                return $"{genericTypeName}{{{genericArgNames}}}";
            }
        }

        // Handle array types
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            return $"{GetTypeName(elementType!)}[]";
        }

        // Handle nullable types
        var nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType != null)
        {
            return GetTypeName(nullableType);
        }

        return type.FullName ?? type.Name;
    }

    private static XDocument? GetXmlDocumentation(Assembly? assembly)
    {
        if (assembly is null) return null;

        return _xmlDocCache.GetOrAdd(assembly, LoadXmlDocumentation);
    }

    private static XDocument? LoadXmlDocumentation(Assembly assembly)
    {
        try
        {
            var assemblyLocation = assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var xmlPath = Path.ChangeExtension(assemblyLocation, ".xml");
                if (File.Exists(xmlPath))
                {
                    return XDocument.Load(xmlPath);
                }
            }
        }
        catch
        {
            // Silently fail if XML documentation is not available
        }

        return null;
    }
}
