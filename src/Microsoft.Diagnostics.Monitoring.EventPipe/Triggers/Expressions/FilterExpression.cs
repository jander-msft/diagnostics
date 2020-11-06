// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions
{
    internal abstract class FilterExpression : ExpressionNode
    {
    }

    internal sealed class LogicalFilterExpression : FilterExpression
    {
        public LogicalFilterExpression(LogicalOperator logicalOperator, FilterExpression left, FilterExpression right)
        {
            Operator = logicalOperator;
            Left = left;
            Right = right;
        }

        public override T Accept<T>(ExpressionNodeVisitor<T> visitor)
        {
            return visitor.VisitLogicalFilter(this);
        }

        public LogicalOperator Operator { get; }

        public FilterExpression Left { get; }

        public FilterExpression Right { get; }
    }

    internal sealed class SelectorFilterExpression : FilterExpression
    {
        public SelectorFilterExpression(SelectorExpression expression, BinaryOperator binaryOperator, string value)
        {
            Expression = expression;
            Operator = binaryOperator;
            Value = value;
        }

        public override T Accept<T>(ExpressionNodeVisitor<T> visitor)
        {
            return visitor.VisitSelectorFilter(this);
        }

        public SelectorExpression Expression { get; }

        public BinaryOperator Operator { get; }

        public string Value { get; }
    }
}
