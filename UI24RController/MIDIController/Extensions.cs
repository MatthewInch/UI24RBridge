using System;
using System.Collections.Generic;
using System.Text;

namespace UI24RController.MIDIController
{
    public static class Extensions
    {
        public static bool MIDIEqual(this byte[] data, byte data0,byte data1, byte data2)
        {
            return (data.Length > 2 && data[0] == data0 && data[1] == data1 && data[2] == data2);
        }
        public static bool MIDIEqual(this byte[] data, byte data0, byte data1)
        {
            return (data.Length > 2 && data[0] == data0 && data[1] == data1);
        }

        public static bool MIDIEqual(this byte[] data, byte data0, byte data1, byte data2, byte mask0, byte mask1, byte mask2)
        {
            return (data.Length > 2 && (data[0] & mask0) == data0 && (data[1] & mask1) == data1 && (data[2] & mask2) == data2);
        }

        public static void AddOrSet<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
        {
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] = value;
            }
            else
            {
                dictionary.Add(key, value);
            }
        }
    }
}
