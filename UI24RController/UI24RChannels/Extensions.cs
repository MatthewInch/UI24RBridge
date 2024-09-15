using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UI24RController.UI24RChannels
{
    public static class Extensions
    {
        public static T[] ToFixedLength<T>(this IEnumerable<T> source, int length,T defaultValue)
        {
            var result = new T[length];
            var sourceArray = source.ToArray();
            for ( var i = 0; i < length; i++)
            {
                if (i < source.Count())
                {
                    result[i] = sourceArray[i];
                } else
                {
                    result[i] = defaultValue;
                }
            }            
            return result;
        }
    }
}
