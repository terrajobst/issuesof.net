using System;
using System.CodeDom.Compiler;
using System.IO;

namespace IssuesOfDotNet.Querying
{
    public abstract class ExpressionSyntax : QueryNodeOrToken
    {
        public override string ToString()
        {
            using (var stringWriter = new StringWriter())
            {
                using (var indentedTextWriter = new IndentedTextWriter(stringWriter))
                    Walk(indentedTextWriter, this);

                return stringWriter.ToString();
            }

            static void Walk(IndentedTextWriter writer, ExpressionSyntax node)
            {
                switch (node.Kind)
                {
                    case QuerySyntaxKind.TextExpression:
                        WalkTextExpression(writer, (TextExpressionSyntax)node);
                        break;
                    case QuerySyntaxKind.KeyValueExpression:
                        WalkKeyValueExpression(writer, (KeyValueExpressionSyntax)node);
                        break;
                    case QuerySyntaxKind.OrExpression:
                        WalkOrExpression(writer, (OrExpressionSyntax)node);
                        break;
                    case QuerySyntaxKind.AndExpression:
                        WalkAndExpression(writer, (AndExpressionSyntax)node);
                        break;
                    case QuerySyntaxKind.NegatedExpression:
                        WalkNegatedExpression(writer, (NegatedExpressionSyntax)node);
                        break;
                    case QuerySyntaxKind.ParenthesizedExpression:
                        WalkParenthesizedExpression(writer, (ParenthesizedExpressionSyntax)node);
                        break;
                    default:
                        throw new Exception($"Unexpected node: {node.Kind}");
                }
            }

            static void WalkTextExpression(IndentedTextWriter writer, TextExpressionSyntax node)
            {
                writer.WriteLine(node.TextToken);
            }

            static void WalkKeyValueExpression(IndentedTextWriter writer, KeyValueExpressionSyntax node)
            {
                writer.Write(node.KeyToken);
                writer.Write(" ");
                writer.Write(node.ColonToken);
                writer.Write(" ");
                writer.Write(node.ValueToken);
                writer.WriteLine();
            }

            static void WalkOrExpression(IndentedTextWriter writer, OrExpressionSyntax node)
            {
                writer.WriteLine("OR");

                writer.Indent++;
                Walk(writer, node.Left);
                Walk(writer, node.Right);
                writer.Indent--;
            }

            static void WalkAndExpression(IndentedTextWriter writer, AndExpressionSyntax node)
            {
                writer.WriteLine("AND");

                writer.Indent++;
                Walk(writer, node.Left);
                Walk(writer, node.Right);
                writer.Indent--;
            }

            static void WalkNegatedExpression(IndentedTextWriter writer, NegatedExpressionSyntax node)
            {
                writer.WriteLine("NOT");

                writer.Indent++;
                Walk(writer, node.Expression);
                writer.Indent--;
            }

            static void WalkParenthesizedExpression(IndentedTextWriter writer, ParenthesizedExpressionSyntax node)
            {
                writer.WriteLine("(");

                writer.Indent++;
                Walk(writer, node.Expression);
                writer.Indent--;

                writer.WriteLine(")");
            }
        }
    }
}
