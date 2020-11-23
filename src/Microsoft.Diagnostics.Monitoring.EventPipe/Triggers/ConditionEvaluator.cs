// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal class ConditionEvaluator : EventEvaluator
    {
        private readonly ConditionExpression _expression;
        private readonly int _maxItemCount;

        private ConditionDelegate _condition;
        private Queue<TraceEventData> _items = new Queue<TraceEventData>();

        public ConditionEvaluator(ConditionExpression expression, int maxItemCount, string providerName, string eventName)
            : this(expression, maxItemCount, providerName, eventName, null, 0)
        {
        }

        public ConditionEvaluator(ConditionExpression expression, int maxItemCount, string providerName, string counterName, int counterIntervalMSec)
            : this(expression, maxItemCount, providerName, "EventCounters", counterName, counterIntervalMSec)
        {
        }

        public ConditionEvaluator(ConditionExpression expression, int maxItemCount, string providerName, string eventName, string counterName, int counterIntervalMSec)
            : base(providerName, eventName, counterName, counterIntervalMSec)
        {
            _expression = expression ?? throw new ArgumentNullException(nameof(expression));
            _maxItemCount = maxItemCount;
        }

        public override bool EvaluateCore(TraceEventData data)
        {
            _items.Enqueue(data);
            if (_items.Count > _maxItemCount)
            {
                _items.Dequeue();
            }

            if (null == _condition && !TryCompile(data, out _condition))
            {
                return false;
            }

            return _condition(_items, data);
        }

        private bool TryCompile(TraceEventData sample, out ConditionDelegate condition)
        {
            try
            {
                condition = ConditionCompiler.Compile(_expression, sample);

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
