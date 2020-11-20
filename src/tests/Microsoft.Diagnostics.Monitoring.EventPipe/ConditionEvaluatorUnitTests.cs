// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers;
using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions;
using Microsoft.Diagnostics.Tracing;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.UnitTests
{
    public class ConditionEvaluatorUnitTests
    {
        private readonly ITestOutputHelper _output;

        public ConditionEvaluatorUnitTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(ConditionEvaluatorTestData.SelectorTestData), MemberType = typeof(ConditionEvaluatorTestData))]
        public void PayloadSelectorTest(TraceEvent[] events)
        {
            bool[] expectedResults = Enumerable.Range(0, 10)
                .Select(i => i >= 5).ToArray();

            // Payload["Value"] >= 5
            var condition = new FilterConditionExpression(
                new SelectorExpression(
                    SelectorType.Payload,
                    nameof(ValueTypeEvent<int>.Value)),
                BinaryOperator.GreaterThanOrEqual,
                "5");

            var evaluator = new ConditionEvaluator(
                CounterFilter.AllCounters,
                condition,
                1);

            AssertResults(evaluator, events, expectedResults);

            // Modify events to test null payload
            SetValueNullable(events[0], null);
            SetValueNullable(events[6], null);
            expectedResults[6] = false;
            SetValueNullable(events[7], null);
            expectedResults[7] = false;

            // Payload["ValueNullable"] >= 5
            condition = new FilterConditionExpression(
                new SelectorExpression(
                    SelectorType.Payload,
                    nameof(ValueTypeEvent<int>.ValueNullable)),
                BinaryOperator.GreaterThanOrEqual,
                "5");
            evaluator = new ConditionEvaluator(
                CounterFilter.AllCounters,
                condition,
                1);

            AssertResults(evaluator, events, expectedResults);
        }

        [Theory]
        [MemberData(nameof(ConditionEvaluatorTestData.SelectorTestData), MemberType = typeof(ConditionEvaluatorTestData))]
        public void PropertySelectorTest(TraceEvent[] events)
        {
            bool[] expectedResults = Enumerable.Range(0, 10)
                .Select(i => i >= 5).ToArray();

            // Property["Value"] >= 5
            var condition = new FilterConditionExpression(
                new SelectorExpression(
                    SelectorType.Property,
                    nameof(ValueTypeEvent<int>.Value)),
                BinaryOperator.GreaterThanOrEqual,
                "5");

            var evaluator = new ConditionEvaluator(
                CounterFilter.AllCounters,
                condition,
                1);

            AssertResults(evaluator, events, expectedResults);

            // Modify events to test null properties
            SetValueNullable(events[0], null);
            SetValueNullable(events[6], null);
            expectedResults[6] = false;
            SetValueNullable(events[7], null);
            expectedResults[7] = false;

            // Property["ValueNullable"] >= 5
            condition = new FilterConditionExpression(
                new SelectorExpression(
                    SelectorType.Property,
                    nameof(ValueTypeEvent<int>.ValueNullable)),
                BinaryOperator.GreaterThanOrEqual,
                "5");

            evaluator = new ConditionEvaluator(
                CounterFilter.AllCounters,
                condition,
                1);

            AssertResults(evaluator, events, expectedResults);
        }

        [Theory]
        [MemberData(nameof(ConditionEvaluatorTestData.AverageTestData), MemberType = typeof(ConditionEvaluatorTestData))]
        public void AverageAggregateTest(TraceEvent[] events)
        {
            var expectedResults = Enumerable.Range(0, 10)
                .Select(i => i >= 7).ToArray();

            // Average(Payload[<propertyName>]) > 3
            var condition = new FilterConditionExpression(
                new AggregateExpression(
                    AggregateFunction.Average,
                    new SelectorExpression(
                        SelectorType.Payload,
                        nameof(ValueTypeEvent<int>.Value))
                ),
                BinaryOperator.GreaterThan,
                "3");

            var evaluator = new ConditionEvaluator(
                CounterFilter.AllCounters,
                condition,
                10);

            AssertResults(evaluator, events, expectedResults);

            // Average(Payload[<propertyName>]) > 3
            condition = new FilterConditionExpression(
                new AggregateExpression(
                    AggregateFunction.Average,
                    new SelectorExpression(
                        SelectorType.Payload,
                        nameof(ValueTypeEvent<int>.ValueNullable))
                ),
                BinaryOperator.GreaterThan,
                "3");

            evaluator = new ConditionEvaluator(
                CounterFilter.AllCounters,
                condition,
                10);

            AssertResults(evaluator, events, expectedResults);
        }

        private static void AssertResults(ConditionEvaluator evaluator, TraceEvent[] events, bool[] expected)
        {
            Assert.Equal(expected.Length, events.Length);
            for (int index = 0; index < events.Length; index++)
            {
                Assert.Equal(expected[index], evaluator.Evaluate(events[index]));
            }
        }

        private static void SetValueNullable(TraceEvent instance, object value)
        {
            instance.GetType().GetProperty(nameof(ValueTypeEvent<int>.ValueNullable)).SetValue(instance, value);
        }
    }
}
