// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions
{
    internal abstract class ConditionExpression : ExpressionNode
    {
    }

    internal sealed class LogicalConditionExpression : ConditionExpression
    {
        public LogicalConditionExpression(LogicalOperator logicalOperator, ConditionExpression left, ConditionExpression right)
        {
            Operator = logicalOperator;
            Left = left;
            Right = right;
        }

        public override T Accept<T>(ExpressionNodeVisitor<T> visitor)
        {
            return visitor.VisitLogicalCondition(this);
        }

        public LogicalOperator Operator { get; }

        public ConditionExpression Left { get; }

        public ConditionExpression Right { get; }
    }

    internal sealed class FilterConditionExpression : ConditionExpression
    {
        public FilterConditionExpression(ScalarExpression expression, BinaryOperator binaryOperator, string value)
        {
            Expression = expression;
            Operator = binaryOperator;
            Value = value;
        }

        public override T Accept<T>(ExpressionNodeVisitor<T> visitor)
        {
            return visitor.VisitFilterCondition(this);
        }

        public ScalarExpression Expression { get; }

        public BinaryOperator Operator { get; }

        public string Value { get; }
    }
}
