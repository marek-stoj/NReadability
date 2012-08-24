namespace NReadability
{
  public class TranscodingResult
  {
    public TranscodingResult(bool contentExtracted, bool titleExtracted)
    {
      ContentExtracted = contentExtracted;
      TitleExtracted = titleExtracted;
    }

    public bool ContentExtracted { get; private set; }

    public bool TitleExtracted { get; private set; }

    public string ExtractedContent { get; set; }

    public string ExtractedTitle { get; set; }

    public string NextPageUrl { get; set; }
  }
}
