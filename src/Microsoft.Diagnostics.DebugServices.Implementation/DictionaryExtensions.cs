// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.DebugServices.Implementation
{
    /// <summary>
    /// A set of helpers operating on various collection types.
    /// </summary>
    public static class DictionaryExtensions 
    {
        public static TValue GetAddValue<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> callback)
        {
            if (dictionary.TryGetValue(key, out TValue existingValue)) {
                return existingValue;
            }
            TValue newValue = callback();
            dictionary.Add(key, newValue);
            return newValue;
        }
    }
}
