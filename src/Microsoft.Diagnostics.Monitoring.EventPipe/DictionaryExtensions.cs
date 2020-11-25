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
