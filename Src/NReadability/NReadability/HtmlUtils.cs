using System;

namespace NReadability
{
  public static class HtmlUtils
  {
    public static string RemoveScriptTags(string htmlContent)
    {
      if (htmlContent == null)
      {
        throw new ArgumentNullException("htmlContent");
      }

      if (htmlContent.Length == 0)
      {
        return "";
      }

      int indexOfScriptTagStart = htmlContent.IndexOf("<script", StringComparison.OrdinalIgnoreCase);

      if (indexOfScriptTagStart == -1)
      {
        return htmlContent;
      }

      int indexOfScriptTagEnd = htmlContent.IndexOf("</script>", indexOfScriptTagStart, StringComparison.OrdinalIgnoreCase);

      if (indexOfScriptTagEnd == -1)
      {
        return htmlContent.Substring(0, indexOfScriptTagStart);
      }

      string strippedHtmlContent =
        htmlContent.Substring(0, indexOfScriptTagStart) +
        htmlContent.Substring(indexOfScriptTagEnd + "</script>".Length);

      return RemoveScriptTags(strippedHtmlContent);
    }
  }
}
