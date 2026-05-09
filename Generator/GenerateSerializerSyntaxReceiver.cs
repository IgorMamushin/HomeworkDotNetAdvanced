using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Demo.Generators
{
	internal sealed class GenerateSerializerSyntaxReceiver : ISyntaxReceiver
	{
		public List<ClassDeclarationSyntax> Candidates { get; } = new();

		public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
		{
			var cls = syntaxNode as ClassDeclarationSyntax;
			if (cls != null && cls.AttributeLists.Count > 0)
			{
				Candidates.Add(cls);
			}
		}
	}
}
