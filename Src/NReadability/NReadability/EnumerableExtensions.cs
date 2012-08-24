using System;
using System.Collections.Generic;
using System.Linq;

namespace NReadability
{
  public static class EnumerableExtensions
  {
    /// <summary>
    /// Returns the only one element in the sequence or default(T) if either the sequence doesn't contain any elements or it contains more than one element.
    /// </summary>
    public static T SingleOrNone<T>(this IEnumerable<T> enumerable)
      where T : class
    {
      // ReSharper disable PossibleMultipleEnumeration

      if (enumerable == null)
      {
        throw new ArgumentNullException("enumerable");
      }

      T firstElement = enumerable.FirstOrDefault();

      if (firstElement == null)
      {
        // no elements
        return null;
      }

      T secondElement = enumerable.Skip(1).FirstOrDefault();

      if (secondElement != null)
      {
        // more than one element
        return null;
      }

      return firstElement;

      // ReSharper restore PossibleMultipleEnumeration
    }
  }
}
