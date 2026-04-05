using System.Collections.Concurrent;
using System.Reflection;
using Google.Protobuf;

namespace Kaya.GrpcExplorer.Helpers;

/// <summary>
/// Cache for finding compiled protobuf message types from loaded assemblies at runtime
/// This allows using compiled proto classes when available, falling back to dynamic messages
/// </summary>
public static class CompiledMessageTypeCache
{
    private static readonly ConcurrentDictionary<string, Type?> _typeCache = new();
    private static bool _assembliesScanned;
    private static readonly object _scanLock = new();
    private static Assembly[] _scannedAssemblies = [];

    /// <summary>
    /// Tries to find a generated protobuf type by its full name
    /// </summary>
    public static Type? FindGeneratedType(string fullTypeName)
    {
        EnsureAssembliesScanned();

        return _typeCache.GetOrAdd(fullTypeName, typeName =>
        {
            // Search all types in protobuf-using assemblies
            foreach (var assembly in _scannedAssemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (!typeof(IMessage).IsAssignableFrom(type))
                        {
                            continue;
                        }
                        
                        if (type.FullName == typeName || type.Name == typeName)
                        {
                            return type;
                        }
                    }
                }
                catch
                {
                    // Ignore assembly load errors
                }
            }

            return null;
        });
    }

    /// <summary>
    /// Tries to get the static Parser property from a generated type
    /// </summary>
    public static MessageParser? GetParser(Type generatedType)
    {
        try
        {
            var parserProperty = generatedType.GetProperty("Parser", 
                BindingFlags.Public | BindingFlags.Static);
            return parserProperty?.GetValue(null) as MessageParser;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Scans loaded assemblies for protobuf message types
    /// </summary>
    private static void EnsureAssembliesScanned()
    {
        if (_assembliesScanned)
        {
            return;
        }

        lock (_scanLock)
        {
            if (_assembliesScanned)
            {
                return;
            }

            var assemblies = new List<Assembly>();
            
            // Scan all loaded assemblies in the current app domain
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Skip system assemblies
                    var name = assembly.GetName().Name ?? "";
                    if (name.StartsWith("System.") || 
                        name.StartsWith("Microsoft.") ||
                        name == "mscorlib" ||
                        name.StartsWith("netstandard"))
                    {
                        continue;
                    }

                    // Check if assembly references Google.Protobuf
                    var referencesProtobuf = assembly.GetReferencedAssemblies()
                        .Any(a => a.Name == "Google.Protobuf");

                    if (referencesProtobuf || assembly == typeof(IMessage).Assembly)
                    {
                        assemblies.Add(assembly);
                    }
                }
                catch
                {
                    // Ignore assemblies we can't inspect
                }
            }

            _scannedAssemblies = [.. assemblies];
            _assembliesScanned = true;
        }
    }

    /// <summary>
    /// Clears the cache
    /// </summary>
    public static void ClearCache()
    {
        _typeCache.Clear();
        _assembliesScanned = false;
        _scannedAssemblies = [];
    }
}
