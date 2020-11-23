// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.UnitTests
{
    internal class ConditionEvaluatorTestData
    {
        private static TraceEvent[] GenerateValueTypeEvents<T>(string[] values) where T : struct
        {
            TraceEvent[] events = new TraceEvent[values.Length];

            for (int index = 0; index < values.Length; index++)
            {
                T value = (T)Convert.ChangeType(values[index], typeof(T));

                ValueTypeEvent<T> instance = new ValueTypeEvent<T>();
                instance.Value = value;
                instance.ValueNullable = value;

                events[index] = instance;
            }

            return events;
        }

        public static IEnumerable<object[]> SelectorTestData()
        {
            string[] values = new string[10];
            for (int index = 0; index < 10; index++)
            {
                values[index] = index.ToString(CultureInfo.InvariantCulture);
            }

            yield return new object[]
            {
                GenerateValueTypeEvents<sbyte>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<short>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<int>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<long>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<byte>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<ushort>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<uint>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<ulong>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<float>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<double>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<decimal>(values)
            };
        }

        public static IEnumerable<object[]> AverageTestData()
        {
            string[] values = new string[10];
            for (int index = 0; index < 10; index++)
            {
                values[index] = index.ToString(CultureInfo.InvariantCulture);
            }

            yield return new object[]
            {
                GenerateValueTypeEvents<int>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<long>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<float>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<double>(values)
            };
            yield return new object[]
            {
                GenerateValueTypeEvents<decimal>(values)
            };
        }
    }

    internal class ValueTypeEvent<T> : TraceEvent where T : struct
    {
        private static readonly string[] s_payloadNames = new[]
        {
            nameof(Value),
            nameof(ValueNullable)
        };

        private Action _action;

        public ValueTypeEvent()
            : base(0, 0, nameof(ValueTypeEvent<int>), Guid.NewGuid(), 0, null, Guid.NewGuid(), nameof(ValueTypeEvent<int>) + "Provider")
        {
        }

        public override string[] PayloadNames => s_payloadNames;

        protected override Delegate Target { get => _action; set => _action = (Action)value; }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0: return Value;
                case 1: return ValueNullable;
            }
            return null;
        }

        public T Value { get; set; }

        public T? ValueNullable { get; set; }
    }

    internal class ReferenceTypeEvent<T> : TraceEvent where T : class
    {
        private static readonly string[] s_payloadNames = new[]
        {
            nameof(Value)
        };

        private Action _action;

        public ReferenceTypeEvent()
            : base(0, 0, null, Guid.NewGuid(), 0, null, Guid.NewGuid(), null)
        {
        }

        public override string[] PayloadNames => s_payloadNames;

        protected override Delegate Target { get => _action; set => _action = (Action)value; }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0: return Value;
            }
            return null;
        }

        public T Value { get; set; }
    }
}
