// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal static class DictionaryExtensions
    {
        public static bool TryGetValue<T>(this IDictionary<string, object> dictionary, string key, out T value)
        {
            if (dictionary.TryGetValue(key, out object objValue) && objValue is T typedValue)
            {
                value = typedValue;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }
    }
}
