using System;
using System.CodeDom.Compiler;
using System.IO;

using IssueDb.Querying.Syntax;

namespace IssueDb.Querying.Binding
{
    public abstract class BoundQuery
    {
        public static BoundQuery Create(QuerySyntax syntax)
        {
            var result = CreateInternal(syntax);
            return ToDisjunctiveNormalForm(result);
        }

        private static BoundQuery CreateInternal(QuerySyntax syntax)
        {
            switch (syntax.Kind)
            {
                case QuerySyntaxKind.TextQuery:
                    return CreateTextExpression((TextQuerySyntax)syntax);
                case QuerySyntaxKind.KeyValueQuery:
                    return CreateKeyValueExpression((KeyValueQuerySyntax)syntax);
                case QuerySyntaxKind.OrQuery:
                    return CreateOrExpression((OrQuerySyntax)syntax);
                case QuerySyntaxKind.AndQuery:
                    return CreateAndExpression((AndQuerySyntax)syntax);
                case QuerySyntaxKind.NegatedQuery:
                    return CreateNegatedExpression((NegatedQuerySyntax)syntax);
                case QuerySyntaxKind.ParenthesizedQuery:
                    return CreateParenthesizedExpression((ParenthesizedQuerySyntax)syntax);
                default:
                    throw new Exception($"Unexpected node {syntax.Kind}");
            }
        }

        private static BoundQuery CreateTextExpression(TextQuerySyntax node)
        {
            return new BoundTextQuery(false, node.TextToken.Value);
        }

        private static BoundQuery CreateKeyValueExpression(KeyValueQuerySyntax node)
        {
            var key = node.KeyToken.Value;
            var value = node.ValueToken.Value;
            return new BoundKeyValueQuery(isNegated: false, key, value);
        }

        private static BoundQuery CreateOrExpression(OrQuerySyntax node)
        {
            return new BoundOrQuery(CreateInternal(node.Left), CreateInternal(node.Right));
        }

        private static BoundQuery CreateAndExpression(AndQuerySyntax node)
        {
            return new BoundAndQuery(CreateInternal(node.Left), CreateInternal(node.Right));
        }

        private static BoundQuery CreateNegatedExpression(NegatedQuerySyntax node)
        {
            return new BoundNegatedQuery(CreateInternal(node.Query));
        }

        private static BoundQuery CreateParenthesizedExpression(ParenthesizedQuerySyntax node)
        {
            return CreateInternal(node.Query);
        }

        private static BoundQuery ToDisjunctiveNormalForm(BoundQuery node)
        {
            if (node is BoundNegatedQuery negated)
                return ToDisjunctiveNormalForm(Negate(negated.Query));

            if (node is BoundOrQuery or)
            {
                var left = ToDisjunctiveNormalForm(or.Left);
                var right = ToDisjunctiveNormalForm(or.Right);
                if (ReferenceEquals(left, or.Left) &&
                    ReferenceEquals(right, or.Right))
                    return node;

                return new BoundOrQuery(left, right);
            }

            if (node is BoundAndQuery and)
            {
                var left = ToDisjunctiveNormalForm(and.Left);
                var right = ToDisjunctiveNormalForm(and.Right);

                // (A OR B) AND C      ->    (A AND C) OR (B AND C)

                if (left is BoundOrQuery leftOr)
                {
                    var a = leftOr.Left;
                    var b = leftOr.Right;
                    var c = right;
                    return new BoundOrQuery(
                        ToDisjunctiveNormalForm(new BoundAndQuery(a, c)),
                        ToDisjunctiveNormalForm(new BoundAndQuery(b, c))
                    );
                }

                // A AND (B OR C)      ->    (A AND B) OR (A AND C)

                if (right is BoundOrQuery rightOr)
                {
                    var a = left;
                    var b = rightOr.Left;
                    var c = rightOr.Right;
                    return new BoundOrQuery(
                        ToDisjunctiveNormalForm(new BoundAndQuery(a, b)),
                        ToDisjunctiveNormalForm(new BoundAndQuery(a, c))
                    );
                }

                return new BoundAndQuery(left, right);
            }

            return node;
        }

        private static BoundQuery Negate(BoundQuery node)
        {
            switch (node)
            {
                case BoundKeyValueQuery kevValue:
                    return NegateKevValueExpression(kevValue);
                case BoundTextQuery text:
                    return NegateTextExpression(text);
                case BoundNegatedQuery negated:
                    return NegateNegatedExpression(negated);
                case BoundAndQuery and:
                    return NegateAndExpression(and);
                case BoundOrQuery or:
                    return NegateOrExpression(or);
                default:
                    throw new Exception($"Unexpected node {node.GetType()}");
            }
        }

        private static BoundQuery NegateKevValueExpression(BoundKeyValueQuery node)
        {
            return new BoundKeyValueQuery(!node.IsNegated, node.Key, node.Value);
        }

        private static BoundQuery NegateTextExpression(BoundTextQuery node)
        {
            return new BoundTextQuery(!node.IsNegated, node.Text);
        }

        private static BoundQuery NegateNegatedExpression(BoundNegatedQuery node)
        {
            return node.Query;
        }

        private static BoundQuery NegateAndExpression(BoundAndQuery node)
        {
            return new BoundOrQuery(Negate(node.Left), Negate(node.Right));
        }

        private static BoundQuery NegateOrExpression(BoundOrQuery node)
        {
            return new BoundAndQuery(Negate(node.Left), Negate(node.Right));
        }

        public override string ToString()
        {
            using var stringWriter = new StringWriter();
            {
                using var indentedTextWriter = new IndentedTextWriter(stringWriter);
                Walk(indentedTextWriter, this);

                return stringWriter.ToString();
            }

            static void Walk(IndentedTextWriter writer, BoundQuery node)
            {
                switch (node)
                {
                    case BoundKeyValueQuery kevValue:
                        writer.WriteLine($"{(kevValue.IsNegated ? "-" : "")}{kevValue.Key}:{kevValue.Value}");
                        break;
                    case BoundTextQuery text:
                        writer.WriteLine($"{(text.IsNegated ? "-" : "")}{text.Text}");
                        break;
                    case BoundNegatedQuery negated:
                        writer.WriteLine("NOT");
                        writer.Indent++;
                        Walk(writer, negated.Query);
                        writer.Indent--;
                        break;
                    case BoundAndQuery and:
                        writer.WriteLine("AND");
                        writer.Indent++;
                        Walk(writer, and.Left);
                        Walk(writer, and.Right);
                        writer.Indent--;
                        break;
                    case BoundOrQuery or:
                        writer.WriteLine("OR");
                        writer.Indent++;
                        Walk(writer, or.Left);
                        Walk(writer, or.Right);
                        writer.Indent--;
                        break;
                    default:
                        throw new Exception($"Unexpected node {node.GetType()}");
                }
            }
        }
    }
}
