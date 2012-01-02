using System;
using NUnit.Framework;

namespace NReadability.Tests
{
  [TestFixture]
  public class HtmlUtilsTests
  {
    [Test]
    public void RemoveScriptTags_handles_invalid_arguments()
    {
      Assert.Throws<ArgumentNullException>(() => HtmlUtils.RemoveScriptTags(null));
    }

    [Test]
    public void RemoveScriptTags_works_with_empty_argument()
    {
      Assert.DoesNotThrow(() => HtmlUtils.RemoveScriptTags(""));
    }

    [Test]
    public void RemoveScriptTags_handles_html_without_scripts()
    {
      string inputHtml = "<html><div></div></html>";
      string strippedHtml = HtmlUtils.RemoveScriptTags(inputHtml);

      Assert.AreEqual(inputHtml, strippedHtml);
    }

    [Test]
    public void RemoveScriptTags_handles_html_with_empty_script()
    {
      string inputHtml = "<html><script></script></html>";
      string expectedHtml = "<html></html>";
      string strippedHtml = HtmlUtils.RemoveScriptTags(inputHtml);

      Assert.AreEqual(expectedHtml, strippedHtml);
    }

    [Test]
    public void RemoveScriptTags_handles_html_with_non_empty_multiline_script()
    {
      string inputHtml = "<html>\r\n  <script>\r\n  var x = 1;\r\n  </script>\r\n</html>";
      string expectedHtml = "<html>\r\n  \r\n</html>";
      string strippedHtml = HtmlUtils.RemoveScriptTags(inputHtml);

      Assert.AreEqual(expectedHtml, strippedHtml);
    }

    [Test]
    public void RemoveScriptTags_handles_html_with_script_being_at_the_end_of_the_document()
    {
      string inputHtml = "<script>\r\n  var x = 1;\r\n  </script>";
      string expectedHtml = "";
      string strippedHtml = HtmlUtils.RemoveScriptTags(inputHtml);

      Assert.AreEqual(expectedHtml, strippedHtml);
    }

    [Test]
    public void RemoveScriptTags_handles_html_with_multiple_scripts()
    {
      string inputHtml = "<html><script type=\"text/javascript\"></script><p>X</p><script></script></html>";
      string expectedHtml = "<html><p>X</p></html>";
      string strippedHtml = HtmlUtils.RemoveScriptTags(inputHtml);

      Assert.AreEqual(expectedHtml, strippedHtml);
    }
  }
}
