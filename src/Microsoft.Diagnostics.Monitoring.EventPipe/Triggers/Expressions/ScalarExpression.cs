// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions
{
    internal abstract class ScalarExpression : ExpressionNode
    {
    }

    internal sealed class SelectorExpression : ScalarExpression
    {
        public SelectorExpression(SelectorType type, string name)
        {
            Type = type;
            Name = name;
        }

        public override T Accept<T>(ExpressionNodeVisitor<T> visitor)
        {
            return visitor.VisitSelector(this);
        }

        public SelectorType Type { get; }

        public string Name { get; }
    }

    internal sealed class AggregateExpression : ScalarExpression
    {
        public AggregateExpression(AggregateFunction function, SelectorExpression selector)
            : this(function, selector, null)
        {
        }

        public AggregateExpression(AggregateFunction function, SelectorExpression selector, FilterExpression filter)
        {
            Function = function;
            Selector = selector;
            Filter = filter;
        }

        public override T Accept<T>(ExpressionNodeVisitor<T> visitor)
        {
            return visitor.VisitAggregate(this);
        }

        public AggregateFunction Function { get; }

        public SelectorExpression Selector { get; }

        public FilterExpression Filter { get; }
    }

    internal sealed class CountExpression : ScalarExpression
    {
        public CountExpression()
            : this(null)
        {
        }

        public CountExpression(FilterExpression filter)
        {
            Filter = filter;
        }

        public override T Accept<T>(ExpressionNodeVisitor<T> visitor)
        {
            return visitor.VisitCount(this);
        }

        public FilterExpression Filter { get; }
    }
}
