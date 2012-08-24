using System;

namespace NReadability.Tests
{
  public class SimpleUrlFetcherStub : IUrlFetcher
  {
    private readonly string _contentToReturn;

    public SimpleUrlFetcherStub(string contentToReturn)
    {
      if (string.IsNullOrEmpty(contentToReturn))
      {
        throw new ArgumentException("Argument can't be null nor empty.", "contentToReturn");
      }

      _contentToReturn = contentToReturn;
    }

    public string Fetch(string url)
    {
      return _contentToReturn;
    }
  }
}
