using System;

namespace NReadability
{
  public class WebTranscodingInput
  {
    private DomSerializationParams _domSerializationParams;

    public WebTranscodingInput(string url)
    {
      if (string.IsNullOrEmpty(url))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "url");
      }

      Url = url;
    }

    public string Url { get; private set; }

    public DomSerializationParams DomSerializationParams
    {
      get { return _domSerializationParams ?? (_domSerializationParams = DomSerializationParams.CreateDefault()); }
      set { _domSerializationParams = value; }
    }
  }
}
