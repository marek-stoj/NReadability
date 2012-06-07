namespace NReadability
{
  public class WebTranscodingResult
  {
    public WebTranscodingResult(bool contentExtracted, bool titleExtracted)
    {
      ContentExtracted = contentExtracted;
      TitleExtracted = titleExtracted;
    }

    public bool ContentExtracted { get; private set; }

    public bool TitleExtracted { get; private set; }

    public string ExtractedContent { get; set; }

    public string ExtractedTitle { get; set; }
  }
}
