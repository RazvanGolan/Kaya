using FluentAssertions;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Kaya.GrpcExplorer.Helpers;
using Xunit;

namespace Kaya.GrpcExplorer.Tests;

/// <summary>
/// Tests for ProtobufHelper methods
/// </summary>
public class ProtobufHelperTests
{
    // -------------------------------------------------------------------------
    // Fixture helpers
    // -------------------------------------------------------------------------

    private static FileDescriptor BuildFileDescriptor(FileDescriptorProto fileProto)
    {
        using var ms = new MemoryStream();
        using var cos = new CodedOutputStream(ms);
        fileProto.WriteTo(cos);
        cos.Flush();
        return FileDescriptor.BuildFromByteStrings([ByteString.CopyFrom(ms.ToArray())])[0];
    }

    private static MessageDescriptor BuildSimpleMessage(string name = "TestMessage")
    {
        var fileProto = new FileDescriptorProto
        {
            Name = $"{name.ToLower()}.proto",
            Package = "test",
            Syntax = "proto3"
        };
        var messageProto = new DescriptorProto { Name = name };
        messageProto.Field.Add(new FieldDescriptorProto
        {
            Name = "name",
            JsonName = "name",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.String,
            Label = FieldDescriptorProto.Types.Label.Optional
        });
        fileProto.MessageType.Add(messageProto);
        return BuildFileDescriptor(fileProto).MessageTypes[0];
    }

    private static MessageDescriptor BuildMultiFieldMessage()
    {
        var fileProto = new FileDescriptorProto
        {
            Name = "multi.proto",
            Package = "multi",
            Syntax = "proto3"
        };
        var messageProto = new DescriptorProto { Name = "MultiField" };
        messageProto.Field.Add(new FieldDescriptorProto { Name = "str_field", JsonName = "strField", Number = 1, Type = FieldDescriptorProto.Types.Type.String, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "int32_field", JsonName = "int32Field", Number = 2, Type = FieldDescriptorProto.Types.Type.Int32, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "int64_field", JsonName = "int64Field", Number = 3, Type = FieldDescriptorProto.Types.Type.Int64, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "bool_field", JsonName = "boolField", Number = 4, Type = FieldDescriptorProto.Types.Type.Bool, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "float_field", JsonName = "floatField", Number = 5, Type = FieldDescriptorProto.Types.Type.Float, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "double_field", JsonName = "doubleField", Number = 6, Type = FieldDescriptorProto.Types.Type.Double, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "bytes_field", JsonName = "bytesField", Number = 7, Type = FieldDescriptorProto.Types.Type.Bytes, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "uint32_field", JsonName = "uint32Field", Number = 8, Type = FieldDescriptorProto.Types.Type.Uint32, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "uint64_field", JsonName = "uint64Field", Number = 9, Type = FieldDescriptorProto.Types.Type.Uint64, Label = FieldDescriptorProto.Types.Label.Optional });
        messageProto.Field.Add(new FieldDescriptorProto { Name = "repeated_str", JsonName = "repeatedStr", Number = 10, Type = FieldDescriptorProto.Types.Type.String, Label = FieldDescriptorProto.Types.Label.Repeated });
        fileProto.MessageType.Add(messageProto);
        return BuildFileDescriptor(fileProto).MessageTypes[0];
    }

    private static MessageDescriptor BuildEnumMessage()
    {
        var fileProto = new FileDescriptorProto
        {
            Name = "enum_msg.proto",
            Package = "enumtest",
            Syntax = "proto3"
        };
        var enumProto = new EnumDescriptorProto { Name = "Status" };
        enumProto.Value.Add(new EnumValueDescriptorProto { Name = "UNKNOWN", Number = 0 });
        enumProto.Value.Add(new EnumValueDescriptorProto { Name = "ACTIVE", Number = 1 });
        var messageProto = new DescriptorProto { Name = "EnumMessage" };
        messageProto.Field.Add(new FieldDescriptorProto
        {
            Name = "status",
            JsonName = "status",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.Enum,
            TypeName = ".enumtest.Status",
            Label = FieldDescriptorProto.Types.Label.Optional
        });
        fileProto.EnumType.Add(enumProto);
        fileProto.MessageType.Add(messageProto);
        return BuildFileDescriptor(fileProto).MessageTypes[0];
    }

    private static (MessageDescriptor Parent, MessageDescriptor Child) BuildNestedMessage()
    {
        var fileProto = new FileDescriptorProto
        {
            Name = "nested.proto",
            Package = "nested",
            Syntax = "proto3"
        };
        var childProto = new DescriptorProto { Name = "Child" };
        childProto.Field.Add(new FieldDescriptorProto { Name = "value", JsonName = "value", Number = 1, Type = FieldDescriptorProto.Types.Type.Int32, Label = FieldDescriptorProto.Types.Label.Optional });
        var parentProto = new DescriptorProto { Name = "Parent" };
        parentProto.Field.Add(new FieldDescriptorProto
        {
            Name = "child",
            JsonName = "child",
            Number = 1,
            Type = FieldDescriptorProto.Types.Type.Message,
            TypeName = ".nested.Child",
            Label = FieldDescriptorProto.Types.Label.Optional
        });
        fileProto.MessageType.Add(childProto);
        fileProto.MessageType.Add(parentProto);
        var fd = BuildFileDescriptor(fileProto);
        return (fd.MessageTypes[1], fd.MessageTypes[0]);
    }

