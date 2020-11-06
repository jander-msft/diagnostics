// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions
{
    internal sealed class DebugDisplayVisitor :
        ExpressionNodeVisitor<ExpressionNode>
    {
        private readonly StringBuilder _builder;

        public DebugDisplayVisitor(StringBuilder builder)
        {
            _builder = builder;
        }

        public override ExpressionNode VisitAggregate(AggregateExpression expression)
        {
            if (null == expression.Selector)
            {
                Invalid();
            }
            else
            {
                _builder.Append(expression.Function);
                _builder.Append("(");
                expression.Selector.Accept(this);
                if (null != expression.Filter)
                {
                    _builder.Append(", ");
                    expression.Filter.Accept(this);
                }
                _builder.Append(")");
            }

            return expression;
        }

        public override ExpressionNode VisitCount(CountExpression expression)
        {
            _builder.Append("Count(");
            if (null != expression.Filter)
            {
                expression.Filter.Accept(this);
            }
            _builder.Append(")");

            return expression;
        }

        public override ExpressionNode VisitFilterCondition(FilterConditionExpression expression)
        {
            if (null == expression.Expression)
            {
                Invalid();
            }
            else
            {
                _builder.Append("(");
                expression.Expression.Accept(this);
                _builder.Append(" ");
                _builder.Append(ToString(expression.Operator));
                _builder.Append(" ");
                _builder.Append(expression.Value ?? "<null>");
                _builder.Append(")");
            }

            return expression;
        }

        public override ExpressionNode VisitLogicalCondition(LogicalConditionExpression expression)
        {
            if (null == expression.Left || null == expression.Right)
            {
                Invalid();
            }
            else
            {
                _builder.Append("(");
                expression.Left.Accept(this);
                _builder.Append(" ");
                _builder.Append(ToString(expression.Operator));
                _builder.Append(" ");
                expression.Right.Accept(this);
                _builder.Append(")");
            }

            return expression;
        }

        public override ExpressionNode VisitLogicalFilter(LogicalFilterExpression expression)
        {
            if (null == expression.Left || null == expression.Right)
            {
                Invalid();
            }
            else
            {
                _builder.Append("(");
                expression.Left.Accept(this);
                _builder.Append(" ");
                _builder.Append(ToString(expression.Operator));
                _builder.Append(" ");
                expression.Right.Accept(this);
                _builder.Append(")");
            }

            return expression;
        }

        public override ExpressionNode VisitSelector(SelectorExpression expression)
        {
            if (null == expression.Name)
            {
                Invalid();
            }
            else
            {
                _builder.Append(expression.Type);
                _builder.Append("[");
                _builder.Append(expression.Name);
                _builder.Append("]");
            }

            return expression;
        }

        public override ExpressionNode VisitSelectorFilter(SelectorFilterExpression expression)
        {
            if (null == expression.Expression)
            {
                Invalid();
            }
            else
            {
                _builder.Append("(");
                expression.Expression.Accept(this);
                _builder.Append(" ");
                _builder.Append(ToString(expression.Operator));
                _builder.Append(" ");
                _builder.Append(expression.Value ?? "<null>");
                _builder.Append(")");
            }

            return expression;
        }

        private void Invalid()
        {
            _builder.Append("<INVALID>");
        }

        private static string ToString(BinaryOperator binaryOperator)
        {
            switch (binaryOperator)
            {
                case BinaryOperator.Equal:
                    return "==";
                case BinaryOperator.GreaterThan:
                    return ">";
                case BinaryOperator.GreaterThanOrEqual:
                    return ">=";
                case BinaryOperator.LessThan:
                    return "<";
                case BinaryOperator.LessThanOrEqual:
                    return "<=";
                case BinaryOperator.NotEqual:
                    return "!=";
            }
            throw new NotSupportedException($"Binary operator {binaryOperator} was not handled.");
        }

        private static string ToString(LogicalOperator logicalOperator)
        {
            switch (logicalOperator)
            {
                case LogicalOperator.And:
                    return "&&";
                case LogicalOperator.Or:
                    return "||";
            }
            throw new NotSupportedException($"Logical operator {logicalOperator} was not handled.");
        }
    }
}
