using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NReadability.Tests
{
  [TestFixture]
  public class EnumerableExtensionsTests
  {
    #region SingleOrNone() tests

    [Test]
    public void SingleOrNone_throws_if_collection_is_null()
    {
      // ReSharper disable ConditionIsAlwaysTrueOrFalse

      Assert.Throws<ArgumentNullException>(
        () => ((IEnumerable<string>)null).SingleOrNone());

      // ReSharper restore ConditionIsAlwaysTrueOrFalse
    }

    [Test]
    public void SingleOrNone_returns_null_if_collection_is_empty()
    {
      Assert.IsNull(new string[0].SingleOrNone());
    }

    [Test]
    public void SingleOrNone_returns_the_only_element_if_collection_has_exactly_one_element()
    {
      Assert.AreEqual("element", new[] { "element" }.SingleOrNone());
    }

    #endregion
  }
}
