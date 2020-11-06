// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions;
using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal class ConditionEvaluator
    {
        private readonly ConditionExpression _conditionExpr;
        private readonly CounterFilter _filter;
        private readonly int _maxItemCount;

        private ConditionDelegate _condition;
        private Queue<TraceEventData> _items = new Queue<TraceEventData>();

        public ConditionEvaluator(CounterFilter filter, ConditionExpression conditionExpr, int maxItemCount)
        {
            _conditionExpr = conditionExpr ?? throw new ArgumentNullException(nameof(conditionExpr));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            _maxItemCount = maxItemCount;
        }

        public bool Evaluate(TraceEvent value)
        {
            var item = TraceEventData.Create(value,  _filter);

            _items.Enqueue(item);
            if (_items.Count > _maxItemCount)
            {
                _items.Dequeue();
            }

            if (null == _condition && !TryCompile(item, out _condition))
            {
                return false;
            }

            return _condition(_items, item);
        }

        private bool TryCompile(TraceEventData sample, out ConditionDelegate condition)
        {
            try
            {
                condition = ConditionCompiler.Compile(_conditionExpr, sample);

                return true;
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
                condition = null;
                return false;
            }
        }
    }
}
