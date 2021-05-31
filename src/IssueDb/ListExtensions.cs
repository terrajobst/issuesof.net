using System.Collections.Generic;
using System.Linq;

namespace IssueDb
{
    public static class ListExtensions
    {
        public static List<T> CopyAndAdd<T>(this List<T> list, T item)
        {
            var result = list.ToList();
            result.Add(item);
            return result;
        }

        public static List<T> CopyAndRemove<T>(this List<T> list, T item)
        {
            var result = list.ToList();
            result.Remove(item);
            return result;
        }
    }
}