    // -------------------------------------------------------------------------
    // MessageDescriptorToSchema
    // -------------------------------------------------------------------------

    [Fact]
    public void MessageDescriptorToSchema_ShouldSetTypeName()
    {
        var schema = ProtobufHelper.MessageDescriptorToSchema(BuildSimpleMessage("MyMessage"));

        schema.TypeName.Should().Be("MyMessage");
        schema.FullTypeName.Should().Be("test.MyMessage");
    }

    [Fact]
    public void MessageDescriptorToSchema_ShouldPopulateFields_ForSimpleMessage()
    {
        var schema = ProtobufHelper.MessageDescriptorToSchema(BuildSimpleMessage());

        schema.Fields.Should().HaveCount(1);
        schema.Fields[0].Name.Should().Be("name");
        schema.Fields[0].Number.Should().Be(1);
        schema.Fields[0].Type.Should().Be("string");
        schema.Fields[0].IsRepeated.Should().BeFalse();
    }

    [Fact]
    public void MessageDescriptorToSchema_ShouldMarkRepeatedField()
    {
        var schema = ProtobufHelper.MessageDescriptorToSchema(BuildMultiFieldMessage());

        schema.Fields.First(f => f.Name == "repeatedStr").IsRepeated.Should().BeTrue();
    }

    [Fact]
    public void MessageDescriptorToSchema_ShouldGenerateExampleJson()
    {
        var schema = ProtobufHelper.MessageDescriptorToSchema(BuildSimpleMessage());

        schema.ExampleJson.Should().NotBeNullOrEmpty();
        schema.ExampleJson.Should().Contain("{");
    }

    [Fact]
    public void MessageDescriptorToSchema_ShouldMapAllScalarFieldTypes()
    {
        var schema = ProtobufHelper.MessageDescriptorToSchema(BuildMultiFieldMessage());
        var typeMap = schema.Fields.ToDictionary(f => f.Name, f => f.Type);

        typeMap["strField"].Should().Be("string");
        typeMap["int32Field"].Should().Be("int32");
        typeMap["int64Field"].Should().Be("int64");
        typeMap["boolField"].Should().Be("bool");
        typeMap["floatField"].Should().Be("float");
        typeMap["doubleField"].Should().Be("double");
        typeMap["bytesField"].Should().Be("bytes");
        typeMap["uint32Field"].Should().Be("uint32");
        typeMap["uint64Field"].Should().Be("uint64");
    }

    [Fact]
    public void MessageDescriptorToSchema_ShouldSetEnumTypeName()
    {
        var schema = ProtobufHelper.MessageDescriptorToSchema(BuildEnumMessage());

        schema.Fields.First(f => f.Name == "status").Type.Should().Be("Status");
    }

    [Fact]
    public void MessageDescriptorToSchema_ShouldSetMessageTypeName_ForNestedField()
    {
        var (parent, _) = BuildNestedMessage();
        var schema = ProtobufHelper.MessageDescriptorToSchema(parent);

        schema.Fields.First(f => f.Name == "child").Type.Should().Be("Child");
    }

    // -------------------------------------------------------------------------
    // GenerateExampleJson
    // -------------------------------------------------------------------------

    [Fact]
    public void GenerateExampleJson_ShouldReturnValidJson()
    {
        var json = ProtobufHelper.GenerateExampleJson(BuildSimpleMessage());

        json.Should().NotBeNullOrEmpty();
        json.Should().StartWith("{");
        json.Should().EndWith("}");
    }

    [Fact]
    public void GenerateExampleJson_ShouldContainStringExample_ForStringField()
    {
        var json = ProtobufHelper.GenerateExampleJson(BuildSimpleMessage());

        json.Should().Contain("\"name\"");
        json.Should().Contain("\"string\"");
    }

    [Fact]
    public void GenerateExampleJson_ShouldContainNumericExample_ForInt32Field()
    {
        var json = ProtobufHelper.GenerateExampleJson(BuildMultiFieldMessage());

        json.Should().Contain("int32Field");
        json.Should().Contain("123");
    }

    [Fact]
    public void GenerateExampleJson_ShouldContainArrayExample_ForRepeatedField()
    {
        var json = ProtobufHelper.GenerateExampleJson(BuildMultiFieldMessage());

        json.Should().Contain("repeatedStr");
        json.Should().Contain("[");
    }

    [Fact]
    public void GenerateExampleJson_ShouldContainEnumValueName_ForEnumField()
    {
        var json = ProtobufHelper.GenerateExampleJson(BuildEnumMessage());

        json.Should().Contain("status");
        json.Should().Contain("UNKNOWN");
    }

    [Fact]
    public void GenerateExampleJson_ShouldHandleNestedMessage()
    {
        var (parent, _) = BuildNestedMessage();
        var json = ProtobufHelper.GenerateExampleJson(parent);

        json.Should().Contain("child");
    }

    [Fact]
    public void GenerateExampleJson_ShouldContainBoolExample_ForBoolField()
    {
        var json = ProtobufHelper.GenerateExampleJson(BuildMultiFieldMessage());

        json.Should().Contain("boolField");
        json.Should().Contain("false");
    }

    [Fact]
    public void GenerateExampleJson_ShouldContainFloatExample_ForFloatField()
    {
        var json = ProtobufHelper.GenerateExampleJson(BuildMultiFieldMessage());

        json.Should().Contain("floatField");
        json.Should().Contain("12.34");
    }
}
