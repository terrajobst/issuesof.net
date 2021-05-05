using System;
using System.CodeDom.Compiler;
using System.IO;

namespace IssuesOfDotNet.Querying
{
    public abstract class BoundExpression
    {
        public static BoundExpression Create(ExpressionSyntax syntax)
        {
            var result = CreateInternal(syntax);
            return ToDisjunctiveNormalForm(result);
        }

        private static BoundExpression CreateInternal(ExpressionSyntax syntax)
        {
            switch (syntax.Kind)
            {
                case QuerySyntaxKind.TextExpression:
                    return CreateTextExpression((TextExpressionSyntax)syntax);
                case QuerySyntaxKind.KeyValueExpression:
                    return CreateKeyValueExpression((KeyValueExpressionSyntax)syntax);
                case QuerySyntaxKind.OrExpression:
                    return CreateOrExpression((OrExpressionSyntax)syntax);
                case QuerySyntaxKind.AndExpression:
                    return CreateAndExpression((AndExpressionSyntax)syntax);
                case QuerySyntaxKind.NegatedExpression:
                    return CreateNegatedExpression((NegatedExpressionSyntax)syntax);
                case QuerySyntaxKind.ParenthesizedExpression:
                    return CreateParenthesizedExpression((ParenthesizedExpressionSyntax)syntax);
                default:
                    throw new Exception($"Unexpected node {syntax.Kind}");
            }
        }

        private static BoundExpression CreateTextExpression(TextExpressionSyntax node)
        {
            return new BoundTextExpression(false, node.TextToken.Value);
        }

        private static BoundExpression CreateKeyValueExpression(KeyValueExpressionSyntax node)
        {
            var key = node.KeyToken.Value;
            var value = node.ValueToken.Value;
            return new BoundKevValueExpression(isNegated: false, key, value);
        }

        private static BoundExpression CreateOrExpression(OrExpressionSyntax node)
        {
            return new BoundOrExpression(CreateInternal(node.Left), CreateInternal(node.Right));
        }

        private static BoundExpression CreateAndExpression(AndExpressionSyntax node)
        {
            return new BoundAndExpression(CreateInternal(node.Left), CreateInternal(node.Right));
        }

        private static BoundExpression CreateNegatedExpression(NegatedExpressionSyntax node)
        {
            return new BoundNegatedExpression(CreateInternal(node.Expression));
        }

        private static BoundExpression CreateParenthesizedExpression(ParenthesizedExpressionSyntax node)
        {
            return CreateInternal(node.Expression);
        }

        private static BoundExpression ToDisjunctiveNormalForm(BoundExpression node)
        {
            if (node is BoundNegatedExpression negated)
                return ToDisjunctiveNormalForm(Negate(negated.Expression));

            if (node is BoundAndExpression and)
            {
                var left = ToDisjunctiveNormalForm(and.Left);
                var right = ToDisjunctiveNormalForm(and.Right);

                // (A OR B) AND C      ->    (A AND C) OR (B AND C)

                if (left is BoundOrExpression leftOr)
                {
                    var a = leftOr.Left;
                    var b = leftOr.Right;
                    var c = right;
                    return new BoundOrExpression(
                        ToDisjunctiveNormalForm(new BoundAndExpression(a, c)),
                        ToDisjunctiveNormalForm(new BoundAndExpression(b, c))
                    );
                }

                // A AND (B OR C)      ->    (A AND B) OR (A AND C)

                if (right is BoundOrExpression rightOr)
                {
                    var a = left;
                    var b = rightOr.Left;
                    var c = rightOr.Right;
                    return new BoundOrExpression(
                        ToDisjunctiveNormalForm(new BoundAndExpression(a, b)),
                        ToDisjunctiveNormalForm(new BoundAndExpression(a, c))
                    );
                }

                return new BoundAndExpression(left, right);
            }

            return node;
        }

        private static BoundExpression Negate(BoundExpression node)
        {
            switch (node)
            {
                case BoundKevValueExpression kevValueExpression:
                    return NegateKevValueExpression(kevValueExpression);
                case BoundTextExpression textExpression:
                    return NegateTextExpression(textExpression);
                case BoundNegatedExpression negatedExpression:
                    return NegateNegatedExpression(negatedExpression);
                case BoundAndExpression andExpression:
                    return NegateAndExpression(andExpression);
                case BoundOrExpression orExpression:
                    return NegateOrExpression(orExpression);
                default:
                    throw new Exception($"Unexpected node {node.GetType()}");
            }
        }

        private static BoundExpression NegateKevValueExpression(BoundKevValueExpression node)
        {
            return new BoundKevValueExpression(!node.IsNegated, node.Key, node.Value);
        }

        private static BoundExpression NegateTextExpression(BoundTextExpression node)
        {
            return new BoundTextExpression(!node.IsNegated, node.Text);
        }

        private static BoundExpression NegateNegatedExpression(BoundNegatedExpression node)
        {
            return node.Expression;
        }

        private static BoundExpression NegateAndExpression(BoundAndExpression node)
        {
            return new BoundOrExpression(Negate(node.Left), Negate(node.Right));
        }

        private static BoundExpression NegateOrExpression(BoundOrExpression node)
        {
            return new BoundAndExpression(Negate(node.Left), Negate(node.Right));
        }

        public override string ToString()
        {
            using var stringWriter = new StringWriter();
            {
                using var indentedTextWriter = new IndentedTextWriter(stringWriter);
                    Walk(indentedTextWriter, this);

                return stringWriter.ToString();
            }

            static void Walk(IndentedTextWriter writer, BoundExpression node)
            {
                switch (node)
                {
                    case BoundKevValueExpression kevValueExpression:
                        writer.WriteLine($"{(kevValueExpression.IsNegated ? "-" : "")}{kevValueExpression.Key}:{kevValueExpression.Value}");
                        break;
                    case BoundTextExpression termExpression:
                        writer.WriteLine($"{(termExpression.IsNegated ? "-" : "")}{termExpression.Text}");
                        break;
                    case BoundNegatedExpression negatedExpression:
                        writer.WriteLine("NOT");
                        writer.Indent++;
                        Walk(writer, negatedExpression.Expression);
                        writer.Indent--;
                        break;
                    case BoundAndExpression andExpression:
                        writer.WriteLine("AND");
                        writer.Indent++;
                        Walk(writer, andExpression.Left);
                        Walk(writer, andExpression.Right);
                        writer.Indent--;
                        break;
                    case BoundOrExpression orExpression:
                        writer.WriteLine("OR");
                        writer.Indent++;
                        Walk(writer, orExpression.Left);
                        Walk(writer, orExpression.Right);
                        writer.Indent--;
                        break;
                    default:
                        throw new Exception($"Unexpected node {node.GetType()}");
                }
            }
        }
    }
}
