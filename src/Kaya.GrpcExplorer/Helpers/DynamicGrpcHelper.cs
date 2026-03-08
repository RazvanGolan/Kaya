using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Kaya.GrpcExplorer.Models;

namespace Kaya.GrpcExplorer.Helpers;

/// <summary>
/// Helper class for dynamic gRPC method invocation
/// </summary>
public static class DynamicGrpcHelper
{
    /// <summary>
    /// Creates a dynamic message from JSON
    /// </summary>
    public static IMessage CreateMessageFromJson(MessageDescriptor descriptor, string json)
    {
        // Try to use generated type if available
        var generatedType = CompiledMessageTypeCache.FindGeneratedType(descriptor.Name);
        if (generatedType is not null)
        {
            return CreateMessageFromJsonWithGeneratedType(generatedType, json);
        }

        // Fall back to dynamic approach
        return CreateDynamicMessageFromJson(descriptor, json);
    }

    /// <summary>
    /// Creates a message using a generated protobuf type
    /// </summary>
    private static IMessage CreateMessageFromJsonWithGeneratedType(Type generatedType, string json)
    {
        try
        {
            var jsonParser = new JsonParser(JsonParser.Settings.Default);
            
            // Get the generic Parse<T> method
            var parseMethod = typeof(JsonParser)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m is { Name: "Parse", IsGenericMethod: true } && m.GetParameters().Length == 1);
            
            if (parseMethod is not null)
            {
                var genericParse = parseMethod.MakeGenericMethod(generatedType);
                try
                {
                    var message = genericParse.Invoke(jsonParser, [json]);
                    if (message is IMessage result)
                    {
                        return result;
                    }
                }
                catch (TargetInvocationException tie) when (tie.InnerException is not null)
                {
                    // Extract the actual parsing error from the reflection invocation
                    throw tie.InnerException;
                }
            }

            throw new InvalidOperationException($"Could not parse JSON for generated type {generatedType.FullName}");
        }
        catch (InvalidOperationException ioe) when (ioe.Message.Contains("enum") || ioe.Message.Contains("Unknown field"))
        {
            // Re-throw validation errors as-is for better error messages
            throw new InvalidOperationException(
                $"JSON validation failed: {ioe.Message}", ioe);
        }
        catch (Exception ex)
        {
            // Extract the most relevant error message
            var errorMessage = GetMostRelevantErrorMessage(ex);
            throw new InvalidOperationException(
                $"Failed to parse JSON: {errorMessage}", ex);
        }
    }

    private static string GetMostRelevantErrorMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }
        
        return !string.IsNullOrWhiteSpace(current.Message) ? current.Message : ex.Message;
    }

    /// <summary>
    /// Creates a dynamic message from JSON (for descriptors without generated types)
    /// </summary>
    private static IMessage CreateDynamicMessageFromJson(MessageDescriptor descriptor, string json)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;
            
            // Create a ByteString by manually building the protobuf message
            using var ms = new MemoryStream();
            using var output = new CodedOutputStream(ms);
            
            // Write each field from JSON to the protobuf format
            foreach (var field in descriptor.Fields.InDeclarationOrder())
            {
                WriteFieldFromJson(output, field, root);
            }
            
            output.Flush();
            var bytes = ms.ToArray();
            
            // Now parse those bytes back into a message
            return ParseMessageFromBytes(descriptor, bytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse JSON for message type {descriptor.FullName}. JSON: {json}", ex);
        }
    }

    /// <summary>
    /// Checks if a field is a map field
    /// </summary>
    private static bool IsMapField(FieldDescriptor field)
    {
        // Map fields are repeated message fields where the message type:
        // 1. Has exactly 2 fields
        // 2. Has fields named "key" and "value"
        // 3. The message name typically ends with "Entry"
        if (!field.IsRepeated || field.FieldType != FieldType.Message)
        {
            return false;
        }

        var messageType = field.MessageType;
        var fields = messageType.Fields.InDeclarationOrder().ToList();
        
        return fields.Count == 2 &&
               fields.Any(f => f.Name == "key") &&
               fields.Any(f => f.Name == "value");
    }

    /// <summary>
    /// Writes a field from JSON to CodedOutputStream
    /// </summary>
    private static void WriteFieldFromJson(CodedOutputStream output, FieldDescriptor field, JsonElement root)
    {
        var jsonName = field.JsonName;
        
        if (!root.TryGetProperty(jsonName, out var value))
        {
            // Try with the original field name as fallback
            if (!root.TryGetProperty(field.Name, out value))
            {
                return; // Field not present in JSON
            }
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        var tag = WireFormat.MakeTag(field.FieldNumber, GetWireType(field));
        
        // Handle map fields (JSON object)
        if (IsMapField(field))
        {
            if (value.ValueKind is not JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Expected an object to populate a map for field '{field.JsonName}', but got {value.ValueKind}. " +
                    $"Map fields should be JSON objects like {{\"key1\": \"value1\", \"key2\": \"value2\"}}");
            }
            
            var keyField = field.MessageType.FindFieldByName("key");
            var valueField = field.MessageType.FindFieldByName("value");
            
            if (keyField == null || valueField == null)
            {
                throw new InvalidOperationException($"Map field '{field.JsonName}' is missing key or value fields");
            }
            
            foreach (var property in value.EnumerateObject())
            {
                output.WriteTag(tag);
                
                // Write map entry as a message with key and value fields
                using var ms = new MemoryStream();
                using var entryOutput = new CodedOutputStream(ms);
                
                // Write key field (tag 1)
                var keyTag = WireFormat.MakeTag(1, GetWireType(keyField));
                entryOutput.WriteTag(keyTag);
                WriteMapKeyValue(entryOutput, keyField, property.Name);
                
                // Write value field (tag 2)
                var valueTag = WireFormat.MakeTag(2, GetWireType(valueField));
                entryOutput.WriteTag(valueTag);
                WriteFieldValue(entryOutput, valueField, property.Value);
                
                entryOutput.Flush();
                output.WriteBytes(ByteString.CopyFrom(ms.ToArray()));
            }
        }
        else if (field.IsRepeated)
        {
            if (value.ValueKind is not JsonValueKind.Array)
                return;
            
            foreach (var item in value.EnumerateArray())
            {
                output.WriteTag(tag);
                WriteFieldValue(output, field, item);
            }
        }
        else
        {
            output.WriteTag(tag);
            WriteFieldValue(output, field, value);
        }
    }

    /// <summary>
    /// Writes a map key value (protobuf JSON keys are strings, but may need parsing for numeric types)
    /// </summary>
    private static void WriteMapKeyValue(CodedOutputStream output, FieldDescriptor keyField, string key)
    {
        switch (keyField.FieldType)
        {
            case FieldType.String:
                output.WriteString(key);
                break;
            case FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32:
                output.WriteInt32(int.Parse(key));
                break;
            case FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64:
                output.WriteInt64(long.Parse(key));
                break;
            case FieldType.UInt32 or FieldType.Fixed32:
                output.WriteUInt32(uint.Parse(key));
                break;
            case FieldType.UInt64 or FieldType.Fixed64:
                output.WriteUInt64(ulong.Parse(key));
                break;
            case FieldType.Bool:
                output.WriteBool(bool.Parse(key));
                break;
            default:
                output.WriteString(key);
                break;
        }
    }

    /// <summary>
    /// Gets the wire type for a field
    /// </summary>
    private static WireFormat.WireType GetWireType(FieldDescriptor field)
    {
        return field.FieldType switch
        {
            FieldType.Double or FieldType.Fixed64 or FieldType.SFixed64 => WireFormat.WireType.Fixed64,
            FieldType.Float or FieldType.Fixed32 or FieldType.SFixed32 => WireFormat.WireType.Fixed32,
            FieldType.Int64 or FieldType.UInt64 or FieldType.Int32 or FieldType.UInt32 or
            FieldType.SInt32 or FieldType.SInt64 or FieldType.Bool or FieldType.Enum => WireFormat.WireType.Varint,
            FieldType.String or FieldType.Bytes or FieldType.Message => WireFormat.WireType.LengthDelimited,
            _ => WireFormat.WireType.Varint
        };
    }

    /// <summary>
    /// Writes a field value to CodedOutputStream
    /// </summary>
    private static void WriteFieldValue(CodedOutputStream output, FieldDescriptor field, JsonElement value)
    {
        switch (field.FieldType)
        {
            case FieldType.String:
                output.WriteString(value.GetString() ?? "");
                break;
            case FieldType.Int32:
            case FieldType.SInt32:
            case FieldType.SFixed32:
                output.WriteInt32(value.GetInt32());
                break;
            case FieldType.Int64:
            case FieldType.SInt64:
            case FieldType.SFixed64:
                output.WriteInt64(value.GetInt64());
                break;
            case FieldType.UInt32:
            case FieldType.Fixed32:
                output.WriteUInt32(value.GetUInt32());
                break;
            case FieldType.UInt64:
            case FieldType.Fixed64:
                output.WriteUInt64(value.GetUInt64());
                break;
            case FieldType.Bool:
                output.WriteBool(value.GetBoolean());
                break;
            case FieldType.Float:
                output.WriteFloat(value.GetSingle());
                break;
            case FieldType.Double:
                output.WriteDouble(value.GetDouble());
                break;
            case FieldType.Enum:
                if (value.ValueKind == JsonValueKind.String)
                {
                    var enumString = value.GetString() ?? "";
                    var enumValue = field.EnumType.FindValueByName(enumString);
                    if (enumValue is null)
                    {
                        var validValues = string.Join(", ", field.EnumType.Values.Select(v => v.Name));
                        throw new InvalidOperationException(
                            $"Invalid value '{enumString}' for enum field '{field.JsonName}'. Valid values are: {validValues}");
                    }
                    output.WriteEnum(enumValue.Number);
                }
                else
                {
                    output.WriteEnum(value.GetInt32());
                }
                break;
            case FieldType.Message:
                var nestedMessage = CreateMessageFromJson(field.MessageType, value.GetRawText());
                output.WriteMessage(nestedMessage);
                break;
        }
    }

    /// <summary>
    /// Converts a message to JSON
    /// </summary>
    public static string MessageToJson(IMessage message)
    {
        if (message is DynamicProtobufMessage dynamicMsg)
        {
            return ConvertProtobufBytesToJson(dynamicMsg.Descriptor, dynamicMsg.GetBytes());
        }

        var formatter = new JsonFormatter(JsonFormatter.Settings.Default);
        return formatter.Format(message);
    }

    /// <summary>
    /// Converts protobuf bytes to JSON using the descriptor
    /// </summary>
    private static string ConvertProtobufBytesToJson(MessageDescriptor descriptor, byte[] bytes)
    {
        var result = ConvertProtobufBytesToObject(descriptor, bytes);
        return JsonSerializer.Serialize(result, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
    }

    /// <summary>
    /// Converts protobuf bytes to a dictionary object using the descriptor
    /// </summary>
    private static Dictionary<string, object?> ConvertProtobufBytesToObject(MessageDescriptor descriptor, byte[] bytes)
    {
        var result = new Dictionary<string, object?>();
        var input = new CodedInputStream(bytes);

        while (!input.IsAtEnd)
        {
            var tag = input.ReadTag();
            var fieldNumber = WireFormat.GetTagFieldNumber(tag);
            var field = descriptor.FindFieldByNumber(fieldNumber);

            if (field is not null)
            {
                var value = ReadFieldValue(input, field);
                
                if (IsMapField(field))
                {
                    // Map fields should be represented as dictionaries/objects in JSON
                    if (!result.ContainsKey(field.JsonName))
                    {
                        result[field.JsonName] = new Dictionary<string, object?>();
                    }
                    
                    // The value is a dictionary with "key" and "value" entries
                    if (value is Dictionary<string, object?> mapEntry)
                    {
                        var mapDict = (Dictionary<string, object?>)result[field.JsonName]!;
                        var key = mapEntry.GetValueOrDefault("key")?.ToString() ?? "";
                        var val = mapEntry.GetValueOrDefault("value");
                        mapDict[key] = val;
                    }
                }
                else if (field.IsRepeated)
                {
                    if (!result.ContainsKey(field.JsonName))
                    {
                        result[field.JsonName] = new List<object?>();
                    }
                    ((List<object?>)result[field.JsonName]!).Add(value);
                }
                else
                {
                    result[field.JsonName] = value;
                }
            }
            else
            {
                input.SkipLastField();
            }
        }

        return result;
    }

    /// <summary>
    /// Reads a field value from CodedInputStream
    /// </summary>
    private static object? ReadFieldValue(CodedInputStream input, FieldDescriptor field)
    {
        return field.FieldType switch
        {
            FieldType.String => input.ReadString(),
            FieldType.Int32 or FieldType.SInt32 or FieldType.SFixed32 => input.ReadInt32(),
            FieldType.Int64 or FieldType.SInt64 or FieldType.SFixed64 => input.ReadInt64(),
            FieldType.UInt32 or FieldType.Fixed32 => input.ReadUInt32(),
            FieldType.UInt64 or FieldType.Fixed64 => input.ReadUInt64(),
            FieldType.Bool => input.ReadBool(),
            FieldType.Float => input.ReadFloat(),
            FieldType.Double => input.ReadDouble(),
            FieldType.Bytes => Convert.ToBase64String(input.ReadBytes().ToByteArray()),
            FieldType.Enum => field.EnumType.FindValueByNumber(input.ReadEnum())?.Name ?? input.ReadEnum().ToString(),
            FieldType.Message => ConvertProtobufBytesToObject(field.MessageType, input.ReadBytes().ToByteArray()),
            _ => null
        };
    }

    /// <summary>
    /// Gets the method descriptor for a service method
    /// </summary>
    public static async Task<MethodDescriptor?> GetMethodDescriptorAsync(
        string serverAddress,
        string serviceName,
        string methodName,
        bool allowInsecure,
        FileDescriptorSet? cachedDescriptorSet = null)
    {
        FileDescriptorSet? fileDescriptorSet = cachedDescriptorSet;
        
        fileDescriptorSet ??= await GrpcReflectionHelper.GetFileDescriptorAsync(
                serverAddress,
                serviceName,
                allowInsecure);

        if (fileDescriptorSet is null)
        {
            return null;
        }

        // Serialize all file descriptors (including dependencies) to ByteStrings
        // and build them together so cross-file references resolve correctly
        var byteStrings = new List<ByteString>();
        foreach (var fileProto in fileDescriptorSet.File)
        {
            using var ms = new MemoryStream();
            using var cos = new CodedOutputStream(ms);
            fileProto.WriteTo(cos);
            cos.Flush();
            byteStrings.Add(ByteString.CopyFrom(ms.ToArray()));
        }

        var fileDescriptors = FileDescriptor.BuildFromByteStrings(byteStrings);

        foreach (var fd in fileDescriptors)
        {
            foreach (var service in fd.Services)
            {
                if (service.FullName == serviceName)
                {
                    return service.Methods.FirstOrDefault(m => m.Name == methodName);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a gRPC method definition for dynamic invocation
    /// </summary>
    public static Method<IMessage, IMessage> CreateMethod(
        MethodDescriptor descriptor,
        string serviceName)
    {
        var methodType = descriptor.IsClientStreaming switch
        {
            false when !descriptor.IsServerStreaming => MethodType.Unary,
            false when descriptor.IsServerStreaming => MethodType.ServerStreaming,
            true when !descriptor.IsServerStreaming => MethodType.ClientStreaming,
            _ => MethodType.DuplexStreaming
        };

        return new Method<IMessage, IMessage>(
            type: methodType,
            serviceName: serviceName,
            name: descriptor.Name,
            requestMarshaller: Marshallers.Create(
                serializer: msg => msg.ToByteArray(),
                deserializer: bytes => ParseMessageFromBytes(descriptor.InputType, bytes)),
            responseMarshaller: Marshallers.Create(
                serializer: msg => msg.ToByteArray(),
                deserializer: bytes => ParseMessageFromBytes(descriptor.OutputType, bytes))
        );
    }

    /// <summary>
    /// Parses a protobuf message from bytes using a message descriptor
    /// </summary>
    private static IMessage ParseMessageFromBytes(MessageDescriptor descriptor, byte[] bytes)
    {
        // Try to use generated type if available
        var generatedType = CompiledMessageTypeCache.FindGeneratedType(descriptor.FullName);
        if (generatedType is not null)
        {
            var parser = CompiledMessageTypeCache.GetParser(generatedType);
            if (parser is not null)
            {
                try
                {
                    using var ms = new MemoryStream(bytes);
                    return parser.ParseFrom(ms);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to parse bytes for generated type {descriptor.FullName}", ex);
                }
            }
        }

        // Fall back to dynamic message
        try
        {
            return new DynamicProtobufMessage(descriptor, bytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse bytes for message type {descriptor.FullName}", ex);
        }
    }

    /// <summary>
    /// Invokes a unary method dynamically
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Requires a live gRPC channel with an active call invoker connected to a running server; verified through integration tests.")]
    public static async Task<IMessage> InvokeUnaryAsync(
        GrpcChannel channel,
        Method<IMessage, IMessage> method,
        IMessage request,
        Metadata? metadata = null)
    {
        var callInvoker = channel.CreateCallInvoker();
        var call = callInvoker.AsyncUnaryCall(
            method,
            null,
            new CallOptions(headers: metadata),
            request);

        return await call.ResponseAsync;
    }

}
