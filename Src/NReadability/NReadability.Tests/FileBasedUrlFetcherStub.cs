using System;
using System.IO;

namespace NReadability.Tests
{
  /// <summary>
  /// Stubs UrlFetcher to provide sample html documents from disk.
  /// The test sample number and an array of urls are passed to the constructor.
  /// When it recieves a request to fetch a page, it checks the array of urls for the
  /// requested page and returns the associated html document from disk.
  /// </summary>
  internal class FileBasedUrlFetcherStub : IUrlFetcher
  {
    private readonly string[] _urls;
    private readonly int _sampleInputNumber;

    #region Constructor(s)

    public FileBasedUrlFetcherStub(int sampleInputNumber, string[] urls)
    {
      _urls = urls;
      _sampleInputNumber = sampleInputNumber;
    }

    #endregion

    #region IUrlFetcher members

    public string Fetch(string url)
    {
      string sampleInputNumberStr = _sampleInputNumber.ToString().PadLeft(2, '0');
      int pageNo = Array.IndexOf(_urls, url) + 1;
      
      if (pageNo == 0)
      {
        return null;
      }

      return File.ReadAllText(string.Format(@"SampleWebInput\SampleInput_{0}_{1}.html", sampleInputNumberStr, pageNo));
    }

    #endregion
  }
}
