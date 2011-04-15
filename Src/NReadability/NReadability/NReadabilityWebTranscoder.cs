/*
 * NReadability
 * http://code.google.com/p/nreadability/
 * 
 * Copyright 2010 Marek Stój
 * http://immortal.pl/
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NReadability
{
  /// <summary>
  /// A class that extracts main content from a url.
  /// </summary>
  public class NReadabilityWebTranscoder
  {
    private const int _MaxPages = 30;
    private const string _PageIdPrefix = "readability-page-";

    private static readonly Func<int, string> _DefaultPageSeparatorBuilder =
      pageNumber => string.Format("<p class='page-separator' title='Page {0}'>&sect;</p>", pageNumber);

    private readonly NReadabilityTranscoder _transcoder;
    private readonly IUrlFetcher _urlFetcher;
    private readonly SgmlDomSerializer _sgmlDomSerializer;

    private Func<int, string> _pageSeparatorBuilder;
    private List<string> _parsedPages;
    private int _curPageNum;

    #region Constructor(s)

    /// <summary>
    ///  Initializes a new instance of NReadabilityWebTranscoder.
    ///  Allows passing in custom-constructed NReadabilityTranscoder,
    ///  and a custom IUrlFetcher.  This overload is mostly used for testing.
    /// </summary>
    /// <param name="transcoder">A NReadabilityTranscoder.</param>
    /// <param name="urlFetcher">IFetcher instance to download content.</param>
    public NReadabilityWebTranscoder(NReadabilityTranscoder transcoder, IUrlFetcher urlFetcher)
    {
      _transcoder = transcoder;
      _urlFetcher = urlFetcher;
      _sgmlDomSerializer = new SgmlDomSerializer();
      _pageSeparatorBuilder = _DefaultPageSeparatorBuilder;
    }

    /// <summary>
    /// Initializes a new instance of NReadabilityWebTranscoder.
    /// Allows passing in custom-constructed NReadabilityTranscoder.
    /// </summary>
    /// <param name="transcoder">A NReadailityTranscoder.</param>
    public NReadabilityWebTranscoder(NReadabilityTranscoder transcoder)
      : this(transcoder, new UrlFetcher())
    {
    }

    /// <summary>
    /// Initializes a new instance of NReadabilityWebTranscoder.
    /// </summary>
    public NReadabilityWebTranscoder()
      : this(new NReadabilityTranscoder(), new UrlFetcher())
    {
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Extracts main article content from a HTML web page.
    /// </summary>    
    /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
    /// <param name="domSerializationParams">Contains parameters that modify the behaviour of the output serialization.</param>
    /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>    
    /// <returns>HTML markup containing extracted article content.</returns>
    public string Transcode(string url, DomSerializationParams domSerializationParams, out bool mainContentExtracted)
    {
      _curPageNum = 1;
      _parsedPages = new List<string>();

      /* Make sure this document is added to the list of parsed pages first, so we don't double up on the first page */
      _parsedPages.Add(Regex.Replace(url, @"\/$", ""));

      string htmlContent = _urlFetcher.Fetch(url);

      /* If we can't fetch the page, then exit. */
      if (string.IsNullOrEmpty(htmlContent))
      {
        mainContentExtracted = false;
        return null;
      }

      /* Attempt to transcode the page */
      XDocument document;
      string nextPage;

      document = _transcoder.TranscodeToXml(htmlContent, url, out mainContentExtracted, out nextPage);

      if (nextPage != null)
      {
        AppendNextPage(document, nextPage);
      }

      /* If there are multiple pages, rename the first content div */
      if (_curPageNum > 1)
      {
        var articleContainer = document.GetElementById("readInner").Element("div");

        articleContainer.SetId(_PageIdPrefix + "1");
        articleContainer.SetClass("page");
      }

      return _sgmlDomSerializer.SerializeDocument(document, domSerializationParams);
    }

    /// <summary>
    /// Extracts main article content from a HTML web page using default DomSerializationParams.
    /// </summary>    
    /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
    /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>    
    /// <returns>HTML markup containing extracted article content.</returns>
    public string Transcode(string url, out bool mainContentExtracted)
    {
      return Transcode(url, DomSerializationParams.CreateDefault(), out mainContentExtracted);
    }

    #endregion

    #region Private helper methods

    /// <summary>
    /// Recursively appends subsequent pages of a multipage article.
    /// </summary>
    /// <param name="document">Compiled document</param>
    /// <param name="url">Url of current page</param>
    private void AppendNextPage(XDocument document, string url)
    {      
      _curPageNum++;

      var contentDiv = document.GetElementById("readInner");
          
      if (_curPageNum > _MaxPages) 
      {
        url = "<div style='text-align: center'><a href='" + url + "'>View Next Page</a></div>";
        contentDiv.Add(XDocument.Parse(url));
        return;
      }

      string nextContent = _urlFetcher.Fetch(url);

      if (string.IsNullOrEmpty(nextContent))
      {
        return;
      }

      bool mainContentExtracted;
      string nextPageLink;
      var nextDocument = _transcoder.TranscodeToXml(nextContent, url, out mainContentExtracted, out nextPageLink);
      var nextInner = nextDocument.GetElementById("readInner");
      var header = nextInner.Element("h1");

      if (header != null)
      {
        header.Remove();
      }

      /*
       * Anti-duplicate mechanism. Essentially, get the first paragraph of our new page.
       * Compare it against all of the the previous document's we've gotten. If the previous
       * document contains exactly the innerHTML of this first paragraph, it's probably a duplicate.
      */
      var firstP = nextInner.GetElementsByTagName("p").Count() > 0 ? nextInner.GetElementsByTagName("p").First() : null;

      if (firstP != null && firstP.GetInnerHtml().Length > 100)
      {
        //string innerHtml = firstP.GetInnerHtml();
        //var existingContent = contentDiv.GetInnerHtml();        
        //existingContent = Regex.Replace(existingContent, "xmlns(:[a-z]+)?=['\"][^'\"]+['\"]", "", RegexOptions.IgnoreCase);
        //existingContent = Regex.Replace(existingContent, @"\s+", "");
        //innerHtml = Regex.Replace(innerHtml, @"\s+", "");

        // TODO: This test could probably be improved to compare the actual markup.
        string existingContent = contentDiv.Value;
        string innerHtml = firstP.Value;

        if (!string.IsNullOrEmpty(existingContent) && !string.IsNullOrEmpty(innerHtml) && existingContent.IndexOf(innerHtml) != -1)
        {
          _parsedPages.Add(url);
          return;
        }
      }

      /* Add the content to the existing html */
      var nextDiv = new XElement("div");

      if (_pageSeparatorBuilder != null)
      {
        nextDiv.SetInnerHtml(_pageSeparatorBuilder(_curPageNum));
      }

      nextDiv.SetId(_PageIdPrefix + _curPageNum);
      nextDiv.SetClass("page");      
      nextDiv.Add(nextInner.Nodes());
      contentDiv.Add(nextDiv);
      _parsedPages.Add(url);

      /* Only continue if we haven't already seen the next page page */
      if (!string.IsNullOrEmpty(nextPageLink) && !_parsedPages.Contains(nextPageLink))
      {
        AppendNextPage(document, nextPageLink);
      }
    }

    #endregion

    #region Properties

    /// <summary>
    /// A function which, given a current page number, constructs the HTML which will be used as a page separator.
    /// </summary>
    public Func<int, string> PageSeparatorBuilder
    {
      get { return _pageSeparatorBuilder; }
      set { _pageSeparatorBuilder = value; }
    }

    #endregion
  }
}
