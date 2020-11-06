// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Expressions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers
{
    internal sealed class ConditionCompiler
    {
        private static readonly Type EnumerableTraceEventDataType =
            typeof(IEnumerable<>).MakeGenericType(typeof(TraceEventData));

        private static readonly MethodInfo WhereTraceEventDataMethod =
            EnumerableMethods.Where().MakeGenericMethod(typeof(TraceEventData));

        public static ConditionDelegate Compile(ConditionExpression expression, TraceEventData sample)
        {
            ParameterExpression itemsParam = Expression.Parameter(EnumerableTraceEventDataType, "items");
            ParameterExpression itemParam = Expression.Parameter(typeof(TraceEventData), "item");

            CompilerVisitor visitor = new CompilerVisitor(sample, itemsParam, itemParam);
            Expression body = expression.Accept(visitor);

            Expression<ConditionDelegate> condition = Expression.Lambda<ConditionDelegate>(
                body,
                itemsParam,
                itemParam);

            return condition.Compile();
        }

        internal sealed class CompilerVisitor :
            ExpressionNodeVisitor<Expression>
        {
            private readonly TraceEventData _sample;

            private CompilationContext _context;

            public CompilerVisitor(TraceEventData sample, ParameterExpression itemsParam, ParameterExpression itemParam)
            {
                _context = new CompilationContext(itemsParam, itemParam);
                _sample = sample;
            }

            public override Expression VisitAggregate(AggregateExpression expression)
            {
                LambdaExpression selector = VisitAsLambda(expression.Selector);

                LambdaExpression filter = null;
                if (null != expression.Filter)
                {
                    filter = VisitAsLambda(expression.Filter);
                }

                switch (expression.Function)
                {
                    case AggregateFunction.Average:
                        return CreateAverage(selector, filter);
                    case AggregateFunction.Max:
                        return CreateMax(selector, filter);
                    case AggregateFunction.Min:
                        return CreateMin(selector, filter);
                    case AggregateFunction.Sum:
                        return CreateSum(selector, filter);
                }

                throw new NotImplementedException();
            }

            private Expression CreateAverage(LambdaExpression selector, LambdaExpression filter)
            {
                // CONSIDER: Enumerable.Average doesn't support types outside of
                // int, long, float, double, and decimal and their nullabe versions.
                // Consider add simulated support for the other integral types
                // (byte, sbyte, short, ushort, uint, ulong) and their nullable versions.
                MethodInfo averageMethod = EnumerableMethods
                    .AverageWithSelector(selector.ReturnType)
                    .MakeGenericMethod(typeof(TraceEventData));

                Expression source;
                if (null == filter)
                {
                    source = ItemsParameter;
                }
                else
                {
                    source = Expression.Call(
                        null,
                        WhereTraceEventDataMethod,
                        ItemsParameter,
                        filter);
                }

                return Expression.Call(
                    null,
                    averageMethod,
                    source,
                    selector);
            }

            private Expression CreateMax(LambdaExpression selector, LambdaExpression filter)
            {
                MethodInfo maxMethod = EnumerableMethods.MaxWithSelector(selector.ReturnType);
                if (null != maxMethod)
                {
                    maxMethod = maxMethod.MakeGenericMethod(typeof(TraceEventData));
                }
                else
                {
                    maxMethod = EnumerableMethods
                        .MaxWithSelector()
                        .MakeGenericMethod(typeof(TraceEventData), selector.ReturnType);
                }

                Expression source;
                if (null == filter)
                {
                    source = ItemsParameter;
                }
                else
                {
                    source = Expression.Call(
                        null,
                        WhereTraceEventDataMethod,
                        ItemsParameter,
                        filter);
                }

                return Expression.Call(
                    null,
                    maxMethod,
                    source,
                    selector);
            }

            private Expression CreateMin(LambdaExpression selector, LambdaExpression filter)
            {
                MethodInfo minMethod = EnumerableMethods.MinWithSelector(selector.ReturnType);
                if (null != minMethod)
                {
                    minMethod = minMethod.MakeGenericMethod(typeof(TraceEventData));
                }
                else
                {
                    minMethod = EnumerableMethods
                        .MinWithSelector()
                        .MakeGenericMethod(typeof(TraceEventData), selector.ReturnType);
                }

                Expression source;
                if (null == filter)
                {
                    source = ItemsParameter;
                }
                else
                {
                    source = Expression.Call(
                        null,
                        WhereTraceEventDataMethod,
                        ItemsParameter,
                        filter);
                }

                return Expression.Call(
                    null,
                    minMethod,
                    source,
                    selector);
            }

            private Expression CreateSum(LambdaExpression selector, LambdaExpression filter)
            {
                throw new NotImplementedException();
            }

            public override Expression VisitCount(CountExpression expression)
            {
                if (null == expression.Filter)
                {
                    return Expression.Call(
                        null,
                        EnumerableMethods.Count().MakeGenericMethod(typeof(TraceEventData)),
                        ItemsParameter);
                }
                else
                {
                    return Expression.Call(
                        null,
                        EnumerableMethods.CountWithFilter().MakeGenericMethod(typeof(TraceEventData)),
                        ItemsParameter,
                        VisitAsLambda(expression.Filter));
                }
            }

            public override Expression VisitFilterCondition(FilterConditionExpression expression)
            {
                var left = VisitAsExpression(expression.Expression);

                return Expression.MakeBinary(
                    AsExpressionType(expression.Operator),
                    left,
                    ConvertConstant(expression.Value, left.Type));
            }

            public override Expression VisitLogicalCondition(LogicalConditionExpression expression)
            {
                return Expression.MakeBinary(
                    AsExpressionType(expression.Operator),
                    VisitAsExpression(expression.Left),
                    VisitAsExpression(expression.Right));
            }

            public override Expression VisitLogicalFilter(LogicalFilterExpression expression)
            {
                return Expression.MakeBinary(
                    AsExpressionType(expression.Operator),
                    VisitAsExpression(expression.Left),
                    VisitAsExpression(expression.Right)
                    );
            }

            public override Expression VisitSelector(SelectorExpression expression)
            {
                switch (expression.Type)
                {
                    case SelectorType.Payload:
                        return CreatePayloadSelector(expression.Name);
                    case SelectorType.Property:
                        return CreatePropertySelector(expression.Name);
                }

                throw new NotImplementedException();
            }

            public override Expression VisitSelectorFilter(SelectorFilterExpression expression)
            {
                Expression left = VisitAsExpression(expression.Expression);

                return Expression.MakeBinary(
                    AsExpressionType(expression.Operator),
                    left,
                    ConvertConstant(expression.Value, left.Type)
                    );
            }

            private Expression CreatePayloadSelector(string payloadName)
            {
                PropertyInfo payloadProperty = ItemParameter.Type.GetProperty("Payload");
                PropertyInfo indexerProperty = payloadProperty.PropertyType.GetProperty("Item");

                MemberExpression payloadExpression = Expression.Property(
                    ItemParameter,
                    payloadProperty);

                IndexExpression valueExpression = Expression.Property(
                    payloadExpression,
                    indexerProperty,
                    Expression.Constant(payloadName));

                Type payloadType = _sample.Event.PayloadByName(payloadName)?.GetType();
                PropertyInfo candidateProperty = _sample.Event.GetType().GetProperty(payloadName);
                if (null == payloadType)
                {
                    // Try to find event property with same name. If one exists, it should have
                    // the same value (null) as the payload property. Use the type of the property
                    // to infer the payload value type.
                    if (null != candidateProperty && null == candidateProperty.GetValue(_sample.Event))
                    {
                        payloadType = candidateProperty.PropertyType;
                    }
                }
                else if (payloadType.IsValueType)
                {
                    // If payload value from sample is a value type, lift to nullable version
                    // in case future event instances have a null value.
                    payloadType = typeof(Nullable<>).MakeGenericType(payloadType);
                }

                if (null == payloadType)
                {
                    return valueExpression;
                }
                else
                {
                    return Expression.Convert(
                        valueExpression,
                        payloadType);
                }
            }

            private Expression CreatePropertySelector(string propertyName)
            {
                PropertyInfo eventProperty = ItemParameter.Type.GetProperty("Event");
                PropertyInfo targetProperty = _sample.Event.GetType().GetProperty(propertyName);

                MemberExpression eventExpression = Expression.Property(
                    ItemParameter,
                    eventProperty);

                UnaryExpression castExpression = Expression.Convert(
                    eventExpression,
                    _sample.Event.GetType());

                return Expression.Property(
                    castExpression,
                    targetProperty);
            }

            private static ExpressionType AsExpressionType(LogicalOperator logicalOperator)
            {
                switch (logicalOperator)
                {
                    case LogicalOperator.And:
                        return ExpressionType.AndAlso;
                    case LogicalOperator.Or:
                        return ExpressionType.OrElse;
                }

                throw CreateException($"Logical operator '{logicalOperator}' was not handled.");
            }

            private static ExpressionType AsExpressionType(BinaryOperator binaryOperator)
            {
                switch (binaryOperator)
                {
                    case BinaryOperator.Equal:
                        return ExpressionType.Equal;
                    case BinaryOperator.GreaterThan:
                        return ExpressionType.GreaterThan;
                    case BinaryOperator.GreaterThanOrEqual:
                        return ExpressionType.GreaterThanOrEqual;
                    case BinaryOperator.LessThan:
                        return ExpressionType.LessThan;
                    case BinaryOperator.LessThanOrEqual:
                        return ExpressionType.LessThanOrEqual;
                    case BinaryOperator.NotEqual:
                        return ExpressionType.NotEqual;
                }

                throw CreateException($"Binary operator '{binaryOperator}' was not handled.");
            }

            private static ConstantExpression ConvertConstant(string value, Type targetType)
            {
                Type underlyingType = Nullable.GetUnderlyingType(targetType);
                if (null != underlyingType)
                {
                    // Convert to the underlying type but create expression with the nullable type.
                    // The Expression infrastructure allows for creation of constants with automatic
                    // lifting to the nullable version of their types.
                    return Expression.Constant(Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture), targetType);
                }
                else
                {
                    return Expression.Constant(Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture), targetType);
                }
            }

            private Expression VisitAsExpression(ExpressionNode node)
            {
                return VisitAsExpression(node, ItemsParameter, ItemParameter);
            }

            private Expression VisitAsExpression(
                ExpressionNode node,
                ParameterExpression itemsParameter,
                ParameterExpression itemParameter)
            {
                CompilationContext previousContext = _context;
                _context = new CompilationContext(itemsParameter, itemParameter);
                try
                {
                    return node.Accept(this);
                }
                finally
                {
                    _context = previousContext;
                }
            }

            private LambdaExpression VisitAsLambda(ExpressionNode node)
            {
                ParameterExpression itemParameter = Expression.Parameter(typeof(TraceEventData), "i");
                return Expression.Lambda(
                    VisitAsExpression(node, null, itemParameter),
                    itemParameter);
            }

            private static PipelineException CreateException(string message)
            {
                return new PipelineException(WrapMessage(message));
            }

            private static string WrapMessage(string message)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return "Condition compilation failed.";
                }
                else
                {
                    return $"Condition compilation failed: {message}";
                }
            }

            private ParameterExpression ItemsParameter => _context.ItemsParameter;

            private ParameterExpression ItemParameter => _context.ItemParameter;

            private class CompilationContext
            {
                public CompilationContext(CompilationContext context)
                    : this(context.ItemsParameter, context.ItemParameter)
                {
                }

                public CompilationContext(ParameterExpression itemsParameter, ParameterExpression itemParameter)
                {
                    ItemsParameter = itemsParameter;
                    ItemParameter = itemParameter;
                }

                public ParameterExpression ItemsParameter { get; }

                public ParameterExpression ItemParameter { get; }
            }
        }
    }
}
