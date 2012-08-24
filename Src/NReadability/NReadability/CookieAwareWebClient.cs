using System;
using System.Net;

namespace NReadability
{
  public class CookieAwareWebClient : WebClient
  {
    private readonly CookieContainer _cookieContainer;

    #region Constructor(s)

    public CookieAwareWebClient()
    {
      _cookieContainer = new CookieContainer();
    }

    #endregion

    #region Overrides of WebClient

    protected override WebRequest GetWebRequest(Uri address)
    {
      WebRequest webRequest = base.GetWebRequest(address);
      HttpWebRequest httpWebRequest = webRequest as HttpWebRequest;

      if (httpWebRequest != null)
      {
        httpWebRequest.CookieContainer = _cookieContainer;
      }

      return webRequest;
    }

    #endregion
  }
}
