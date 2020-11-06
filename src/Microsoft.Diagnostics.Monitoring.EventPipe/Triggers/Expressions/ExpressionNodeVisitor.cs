// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions
{
    internal abstract class ExpressionNodeVisitor<T>
    {
        public virtual T VisitAggregate(AggregateExpression expression)
        {
            return default(T);
        }

        public virtual T VisitCount(CountExpression expression)
        {
            return default(T);
        }

        public virtual T VisitFilterCondition(FilterConditionExpression expression)
        {
            return default(T);
        }

        public virtual T VisitLogicalCondition(LogicalConditionExpression expression)
        {
            return default(T);
        }

        public virtual T VisitLogicalFilter(LogicalFilterExpression expression)
        {
            return default(T);
        }

        public virtual T VisitSelector(SelectorExpression expression)
        {
            return default(T);
        }

        public virtual T VisitSelectorFilter(SelectorFilterExpression expression)
        {
            return default(T);
        }
    }
}
