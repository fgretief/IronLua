using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IronLua
{
    static class DictionaryExtensions
    {
    
        public static void AddNotPresent<TKey,TValue>(this IDictionary<TKey,TValue> dictionary, TKey key, TValue value)
        {
            if (!dictionary.ContainsKey(key))
                dictionary.Add(key, value);
        }

        public static void AddOrSet<TKey,TValue>(this IDictionary<TKey,TValue> dictionary, TKey key, TValue value)
        {
            if (!dictionary.ContainsKey(key))
                dictionary.Add(key, value);
            else
                dictionary[key] = value;
        }

        public static void RemoveIfEqual<TKey,TValue>(this IDictionary<TKey,TValue> dictionary, TKey key, TValue value)
        {
            if (!dictionary.ContainsKey(key))
                return;

            var actual = dictionary[key];

            if (actual == null && value != null)
                return;
            
            if (actual != null && !actual.Equals(value))
                return;

            dictionary.Remove(key);
        }
    }
}
