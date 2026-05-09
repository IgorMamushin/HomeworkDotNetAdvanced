using System.Text;
using Demo.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Generator
{
	[Generator]
	public sealed class GenerateSerializerSourceGenerator : ISourceGenerator
	{
		private static readonly DiagnosticDescriptor s_unsupportedTypeRule =
			new DiagnosticDescriptor(
				"SG0001",
				"Unsupported property type",
				"Property '{0}' in type '{1}' has unsupported type '{2}' for binary serialization",
				"GenerateSerializer",
				DiagnosticSeverity.Error,
				true);

		public void Initialize(GeneratorInitializationContext context)
		{
			context.RegisterForSyntaxNotifications(() => new GenerateSerializerSyntaxReceiver());
		}

		public void Execute(GeneratorExecutionContext context)
		{
			var receiver = context.SyntaxReceiver as GenerateSerializerSyntaxReceiver;
			if (receiver == null)
            {
                return;
            }

            var compilation = context.Compilation;
			var serializableTypes = new List<SerializableType>();

			foreach (var classDecl in receiver.Candidates)
			{
				var semanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);
				var classSymbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
				if (classSymbol == null)
                {
                    continue;
                }

                if (!HasGenerateSerializerAttribute(classSymbol))
                {
                    continue;
                }

                var serializableType = BuildSerializableType(context, classSymbol);
				if (serializableType != null)
				{
					serializableTypes.Add(serializableType);
				}
			}

			foreach (var type in serializableTypes)
			{
				var source = GenerateSerializerClass(type);

				context.AddSource(
					type.TypeName + ".Serializer.g.cs",
					SourceText.From(source, Encoding.UTF8));
			}
		}

		private static bool HasGenerateSerializerAttribute(INamedTypeSymbol classSymbol)
		{
			foreach (var attr in classSymbol.GetAttributes())
			{
				var attrClass = attr.AttributeClass;
				if (attrClass == null)
                {
                    continue;
                }

                var name = attrClass.Name;
				var fullName = attrClass.ToDisplayString();

				if (name == "GenerateSerializerAttribute" ||
					fullName == "Demo.App.GenerateSerializerAttribute")
				{
					return true;
				}
			}

			return false;
		}

		private static SerializableType BuildSerializableType(
			GeneratorExecutionContext context,
			INamedTypeSymbol classSymbol)
		{
			var props = new List<SerializableProperty>();

			foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
			{
				if (member.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (member.GetMethod == null)
                {
                    continue;
                }

                var type = member.Type;
				if (!IsSupportedType(type))
				{
					ReportUnsupportedProperty(context, member, classSymbol);
					continue;
				}

				var canonicalTypeName = GetCanonicalTypeName(type);
				props.Add(new SerializableProperty(member.Name, canonicalTypeName));
			}

			var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
				? string.Empty
				: classSymbol.ContainingNamespace.ToDisplayString();

			return new SerializableType(ns, classSymbol.Name, props);
		}

		private static bool IsSupportedType(ITypeSymbol type)
		{
			switch (type.SpecialType)
			{
				case SpecialType.System_Int32:
				case SpecialType.System_Int64:
				case SpecialType.System_Double:
				case SpecialType.System_Boolean:
				case SpecialType.System_String:
				case SpecialType.System_DateTime:
					return true;
				default:
					return false;
			}
		}

		private static string GetCanonicalTypeName(ITypeSymbol type)
		{
			switch (type.SpecialType)
			{
				case SpecialType.System_Int32:
					return "int";
				case SpecialType.System_Int64:
					return "long";
				case SpecialType.System_Double:
					return "double";
				case SpecialType.System_Boolean:
					return "bool";
				case SpecialType.System_String:
					return "string";
                case SpecialType.System_DateTime:
                    return "DateTime";
				default:
					return type.ToDisplayString();
			}
		}

		private static void ReportUnsupportedProperty(
			GeneratorExecutionContext context,
			IPropertySymbol property,
			INamedTypeSymbol classSymbol)
		{
			var location = property.Locations.FirstOrDefault();

			var diagnostic = Diagnostic.Create(
				s_unsupportedTypeRule,
				location,
				property.Name,
				classSymbol.Name,
				property.Type.ToDisplayString());

			context.ReportDiagnostic(diagnostic);
		}

		private static string GenerateSerializerClass(SerializableType type)
		{
			var sb = new StringBuilder();

			sb.AppendLine("// <auto-generated/>");
			sb.AppendLine("#nullable disable");
			sb.AppendLine("using System;");
			sb.AppendLine("using System.Buffers.Binary;");
			sb.AppendLine();

			if (!string.IsNullOrEmpty(type.Namespace))
			{
				sb.Append("namespace ").Append(type.Namespace).AppendLine(";");
				sb.AppendLine();
			}

			sb.Append("public partial class ").AppendLine(type.TypeName);
			sb.AppendLine("{");

			AppendGetByteCount(sb, type);
			sb.AppendLine();
			AppendSerializeToSpan(sb, type);
			sb.AppendLine();
			AppendSerializeToArray(sb, type);
			sb.AppendLine();
			AppendDeserialize(sb, type);
			sb.AppendLine();
			AppendDeserializeFromArray(sb, type);

			sb.AppendLine("}");

			return sb.ToString();
		}

		private static void AppendGetByteCount(StringBuilder sb, SerializableType type)
		{
			sb.AppendLine("    public int GetByteCount()");
			sb.AppendLine("    {");
			sb.AppendLine("        var count = 0;");

			foreach (var prop in type.Properties)
			{
				switch (prop.TypeName)
				{
					case "int":
						sb.AppendLine("        count += 4;");
						break;
					case "long":
					case "double":
					case "DateTime":
						sb.AppendLine("        count += 8;");
						break;
					case "bool":
						sb.AppendLine("        count += 1;");
						break;
					case "string":
						sb.Append("        count += 4 + (").Append(prop.Name)
						  .Append(" == null ? 0 : System.Text.Encoding.UTF8.GetByteCount(")
						  .Append(prop.Name).AppendLine("));");
						break;
				}
			}

			sb.AppendLine("        return count;");
			sb.AppendLine("    }");
		}

		private static void AppendSerializeToSpan(StringBuilder sb, SerializableType type)
		{
			sb.AppendLine("    public int SerializeTo(Span<byte> destination)");
			sb.AppendLine("    {");
			sb.AppendLine("        if (destination.Length < GetByteCount())");
			sb.AppendLine("            throw new ArgumentException(\"Destination buffer is too small.\", nameof(destination));");
			sb.AppendLine("        var offset = 0;");

			foreach (var prop in type.Properties)
            {
                AppendSerializeProperty(sb, prop);
            }

            sb.AppendLine("        return offset;");
			sb.AppendLine("    }");
		}

		private static void AppendSerializeProperty(StringBuilder sb, SerializableProperty prop)
		{
			switch (prop.TypeName)
			{
				case "int":
					sb.Append("        BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), ").Append(prop.Name).AppendLine(");");
					sb.AppendLine("        offset += 4;");
					break;
				case "long":
					sb.Append("        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset), ").Append(prop.Name).AppendLine(");");
					sb.AppendLine("        offset += 8;");
					break;
				case "double":
					sb.Append("        BinaryPrimitives.WriteDoubleLittleEndian(destination.Slice(offset), ").Append(prop.Name).AppendLine(");");
					sb.AppendLine("        offset += 8;");
					break;
				case "bool":
					sb.Append("        destination[offset] = (byte)(").Append(prop.Name).AppendLine(" ? 1 : 0);");
					sb.AppendLine("        offset += 1;");
					break;
				case "DateTime":
					sb.Append("        BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(offset), ").Append(prop.Name).AppendLine(".ToBinary());");
					sb.AppendLine("        offset += 8;");
					break;
				case "string":
					sb.Append("        if (").Append(prop.Name).AppendLine(" == null)");
					sb.AppendLine("        {");
					sb.AppendLine("            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), -1);");
					sb.AppendLine("            offset += 4;");
					sb.AppendLine("        }");
					sb.AppendLine("        else");
					sb.AppendLine("        {");
					sb.Append("            var priv").Append(prop.Name).Append("ByteCount = System.Text.Encoding.UTF8.GetByteCount(").Append(prop.Name).AppendLine(");");
					sb.Append("            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset), priv").Append(prop.Name).AppendLine("ByteCount);");
					sb.AppendLine("            offset += 4;");
					sb.Append("            System.Text.Encoding.UTF8.GetBytes(").Append(prop.Name).AppendLine(", destination.Slice(offset));");
					sb.Append("            offset += priv").Append(prop.Name).AppendLine("ByteCount;");
					sb.AppendLine("        }");
					break;
			}
		}

		private static void AppendSerializeToArray(StringBuilder sb, SerializableType type)
		{
			sb.AppendLine("    public byte[] Serialize()");
			sb.AppendLine("    {");
			sb.AppendLine("        var buffer = new byte[GetByteCount()];");
			sb.AppendLine("        SerializeTo(buffer);");
			sb.AppendLine("        return buffer;");
			sb.AppendLine("    }");
		}

		private static void AppendDeserialize(StringBuilder sb, SerializableType type)
		{
			sb.Append("    public static ").Append(type.TypeName).AppendLine(" Deserialize(ReadOnlySpan<byte> source)");
			sb.AppendLine("    {");
			sb.AppendLine("        var offset = 0;");

			foreach (var prop in type.Properties)
            {
                AppendDeserializeProperty(sb, prop);
            }

            sb.Append("        return new ").Append(type.TypeName).AppendLine();
			sb.AppendLine("        {");
			foreach (var prop in type.Properties)
            {
                sb.Append("            ").Append(prop.Name).Append(" = priv").Append(prop.Name).AppendLine(",");
            }

            sb.AppendLine("        };");
			sb.AppendLine("    }");
		}

		private static void AppendDeserializeProperty(StringBuilder sb, SerializableProperty prop)
		{
			switch (prop.TypeName)
			{
				case "int":
					sb.AppendLine("        if (offset + 4 > source.Length) throw new ArgumentException(\"Source buffer is too short.\", nameof(source));");
					sb.Append("        var priv").Append(prop.Name).AppendLine(" = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset));");
					sb.AppendLine("        offset += 4;");
					break;
				case "long":
					sb.AppendLine("        if (offset + 8 > source.Length) throw new ArgumentException(\"Source buffer is too short.\", nameof(source));");
					sb.Append("        var priv").Append(prop.Name).AppendLine(" = BinaryPrimitives.ReadInt64LittleEndian(source.Slice(offset));");
					sb.AppendLine("        offset += 8;");
					break;
				case "double":
					sb.AppendLine("        if (offset + 8 > source.Length) throw new ArgumentException(\"Source buffer is too short.\", nameof(source));");
					sb.Append("        var priv").Append(prop.Name).AppendLine(" = BinaryPrimitives.ReadDoubleLittleEndian(source.Slice(offset));");
					sb.AppendLine("        offset += 8;");
					break;
				case "bool":
					sb.AppendLine("        if (offset + 1 > source.Length) throw new ArgumentException(\"Source buffer is too short.\", nameof(source));");
					sb.Append("        var priv").Append(prop.Name).AppendLine(" = source[offset] != 0;");
					sb.AppendLine("        offset += 1;");
					break;
				case "DateTime":
					sb.AppendLine("        if (offset + 8 > source.Length) throw new ArgumentException(\"Source buffer is too short.\", nameof(source));");
					sb.Append("        var priv").Append(prop.Name).AppendLine(" = DateTime.FromBinary(BinaryPrimitives.ReadInt64LittleEndian(source.Slice(offset)));");
					sb.AppendLine("        offset += 8;");
					break;
				case "string":
					sb.AppendLine("        if (offset + 4 > source.Length) throw new ArgumentException(\"Source buffer is too short.\", nameof(source));");
					sb.Append("        var priv").Append(prop.Name).AppendLine("Len = BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset));");
					sb.AppendLine("        offset += 4;");
					sb.Append("        string priv").Append(prop.Name).AppendLine(";");
					sb.Append("        if (priv").Append(prop.Name).AppendLine("Len == -1)");
					sb.AppendLine("        {");
					sb.Append("            priv").Append(prop.Name).AppendLine(" = null;");
					sb.AppendLine("        }");
					sb.AppendLine("        else");
					sb.AppendLine("        {");
					sb.Append("            if (offset + priv").Append(prop.Name).AppendLine("Len > source.Length) throw new ArgumentException(\"Source buffer is too short.\", nameof(source));");
					sb.Append("            priv").Append(prop.Name).Append(" = System.Text.Encoding.UTF8.GetString(source.Slice(offset, priv").Append(prop.Name).AppendLine("Len));");
					sb.Append("            offset += priv").Append(prop.Name).AppendLine("Len;");
					sb.AppendLine("        }");
					break;
			}
		}

		private static void AppendDeserializeFromArray(StringBuilder sb, SerializableType type)
		{
			sb.Append("    public static ").Append(type.TypeName).AppendLine(" Deserialize(byte[] data)");
			sb.AppendLine("        => Deserialize(data.AsSpan());");
		}
	}
}
