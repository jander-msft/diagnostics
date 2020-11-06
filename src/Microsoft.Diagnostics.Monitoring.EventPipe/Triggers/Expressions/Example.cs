using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions
{
    class Example
    {
        public static FilterConditionExpression CpuAvgOver80()
        {
            // event.Payload["Mean"]
            SelectorExpression selector = new SelectorExpression(SelectorType.Payload, "Mean");

            // Average(data, event => event.Payload["Mean"])
            AggregateExpression avg = new AggregateExpression(AggregateFunction.Average, selector);

            // Average(data, event => event.Payload["Mean"]) >= 80
            return new FilterConditionExpression(avg, BinaryOperator.GreaterThanOrEqual, "80");
        }

        public static FilterConditionExpression InducedGC()
        {
            // event.Payload["Reason"]
            SelectorExpression selector = new SelectorExpression(SelectorType.Payload, "Reason");

            // event.Payload["Reason"] == "Induced"
            return new FilterConditionExpression(selector, BinaryOperator.Equal, "Induced");
        }

        public static FilterConditionExpression NonNativeModules()
        {
            // event.Payload["ModuleFlags"]
            SelectorExpression selector = new SelectorExpression(SelectorType.Payload, "MoudleFlags");

            // event.Payload["ModuleFlags"] != "Native"
            SelectorFilterExpression filter = new SelectorFilterExpression(selector, BinaryOperator.NotEqual, "Native");

            // Count(event => event.Payload["ModuleFlags"] != "Native")
            CountExpression count = new CountExpression(filter);

            // Count(event => event.Payload["ModuleFlags"] != "Native") >= 10
            return new FilterConditionExpression(count, BinaryOperator.GreaterThanOrEqual, "10");
        }
    }
}
