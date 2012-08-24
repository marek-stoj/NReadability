using System;

namespace NReadability
{
  public class TranscodingInput
  {
    private DomSerializationParams _domSerializationParams;

    public TranscodingInput(string htmlContent)
    {
      if (string.IsNullOrEmpty(htmlContent))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "htmlContent");
      }

      HtmlContent = htmlContent;
    }

    public string HtmlContent { get; private set; }

    public string Url { get; set; }

    public DomSerializationParams DomSerializationParams
    {
      get { return _domSerializationParams ?? (_domSerializationParams = DomSerializationParams.CreateDefault()); }
      set { _domSerializationParams = value; }
    }
  }
}
