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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;

namespace NReadability
{
  /// <summary>
  /// A class that extracts main content from a HTML page.
  /// </summary>
  public class NReadabilityTranscoder
  {
    #region Data structures
    
    /// <summary>
    /// Used in FindNextPageLink
    /// </summary>
    private class LinkData
    {
        public float Score;
        public string LinkText;
        public string LinkHref;
    }

    #endregion
      
    #region Fields

    #region Resources constants

    private static readonly string _ReadabilityStylesheetResourceName = typeof(NReadabilityTranscoder).Namespace + ".Resources.readability.css";

    #endregion

    #region Algorithm constants

    /// <summary>
    /// Default styling of the extracted article.
    /// </summary>
    public const ReadingStyle DefaultReadingStyle = ReadingStyle.Newspaper;

    /// <summary>
    /// Default margin of the extracted article.
    /// </summary>
    public const ReadingMargin DefaultReadingMargin = ReadingMargin.Wide;

    /// <summary>
    /// Default size of the font used for the extracted article.
    /// </summary>
    public const ReadingSize DefaultReadingSize = ReadingSize.Medium;

    internal const string OverlayDivId = "readOverlay";
    internal const string InnerDivId = "readInner";
    internal const string ContentDivId = "readability-content";
    internal const string ReadabilityStyledCssClass = "readability-styled";

    private const int _MinParagraphLength = 25;
    private const int _MinInnerTextLength = 25;
    private const int _ParagraphSegmentLength = 100;
    private const int _MaxPointsForSegmentsCount = 3;
    private const int _MinSiblingParagraphLength = 80;
    private const int _MinCommaSegments = 10;
    private const int _LisCountTreshold = 100;
    private const int _MaxImagesInShortSegmentsCount = 2;
    private const int _MinInnerTextLengthInElementsWithEmbed = 75;
    private const int _ClassWeightTreshold = 25;
    private const int _MaxEmbedsCount = 1;
    private const int _MaxArticleTitleLength = 150;
    private const int _MinArticleTitleLength = 15;
    private const int _MinArticleTitleWordsCount1 = 3;
    private const int _MinArticleTitleWordsCount2 = 4;

    private const float _SiblingScoreTresholdCoefficient = 0.2f;
    private const float _MaxSiblingScoreTreshold = 10.0f;
    private const float _MaxSiblingParagraphLinksDensity = 0.25f;
    private const float _MaxHeaderLinksDensity = 0.33f;
    private const float _MaxDensityForElementsWithSmallerClassWeight = 0.2f;
    private const float _MaxDensityForElementsWithGreaterClassWeight = 0.5f;

    #endregion

    #region Algorithm regular expressions

    private static readonly Regex _UnlikelyCandidatesRegex = new Regex("combx|comment|community|disqus|extra|foot|header|menu|remark|rss|shoutbox|sidebar|sponsor|ad-break|agegate|pagination|pager|popup|tweet|twitter", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _OkMaybeItsACandidateRegex = new Regex("and|article|body|column|main|shadow", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _PositiveWeightRegex = new Regex("article|body|content|entry|hentry|main|page|pagination|post|text|blog|story", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _NegativeWeightRegex = new Regex("combx|comment|com-|contact|foot|footer|footnote|masthead|media|meta|outbrain|promo|related|scroll|shoutbox|sidebar|side|sponsor|shopping|tags|tool|widget", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _Extraneous = new Regex("print|archive|comment|discuss|e[-]?mail|share|reply|all|login|sign|single|also", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _DivToPElementsRegex = new Regex("<(a|blockquote|dl|div|img|ol|p|pre|table|ul)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _EndOfSentenceRegex = new Regex("\\.( |$)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex _BreakBeforeParagraphRegex = new Regex("<br[^>]*>\\s*<p", RegexOptions.Compiled);
    private static readonly Regex _NormalizeSpacesRegex = new Regex("\\s{2,}", RegexOptions.Compiled);
    private static readonly Regex _KillBreaksRegex = new Regex("(<br\\s*\\/?>(\\s|&nbsp;?)*){1,}", RegexOptions.Compiled);
    private static readonly Regex _VideoRegex = new Regex("http:\\/\\/(www\\.)?(youtube|vimeo)\\.com", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _ReplaceDoubleBrsRegex = new Regex("(<br[^>]*>[ \\n\\r\\t]*){2,}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _ReplaceFontsRegex = new Regex("<(\\/?)font[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _ArticleTitleDashRegex1 = new Regex(" [\\|\\-] ", RegexOptions.Compiled);
    private static readonly Regex _ArticleTitleDashRegex2 = new Regex("(.*)[\\|\\-] .*", RegexOptions.Compiled);
    private static readonly Regex _ArticleTitleDashRegex3 = new Regex("[^\\|\\-]*[\\|\\-](.*)", RegexOptions.Compiled);
    private static readonly Regex _ArticleTitleColonRegex1 = new Regex(".*:(.*)", RegexOptions.Compiled);
    private static readonly Regex _ArticleTitleColonRegex2 = new Regex("[^:]*[:](.*)", RegexOptions.Compiled);    
    private static readonly Regex _NextLink = new Regex(@"(next|weiter|continue|dalej|następna|nastepna>([^\|]|$)|�([^\|]|$))", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _PrevLink = new Regex("(prev|earl|old|new|wstecz|poprzednia|<|�)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #endregion

    #region Other regular expressions

    private static readonly Regex _MailtoHrefRegex = new Regex("^\\s*mailto\\s*:", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #endregion

    #region Algorithm parameters

    private bool _dontStripUnlikelys;
    private readonly bool _dontNormalizeSpacesInTextContent;
    private readonly bool _dontWeightClasses;
    private readonly ReadingStyle _readingStyle;
    private readonly ReadingSize _readingSize;
    private readonly ReadingMargin _readingMargin;
    
    #endregion

    #region Helper instance fields

    private readonly SgmlDomBuilder _sgmlDomBuilder;
    private readonly SgmlDomSerializer _sgmlDomSerializer;
    private readonly Dictionary<XElement, float> _elementsScores;

    private Func<AttributeTransformationInput, AttributeTransformationResult> _imageSourceTranformer;
    private Func<AttributeTransformationInput, AttributeTransformationResult> _anchorHrefTransformer;
        
    #endregion

    #endregion

    #region Constructor(s)

    /// <summary>
    /// Initializes a new instance of NReadabilityTranscoder. Allows setting all options.
    /// </summary>
    /// <param name="dontStripUnlikelys">Determines whether elements that are unlikely to be a part of main content will be removed.</param>
    /// <param name="dontNormalizeSpacesInTextContent">Determines whether spaces in InnerText properties of elements will be normalized automatically (eg. whether double spaces will be replaced with single spaces).</param>
    /// <param name="dontWeightClasses">Determines whether 'weight-class' algorithm will be used when cleaning content.</param>
    /// <param name="readingStyle">Styling for the extracted article.</param>
    /// <param name="readingMargin">Margin for the extracted article.</param>
    /// <param name="readingSize">Font size for the extracted article.</param>
    private NReadabilityTranscoder(
      bool dontStripUnlikelys,
      bool dontNormalizeSpacesInTextContent,
      bool dontWeightClasses,
      ReadingStyle readingStyle,
      ReadingMargin readingMargin,
      ReadingSize readingSize)
    {
      _dontStripUnlikelys = dontStripUnlikelys;
      _dontNormalizeSpacesInTextContent = dontNormalizeSpacesInTextContent;
      _dontWeightClasses = dontWeightClasses;
      _readingStyle = readingStyle;
      _readingMargin = readingMargin;
      _readingSize = readingSize;

      _sgmlDomBuilder = new SgmlDomBuilder();
      _sgmlDomSerializer = new SgmlDomSerializer();
      _elementsScores = new Dictionary<XElement, float>();      
    }

    /// <summary>
    /// Initializes a new instance of NReadabilityTranscoder. Allows setting reading options.
    /// </summary>
    /// <param name="readingStyle">Styling for the extracted article.</param>
    /// <param name="readingMargin">Margin for the extracted article.</param>
    /// <param name="readingSize">Font size for the extracted article.</param>
    public NReadabilityTranscoder(ReadingStyle readingStyle, ReadingMargin readingMargin, ReadingSize readingSize)
      : this(false, false, false, readingStyle, readingMargin, readingSize)
    {
    }

    /// <summary>
    /// Initializes a new instance of NReadabilityTranscoder.
    /// </summary>
    public NReadabilityTranscoder()
      : this(DefaultReadingStyle, DefaultReadingMargin, DefaultReadingSize)
    {
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Extracts main article content from a HTML page.
    /// </summary>
    /// <param name="htmlContent">HTML markup to process.</param>
    /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
    /// <param name="domSerializationParams">Contains parameters that modify the behaviour of the output serialization.</param>
    /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>
    /// <param name="nextPageUrl">If the content contains a link to a subsequent page, it is returned here.</param>
    /// <returns>HTML markup containing extracted article content.</returns>
    public string Transcode(string htmlContent, string url, DomSerializationParams domSerializationParams, out bool mainContentExtracted, out string nextPageUrl)
    {
      var document = TranscodeToXml(htmlContent, url, out mainContentExtracted, out nextPageUrl);

      return _sgmlDomSerializer.SerializeDocument(document, domSerializationParams);
    }

    /// <summary>
    /// Extracts main article content from a HTML page.
    /// </summary>
    /// <param name="htmlContent">HTML markup to process.</param>
    /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
    /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>
    /// <param name="nextPageUrl">If the content contains a link to a subsequent page, it is returned here.</param>
    /// <returns>HTML markup containing extracted article content.</returns>
    public string Transcode(string htmlContent, string url, out bool mainContentExtracted, out string nextPageUrl)
    {
      return Transcode(htmlContent, url, DomSerializationParams.CreateDefault(), out mainContentExtracted, out nextPageUrl);
    }

    /// <summary>
    /// Extracts main article content from a HTML page.
    /// </summary>
    /// <param name="htmlContent">HTML markup to process.</param>
    /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
    /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>    
    /// <returns>HTML markup containing extracted article content.</returns>
    public string Transcode(string htmlContent, string url, out bool mainContentExtracted)
    {
      string nextPageUrl;

      return Transcode(htmlContent, url, DomSerializationParams.CreateDefault(), out mainContentExtracted, out nextPageUrl);
    }

    /// <summary>
    /// Extracts main article content from a HTML page.
    /// </summary>
    /// <param name="htmlContent">HTML markup to process.</param>
    /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>    
    /// <returns>HTML markup containing extracted article content.</returns>
    public string Transcode(string htmlContent, out bool mainContentExtracted)
    {
      string nextPageUrl;

      return Transcode(htmlContent, null, out mainContentExtracted, out nextPageUrl);
    }

    #endregion

    #region Readability algorithm

    /// <summary>
    /// Extracts main article content from a HTML page.
    /// </summary>
    /// <param name="htmlContent">HTML markup to process.</param>
    /// <param name="url">Url from which the content was downloaded. Used to resolve relative urls. Can be null.</param>
    /// <param name="mainContentExtracted">Determines whether the content has been extracted (if the article is not empty).</param>
    /// <param name="nextPageUrl">If the content contains a link to a subsequent page, it is returned here.</param>
    /// <returns>An XDocument containing extracted article content.</returns>
    internal XDocument TranscodeToXml(string htmlContent, string url, out bool mainContentExtracted, out string nextPageUrl)
    {
      if (string.IsNullOrEmpty(htmlContent))
      {
        throw new ArgumentNullException("htmlContent");
      }

      var document = _sgmlDomBuilder.BuildDocument(htmlContent);

      PrepareDocument(document);

      if (!string.IsNullOrEmpty(url))
      {
        ResolveElementsUrls(document, "img", "src", url, _imageSourceTranformer);
        ResolveElementsUrls(document, "a", "href", url, _anchorHrefTransformer);
      }

      nextPageUrl = null;

      if (url != null)
      {
        nextPageUrl = FindNextPageLink(document.GetBody(), url);
      }

      var articleTitleElement = ExtractArticleTitle(document);
      var articleContentElement = ExtractArticleContent(document);

      GlueDocument(document, articleTitleElement, articleContentElement);

      // fallback behaviour - rerun one more time with _dontStripUnlikelys if we have little content
      if (!_dontStripUnlikelys && GetInnerText(articleContentElement).Length < 250)
      {
        try
        {
          _dontStripUnlikelys = true;

          return TranscodeToXml(htmlContent, url, out mainContentExtracted, out nextPageUrl);
        } 
        finally
        {
          _dontStripUnlikelys = false;
        }
      }

      // TODO: implement another fallback behaviour - rerun one more time with _dontWeightClasses

      mainContentExtracted = !articleContentElement.IsEmpty;

      return document;
    }

    /// <summary>
    /// Looks for any paging links that may occur within the document
    /// </summary>
    /// <param name="body">Content body</param>
    /// <param name="url">Url of document</param>
    internal string FindNextPageLink(XElement body, string url)
    {
      var possiblePages = new Dictionary<string, LinkData>();
      var allLinks = body.GetElementsByTagName("a");
      var articleBaseUrl = FindBaseUrl(url);

      /* Loop through all links, looking for hints that they may be next-page links. 
       * Things like having "page" in their textContent, className or id, or being a child
       * of a node with a page-y className or id. 
       * After we do that, assign each page a score.
       */
      foreach (var link in allLinks)
      {
        string linkHref = (string)link.Attribute("href");

        if (string.IsNullOrEmpty(linkHref))
          continue;

        if (_MailtoHrefRegex.IsMatch(linkHref))
          continue;

        linkHref = Regex.Replace(linkHref, "#.*$", "");
        linkHref = Regex.Replace(linkHref, "/$", "");

        /* If we've already seen this page, then ignore it. */
        // This leaves out an already-checked page check, because 
        // the web transcoder is seperate from the original transcoder
        if (linkHref == "" || linkHref == articleBaseUrl || linkHref == url)
          continue;

        /* If it's on a different domain, skip it. */
        Uri linkHrefUri;
        if (Uri.TryCreate(linkHref, UriKind.Absolute, out linkHrefUri) && linkHrefUri.Host != new Uri(articleBaseUrl).Host)
          continue;

        string linkText = GetInnerText(link);

        /* If the linktext looks like it's not the next page, then skip it */
        if (_Extraneous.IsMatch(linkText) || linkText.Length > 25)
          continue;

        /* If the leftovers of the URL after removing the base URL don't contain any digits, it's certainly not a next page link. */
        string linkHrefLeftover = linkHref.Replace(articleBaseUrl, "");
        if (!Regex.IsMatch(linkHrefLeftover, @"\d"))
          continue;

        if (!possiblePages.Keys.Contains(linkHref))
        {
          possiblePages[linkHref] = new LinkData { Score = 0, LinkHref = linkHref, LinkText = linkText };
        } 
        else
        {
          possiblePages[linkHref].LinkText += " | " + linkText;
        }

        var linkObj = possiblePages[linkHref];

        /*
         * If the articleBaseUrl isn't part of this URL, penalize this link. It could still be the link, but the odds are lower.
         * Example: http://www.actionscript.org/resources/articles/745/1/JavaScript-and-VBScript-Injection-in-ActionScript-3/Page1.html
         */
        if (linkHref.IndexOf(articleBaseUrl) == -1)
          linkObj.Score -= 25;

        string linkData = linkText + " " + link.GetClass() + " " + link.GetId();

        if (_NextLink.IsMatch(linkData))
          linkObj.Score += 50;

        if (Regex.IsMatch(linkData, "pag(e|ing|inat)", RegexOptions.IgnoreCase))
          linkObj.Score += 25;

        /* If we already matched on "next", last is probably fine. If we didn't, then it's bad. Penalize. */
        /* -65 is enough to negate any bonuses gotten from a > or � in the text */
        if (Regex.IsMatch(linkData, "(first|last)", RegexOptions.IgnoreCase))
          if (!_NextLink.IsMatch(linkObj.LinkText))
            linkObj.Score -= 65;

        if (_NegativeWeightRegex.IsMatch(linkData) || _Extraneous.IsMatch(linkData))
          linkObj.Score -= 50;

        if (_PrevLink.IsMatch(linkData))
          linkObj.Score -= 200;

        /* If any ancestor node contains page or paging or paginat */
        var parentNode = link.Parent;
        bool positiveNodeMatch = false;
        bool negativeNodeMatch = false;

        while (parentNode != null)
        {
          string parentNodeClassAndId = parentNode.GetClass() + " " + parentNode.GetId();
          if (!positiveNodeMatch && Regex.IsMatch(parentNodeClassAndId, "pag(e|ing|inat)", RegexOptions.IgnoreCase))
          {
            positiveNodeMatch = true;
            linkObj.Score += 25;
          }
          if (!negativeNodeMatch && _NegativeWeightRegex.IsMatch(parentNodeClassAndId))
          {
            if (!_PositiveWeightRegex.IsMatch(parentNodeClassAndId))
            {
              linkObj.Score -= 25;
              negativeNodeMatch = true;
            }
          }

          parentNode = parentNode.Parent;
        }

        /*
        * If the URL looks like it has paging in it, add to the score.
        * Things like /page/2/, /pagenum/2, ?p=3, ?page=11, ?pagination=34
        */
        if (Regex.IsMatch(linkHref, @"p(a|g|ag)?(e|ing|ination)?(=|\/)[0-9]{1,2}", RegexOptions.IgnoreCase) || 
            Regex.IsMatch(linkHref, @"(page|paging)", RegexOptions.IgnoreCase))
        {
          linkObj.Score += 25;
        }

        /* If the URL contains negative values, give a slight decrease. */
        if (_Extraneous.IsMatch(linkHref))
        {
          linkObj.Score -= 15;
        }

        /*
         * If the link text can be parsed as a number, give it a minor bonus, with a slight
         * bias towards lower numbered pages. This is so that pages that might not have 'next'
         * in their text can still get scored, and sorted properly by score.
         */
        int linkTextAsNumber;
        bool isInt = int.TryParse(linkText, out linkTextAsNumber);
        if (isInt)
        {
          /* Punish 1 since we're either already there, or it's probably before what we want anyways. */
          if (linkTextAsNumber == 1)
            linkObj.Score -= 10;
          else
            linkObj.Score += Math.Max(0, 10 - linkTextAsNumber);
        }
      }

      /*
      * Loop thrugh all of our possible pages from above and find our top candidate for the next page URL.
      * Require at least a score of 50, which is a relatively high confidence that this page is the next link.
      */
      LinkData topPage = null;
      foreach (var page in possiblePages.Keys)
      {
        if (possiblePages[page].Score >= 50 && (topPage == null || topPage.Score < possiblePages[page].Score))
          topPage = possiblePages[page];
      }

      if (topPage != null)
      {
        string nextHref = Regex.Replace(topPage.LinkHref, @"\/$", "");        
        var nextHrefUri = new Uri(new Uri(articleBaseUrl), nextHref);

        return nextHrefUri.ToString();
      }

      return null;
    }
    
    /// <summary>
    /// Find a cleaned up version of the current URL, to use for comparing links for possible next-pageyness.
    /// </summary>    
    internal string FindBaseUrl(string url)
    {
      Uri urlUri;

      if (!Uri.TryCreate(url, UriKind.Absolute, out urlUri))
      {
        return url;
      }

      string protocol = urlUri.Scheme;
      string hostname = urlUri.Host;        
      string noUrlParams = urlUri.AbsolutePath + "/";
      var urlSlashes = noUrlParams.Split('/').Reverse().ToList();
      var cleanedSegments = new List<string>();
      int slashLen = urlSlashes.Count();

      for (int i = 0;  i < slashLen; i++)
      {
        string segment = urlSlashes[i];

        /* Split off and save anything that looks like a file type. */
        if (segment.IndexOf('.') != -1)
        {
          string possibleType = segment.Split('.')[1];

          /* If the type isn't alpha-only, it's probably not actually a file extension. */
          if (!Regex.IsMatch(possibleType, "[^a-zA-Z]"))
          {
            segment = segment.Split('.')[0];
          }
        }

        /*
         * EW-CMS specific segment replacement. Ugly.
         * Example: http://www.ew.com/ew/article/0,,20313460_20369436,00.html
        */
        if (segment.IndexOf(",00") != -1)
        {
          segment = segment.Replace(",00", "");
        }
            
        /* If our first or second segment has anything looking like a page number, remove it. */
        var pageNumRegex = new Regex("((_|-)?p[a-z]*|(_|-))[0-9]{1,2}$", RegexOptions.IgnoreCase);
        
        if (pageNumRegex.IsMatch(segment) && ((i == 1) || (i == 0)))
        {
          segment = pageNumRegex.Replace(segment, "");
        }
          
        bool del = false;

        /* If this is purely a number, and it's the first or second segment, it's probably a page number. Remove it. */
        if (i < 2 && Regex.IsMatch(segment, @"^[\d]{1,2}$"))
        {
          del = true;
        }
            
        /* If this is the first segment and it's just "index," remove it. */
        if (i == 0 && segment.ToLower() == "index")
        {
          del = true;
        }

        /* If tour first or second segment is smaller than 3 characters, and the first segment was purely alphas, remove it. */
        // TODO: Check these "purely alpha" regexes.  They don't seem right.
        if (i < 2 && segment.Length < 3 && !Regex.IsMatch(urlSlashes[0], "[a-z]", RegexOptions.IgnoreCase))
        {
          del = true;
        }

        /* If it's not marked for deletion, push it to cleanedSegments */
        if (!del)
        {
          cleanedSegments.Add(segment);
        }
      }

      /* This is our final, cleaned, base article URL. */
      cleanedSegments.Reverse();

      return string.Format("{0}://{1}{2}", protocol, hostname, String.Join("/", cleanedSegments.ToArray()));
    }

    internal void PrepareDocument(XDocument document)
    {
      /* In some cases a body element can't be found (if the HTML is totally hosed for example),
       * so we create a new body element and append it to the document. */
      var documentBody = GetOrCreateBody(document);
      var rootElement = document.Root;

      // TODO: handle HTML frames

      var elementsToRemove = new List<XElement>();

      /* Remove all scripts that are not readability. */
      elementsToRemove.Clear();

      rootElement.GetElementsByTagName("script")
        .ForEach(scriptElement =>
                   {
                     string scriptSrc = scriptElement.GetAttributeValue("src", null);

                     if (string.IsNullOrEmpty(scriptSrc) || scriptSrc.LastIndexOf("readability") == -1)
                     {
                       elementsToRemove.Add(scriptElement);
                     }
                   });

      RemoveElements(elementsToRemove);

      /* Remove all external stylesheets. */
      elementsToRemove.Clear();
      elementsToRemove.AddRange(
        rootElement.GetElementsByTagName("link")
          .Where(element => element.GetAttributeValue("rel", "").Trim().ToLower() == "stylesheet"
                         && element.GetAttributeValue("href", "").LastIndexOf("readability") == -1));
      RemoveElements(elementsToRemove);

      /* Remove all style tags. */
      elementsToRemove.Clear();
      elementsToRemove.AddRange(rootElement.GetElementsByTagName("style"));
      RemoveElements(elementsToRemove);

      /* Turn all double br's into p's and all font's into span's. */
      // TODO: optimize?
      string bodyInnerHtml = documentBody.GetInnerHtml();

      bodyInnerHtml = _ReplaceDoubleBrsRegex.Replace(bodyInnerHtml, "<p></p>");
      bodyInnerHtml = _ReplaceFontsRegex.Replace(bodyInnerHtml, "<$1span>");

      documentBody.SetInnerHtml(bodyInnerHtml);
    }

    internal XElement ExtractArticleTitle(XDocument document)
    {
      var documentBody = GetOrCreateBody(document);
      string documentTitle = document.GetTitle() ?? "";
      string currentTitle = documentTitle;

      if (_ArticleTitleDashRegex1.IsMatch(currentTitle))
      {
        currentTitle = _ArticleTitleDashRegex2.Replace(documentTitle, "$1");

        if (currentTitle.Split(' ').Length < _MinArticleTitleWordsCount1)
        {
          currentTitle = _ArticleTitleDashRegex3.Replace(documentTitle, "$1");
        }
      }
      else if (currentTitle.IndexOf(": ") != -1)
      {
        currentTitle = _ArticleTitleColonRegex1.Replace(documentTitle, "$1");

        if (currentTitle.Split(' ').Length < _MinArticleTitleWordsCount1)
        {
          currentTitle = _ArticleTitleColonRegex2.Replace(documentTitle, "$1");
        }
      }
      else if (currentTitle.Length > _MaxArticleTitleLength || currentTitle.Length < _MinArticleTitleLength)
      {
        var levelOneHeaders = documentBody.GetElementsByTagName("h1");

        if (levelOneHeaders.Count() == 1)
        {
          currentTitle = GetInnerText(levelOneHeaders.First());
        }
      }

      currentTitle = (currentTitle ?? "").Trim();

      if (currentTitle.Split(' ').Length <= _MinArticleTitleWordsCount2)
      {
        currentTitle = documentTitle;
      }

      if (string.IsNullOrEmpty(currentTitle))
      {
        return null;
      }

      var articleTitleElement = new XElement("h1");

      articleTitleElement.SetInnerHtml(currentTitle);

      return articleTitleElement;
    }

    internal XElement ExtractArticleContent(XDocument document)
    {
      StripUnlikelyCandidates(document);

      var candidatesForArticleContent = FindCandidatesForArticleContent(document);

      XElement topCandidateElement = DetermineTopCandidateElement(document, candidatesForArticleContent);
      XElement articleContentElement = CreateArticleContentElement(document, topCandidateElement);

      PrepareArticleContentElement(articleContentElement);

      return articleContentElement;
    }

    internal void GlueDocument(XDocument document, XElement articleTitleElement, XElement articleContentElement)
    {
      var documentBody = GetOrCreateBody(document);

      /* Include readability.css stylesheet. */
      var headElement = document.GetElementsByTagName("head").FirstOrDefault();

      if (headElement == null)
      {
        headElement = new XElement("head");
        documentBody.AddBeforeSelf(headElement);
      }

      var styleElement = new XElement("style");

      styleElement.SetAttributeValue("type", "text/css");

      var readabilityStylesheetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(_ReadabilityStylesheetResourceName);

      if (readabilityStylesheetStream == null)
      {
        throw new InternalErrorException("Couldn't load the NReadability stylesheet embedded resource.");
      }

      using (var sr = new StreamReader(readabilityStylesheetStream))
      {
        styleElement.SetInnerHtml(sr.ReadToEnd());
      }

      headElement.Add(styleElement);

      /* Apply reading style to body. */
      string readingStyleClass = GetReadingStyleClass(_readingStyle);

      documentBody.SetClass(readingStyleClass);
      documentBody.SetStyle("display: block;");

      /* Create inner div. */
      var innerDiv = new XElement("div");

      innerDiv.SetId(InnerDivId);
      innerDiv.SetClass(GetReadingMarginClass(_readingMargin) + " " + GetReadingSizeClass(_readingSize));

      if (articleTitleElement != null)
      {
        innerDiv.Add(articleTitleElement);
      }

      if (articleContentElement != null)
      {
        innerDiv.Add(articleContentElement);
      }

      /* Create overlay div. */
      var overlayDiv = new XElement("div");

      overlayDiv.SetId(OverlayDivId);
      overlayDiv.SetClass(readingStyleClass);
      overlayDiv.Add(innerDiv);

      /* Clear the old HTML, insert the new content. */
      documentBody.RemoveAll();
      documentBody.Add(overlayDiv);
    }

    internal void StripUnlikelyCandidates(XDocument document)
    {
      if (_dontStripUnlikelys)
      {
        return;
      }

      var rootElement = document.Root;

      new ElementsTraverser(
        element =>
          {
            string elementName = element.Name != null ? (element.Name.LocalName ?? "") : "";

            /* Remove unlikely candidates. */
            string unlikelyMatchString = element.GetClass() + element.GetId();

            if (unlikelyMatchString.Length > 0
             && !"body".Equals(elementName, StringComparison.OrdinalIgnoreCase)
             && !"a".Equals(elementName, StringComparison.OrdinalIgnoreCase)
             && _UnlikelyCandidatesRegex.IsMatch(unlikelyMatchString)
             && !_OkMaybeItsACandidateRegex.IsMatch(unlikelyMatchString))
            {
              var parentElement = element.Parent;

              if (parentElement != null)
              {
                element.Remove();
              }

              // element has been removed - we can go to the next one
              return;
            }

            /* Turn all divs that don't have children block level elements into p's or replace text nodes within the div with p's. */
            if ("div".Equals(elementName, StringComparison.OrdinalIgnoreCase))
            {
              if (!_DivToPElementsRegex.IsMatch(element.GetInnerHtml()))
              {
                // no block elements inside - change to p
                element.Name = "p";
              }
              else
              {
                // replace text nodes with p's (experimental)
                new ChildNodesTraverser(
                  childNode =>
                    {
                      if (childNode.NodeType != XmlNodeType.Text
                       || GetInnerText(childNode).Length == 0)
                      {
                        return;
                      }

                      var paraElement = new XElement("p");

                      // note that we're not using GetInnerText() here; instead we're getting raw InnerText to preserve whitespaces
                      paraElement.SetInnerHtml(((XText)childNode).Value);

                      paraElement.SetClass(ReadabilityStyledCssClass);
                      paraElement.SetStyle("display: inline;");

                      childNode.ReplaceWith(paraElement);
                    }
                  ).Traverse(element);
              }
            }
          }).Traverse(rootElement);
    }

    internal IEnumerable<XElement> FindCandidatesForArticleContent(XDocument document)
    {
      var paraElements = document.GetElementsByTagName("p");
      var candidateElements = new HashSet<XElement>();

      _elementsScores.Clear();

      foreach (var paraElement in paraElements)
      {
        string innerText = GetInnerText(paraElement);

        if (innerText.Length < _MinParagraphLength)
        {
          continue;
        }

        var parentElement = paraElement.Parent;
        var grandParentElement = parentElement != null ? parentElement.Parent : null;
        int score = 1; // 1 point for having a paragraph

        // Add points for any comma-segments within this paragraph.
        score += GetSegmentsCount(innerText, ',');

        // For every PARAGRAPH_SEGMENT_LENGTH characters in this paragraph, add another point. Up to MAX_POINTS_FOR_SEGMENTS_COUNT points.
        score += Math.Min(innerText.Length / _ParagraphSegmentLength, _MaxPointsForSegmentsCount);

        // Add the score to the parent.
        if (parentElement != null && (parentElement.Name == null || !"html".Equals(parentElement.Name.LocalName, StringComparison.OrdinalIgnoreCase)))
        {
          candidateElements.Add(parentElement);
          AddPointsToElementScore(parentElement, score);
        }

        // Add half the score to the grandparent.
        if (grandParentElement != null && (grandParentElement.Name == null || !"html".Equals(grandParentElement.Name.LocalName, StringComparison.OrdinalIgnoreCase)))
        {
          candidateElements.Add(grandParentElement);
          AddPointsToElementScore(grandParentElement, score / 2);
        }
      }

      return candidateElements;
    }

    internal XElement DetermineTopCandidateElement(XDocument document, IEnumerable<XElement> candidatesForArticleContent)
    {
      XElement topCandidateElement = null;

      foreach (var candidateElement in candidatesForArticleContent)
      {
        float candidateScore = GetElementScore(candidateElement);

        // Scale the final candidates score based on link density. Good content should have a
        // relatively small link density (5% or less) and be mostly unaffected by this operation.
        float newCandidateScore = (1.0f - GetLinksDensity(candidateElement)) * candidateScore;

        SetElementScore(candidateElement, newCandidateScore);

        if (topCandidateElement == null
         || newCandidateScore > GetElementScore(topCandidateElement))
        {
          topCandidateElement = candidateElement;
        }
      }

      if (topCandidateElement == null
       || "body".Equals(topCandidateElement.Name != null ? topCandidateElement.Name.LocalName : null, StringComparison.OrdinalIgnoreCase))
      {
        topCandidateElement = new XElement("div");

        var documentBody = GetOrCreateBody(document);

        topCandidateElement.Add(documentBody.Nodes());
      }

      return topCandidateElement;
    }

    internal XElement CreateArticleContentElement(XDocument document, XElement topCandidateElement)
    {
      /* Now that we have the top candidate, look through its siblings for content that might also be related.
       * Things like preambles, content split by ads that we removed, etc. */

      var articleContentElement = new XElement("div");

      articleContentElement.SetId(ContentDivId);

      var parentElement = topCandidateElement.Parent;

      if (parentElement == null)
      {
        // if the top candidate element has no parent, it means that it's an element created by us and detached from the document,
        // so we don't analyze its siblings and just attach it to the article content
        articleContentElement.Add(topCandidateElement);

        return articleContentElement;
      }

      IEnumerable<XElement> siblingElements = parentElement.Elements();

      float topCandidateElementScore = GetElementScore(topCandidateElement);
      float siblingScoreThreshold =
        Math.Max(
          _MaxSiblingScoreTreshold,
          _SiblingScoreTresholdCoefficient * topCandidateElementScore);
      
      string topCandidateClass = topCandidateElement.GetClass();

      // iterate through the sibling elements and decide whether append them
      foreach (var siblingElement in siblingElements)
      {
        bool append = false;
        string siblingElementName = siblingElement.Name != null ? (siblingElement.Name.LocalName ?? "") : "";
        float contentBonus = 0;

        // Give a bonus if sibling nodes and top canidates have the same class name
        if (!string.IsNullOrEmpty(topCandidateClass) && siblingElement.GetClass() == topCandidateClass)
        {
          contentBonus += topCandidateElementScore * _SiblingScoreTresholdCoefficient;
        }

        if (siblingElement == topCandidateElement)
        {
          // we'll append the article content element (created from the top candidate element during an earlier step)
          append = true;
        }
        else if ((GetElementScore(siblingElement) + contentBonus) >= siblingScoreThreshold)
        {
          // we'll append this element if the calculated score is higher than a treshold (derived from the score of the top candidate element)
          append = true;
        }
        else if ("p".Equals(siblingElementName, StringComparison.OrdinalIgnoreCase))
        {
          // we have to somehow decide whether we should append this paragraph

          string siblingElementInnerText = GetInnerText(siblingElement);

          // we won't append an empty paragraph
          if (siblingElementInnerText.Length > 0)
          {
            int siblingElementInnerTextLength = siblingElementInnerText.Length;

            if (siblingElementInnerTextLength >= _MinSiblingParagraphLength)
            {
              // we'll append this paragraph if the links density is not higher than a treshold
              append = GetLinksDensity(siblingElement) < _MaxSiblingParagraphLinksDensity;
            }
            else
            {
              // we'll append this paragraph if there are no links inside and if it contains a probable end of sentence indicator
              append = GetLinksDensity(siblingElement).IsCloseToZero()
                    && _EndOfSentenceRegex.IsMatch(siblingElementInnerText);
            }
          }
        }

        if (append)
        {
          XElement elementToAppend;

          if ("div".Equals(siblingElementName, StringComparison.OrdinalIgnoreCase)
           || "p".Equals(siblingElementName, StringComparison.OrdinalIgnoreCase))
          {
            elementToAppend = siblingElement;
          }
          else
          {
            /* We have an element that isn't a common block level element, like a form or td tag.
             * Turn it into a div so it doesn't get filtered out later by accident. */

            elementToAppend = new XElement("div");
            elementToAppend.SetId(siblingElement.GetId());
            elementToAppend.SetClass(siblingElement.GetClass());
            elementToAppend.Add(siblingElement.Nodes());
          }

          articleContentElement.Add(elementToAppend);
        }
      }

      return articleContentElement;
    }

    internal void PrepareArticleContentElement(XElement articleContentElement)
    {
      CleanStyles(articleContentElement);
      KillBreaks(articleContentElement);

      /* Clean out junk from the article content. */
      Clean(articleContentElement, "form");
      Clean(articleContentElement, "object");
      Clean(articleContentElement, "h1");

      /* If there is only one h2, they are probably using it as a header and not a subheader,
       * so remove it since we already have a header. */
      if (articleContentElement.GetElementsByTagName("h2").Count() == 1)
      {
        Clean(articleContentElement, "h2");
      }

      Clean(articleContentElement, "iframe");
      CleanHeaders(articleContentElement);

      /* Do these last as the previous stuff may have removed junk that will affect these. */
      CleanConditionally(articleContentElement, "table");
      CleanConditionally(articleContentElement, "ul");
      CleanConditionally(articleContentElement, "div");

      /* Remove extra paragraphs. */
      var paraElements = articleContentElement.GetElementsByTagName("p");
      var elementsToRemove = new List<XElement>();

      foreach (var paraElement in paraElements)
      {
        string innerText = GetInnerText(paraElement, false);
        if (innerText.Length > 0) { continue; }

        int imgsCount = paraElement.GetElementsByTagName("img").Count();
        if (imgsCount > 0) { continue; }

        int embedsCount = paraElement.GetElementsByTagName("embed").Count();
        if (embedsCount > 0) { continue; }

        int objectsCount = paraElement.GetElementsByTagName("object").Count();
        if (objectsCount > 0) { continue; }

        // We have a paragraph with empty inner text, with no images, no embeds and no objects.
        // Let's remove it.
        elementsToRemove.Add(paraElement);
      }

      RemoveElements(elementsToRemove);

      /* Remove br's that are directly before paragraphs. */
      articleContentElement.SetInnerHtml(_BreakBeforeParagraphRegex.Replace(articleContentElement.GetInnerHtml(), "<p"));
    }

    internal float GetLinksDensity(XElement element)
    {
      string elementInnerText = GetInnerText(element);
      int elementInnerTextLength = elementInnerText.Length;

      if (elementInnerTextLength == 0)
      {
        // we won't divide by zero
        return 0.0f;
      }

      int linksLength =
        element.GetElementsByTagName("a")
          .Sum(anchorElement => GetInnerText(anchorElement).Length);

      return (float)linksLength / elementInnerTextLength;
    }

    internal int GetSegmentsCount(string s, char ch)
    {
      return s.Count(c => c == ch) + 1;
    }

    /// <summary>
    /// Get "class/id weight" of the given <paramref name="element" />. Uses regular expressions to tell if this element looks good or bad.
    /// </summary>
    internal int GetClassWeight(XElement element)
    {
      if (_dontWeightClasses)
      {
        return 0;
      }

      int weight = 0;

      /* Look for a special classname. */
      string elementClass = element.GetClass();

      if (elementClass.Length > 0)
      {
        if (_NegativeWeightRegex.IsMatch(elementClass))
        {
          weight -= 25;
        }

        if (_PositiveWeightRegex.IsMatch(elementClass))
        {
          weight += 25;
        }
      }

      /* Look for a special ID */
      string elementId = element.GetId();

      if (elementId.Length > 0)
      {
        if (_NegativeWeightRegex.IsMatch(elementId))
        {
          weight -= 25;
        }

        if (_PositiveWeightRegex.IsMatch(elementId))
        {
          weight += 25;
        }
      }

      return weight;
    }

    internal string GetInnerText(XNode node, bool dontNormalizeSpaces)
    {
      if (node == null)
      {
        throw new ArgumentNullException("node");
      }

      string result;

      if (node is XElement)
      {
        result = ((XElement)node).Value;
      }
      else if (node is XText)
      {
        result = ((XText)node).Value;
      }
      else
      {
        throw new NotSupportedException(string.Format("Nodes of type '{0}' are not supported.", node.GetType()));
      }

      result = (result ?? "").Trim();

      if (!dontNormalizeSpaces)
      {
        return _NormalizeSpacesRegex.Replace(result, " ");
      }

      return result;
    }

    internal string GetInnerText(XNode node)
    {
      return GetInnerText(node, _dontNormalizeSpacesInTextContent);
    }

    /// <summary>
    /// Removes extraneous break tags from a <paramref name="element" />.
    /// </summary>
    internal void KillBreaks(XElement element)
    {
      element.SetInnerHtml(_KillBreaksRegex.Replace(element.GetInnerHtml(), "<br />"));
    }

    /// <summary>
    /// Cleans an element of all elements with name <paramref name="elementName" />.
    /// (Unless it's a youtube/vimeo video. People love movies.)
    /// </summary>
    internal void Clean(XElement rootElement, string elementName)
    {
      var elements = rootElement.GetElementsByTagName(elementName);
      bool isEmbed = "object".Equals(elementName, StringComparison.OrdinalIgnoreCase)
                  || "embed".Equals(elementName, StringComparison.OrdinalIgnoreCase);
      var elementsToRemove = new List<XElement>();

      foreach (var element in elements)
      {
        /* Allow youtube and vimeo videos through as people usually want to see those. */
        if (isEmbed
         && (_VideoRegex.IsMatch(element.GetAttributesString("|"))
          || _VideoRegex.IsMatch(element.GetInnerHtml())))
        {
          continue;
        }

        elementsToRemove.Add(element);
      }

      RemoveElements(elementsToRemove);
    }

    /// <summary>
    /// Cleans a <paramref name="rootElement" /> of all elements with name <paramref name="elementName" /> if they look fishy.
    /// "Fishy" is an algorithm based on content length, classnames, link density, number of images and embeds, etc.
    /// </summary>
    internal void CleanConditionally(XElement rootElement, string elementName)
    {
      if (elementName == null)
      {
        throw new ArgumentNullException("elementName");
      }

      var elements = rootElement.GetElementsByTagName(elementName);
      var elementsToRemove = new List<XElement>();

      foreach (var element in elements)
      {
        int weight = GetClassWeight(element);
        float score = GetElementScore(element);

        if (weight + score < 0.0f)
        {
          elementsToRemove.Add(element);
          continue;
        }

        /* If there are not very many commas and the number of non-paragraph elements
         * is more than paragraphs or other ominous signs, remove the element. */

        string elementInnerText = GetInnerText(element);

        if (GetSegmentsCount(elementInnerText, ',') < _MinCommaSegments)
        {
          int psCount = element.GetElementsByTagName("p").Count();
          int imgsCount = element.GetElementsByTagName("img").Count();
          int lisCount = element.GetElementsByTagName("li").Count();
          int inputsCount = element.GetElementsByTagName("input").Count();

          // while counting embeds we omit video-embeds
          int embedsCount =
            element.GetElementsByTagName("embed")
              .Count(embedElement => !_VideoRegex.IsMatch(embedElement.GetAttributeValue("src", "")));

          float linksDensity = GetLinksDensity(element);
          int innerTextLength = elementInnerText.Length;
          string elementNameLower = elementName.Trim().ToLower();
          bool remove = (imgsCount > psCount)
                     || (lisCount - _LisCountTreshold > psCount && elementNameLower != "ul" && elementNameLower != "ol")
                     || (inputsCount > psCount / 3)
                     || (innerTextLength < _MinInnerTextLength && (imgsCount == 0 || imgsCount > _MaxImagesInShortSegmentsCount))
                     || (weight < _ClassWeightTreshold && linksDensity > _MaxDensityForElementsWithSmallerClassWeight)
                     || (weight >= _ClassWeightTreshold && linksDensity > _MaxDensityForElementsWithGreaterClassWeight)
                     || (embedsCount > _MaxEmbedsCount || (embedsCount == _MaxEmbedsCount && innerTextLength < _MinInnerTextLengthInElementsWithEmbed));

          if (remove)
          {
            elementsToRemove.Add(element);
          }

        }
      } /* end foreach */

      RemoveElements(elementsToRemove);
    }

    /// <summary>
    /// Cleans out spurious headers from a <paramref name="element" />. Checks things like classnames and link density.
    /// </summary>
    internal void CleanHeaders(XElement element)
    {
      var elementsToRemove = new List<XElement>();

      for (int headerLevel = 1; headerLevel < 7; headerLevel++)
      {
        var headerElements = element.GetElementsByTagName("h" + headerLevel);

        foreach (var headerElement in headerElements)
        {
          if (GetClassWeight(headerElement) < 0
           || GetLinksDensity(headerElement) > _MaxHeaderLinksDensity)
          {
            elementsToRemove.Add(headerElement);
          }
        }
      }

      RemoveElements(elementsToRemove);
    }

    /// <summary>
    /// Removes the style attribute from the specified <paramref name="rootElement" /> and all elements underneath it.
    /// </summary>
    internal void CleanStyles(XElement rootElement)
    {
      new ElementsTraverser(
        element =>
          {
            string elementClass = element.GetClass();

            if (elementClass.Contains(ReadabilityStyledCssClass))
            {
              // don't remove the style if that's we who have styled this element
              return;
            }

            element.SetStyle(null);
          }).Traverse(rootElement);
    }

    internal string GetUserStyleClass(string prefix, string enumStr)
    {
      var suffixSB = new StringBuilder();
      bool wasUpperCaseCharacterSeen = false;

      enumStr.Aggregate(
        suffixSB,
        (sb, ch) =>
        {
          if (Char.IsUpper(ch))
          {
            if (wasUpperCaseCharacterSeen)
            {
              sb.Append('-');
            }

            wasUpperCaseCharacterSeen = true;

            sb.Append(Char.ToLower(ch));
          }
          else
          {
            sb.Append(ch);
          }

          return sb;
        });

      return string.Format("{0}-{1}", prefix, suffixSB).TrimEnd('-');
    }

    #endregion

    #region Private helper methods

    private static XElement GetOrCreateBody(XDocument document)
    {
      var documentBody = document.GetBody();

      if (documentBody == null)
      {
        var htmlElement = document.GetChildrenByTagName("html").FirstOrDefault();

        if (htmlElement == null)
        {
          htmlElement = new XElement("html");
          document.Add(htmlElement);
        }

        documentBody = new XElement("body");
        htmlElement.Add(documentBody);
      }

      return documentBody;
    }

    private static void RemoveElements(IEnumerable<XElement> elementsToRemove)
    {
      elementsToRemove.ForEach(elementToRemove => elementToRemove.Remove());
    }

    private static void ResolveElementsUrls(XDocument document, string tagName, string attributeName, string url, Func<AttributeTransformationInput, AttributeTransformationResult> attributeValueTransformer)
    {
      if (document == null)
      {
        throw new ArgumentNullException("document");
      }

      if (string.IsNullOrEmpty(url))
      {
        throw new ArgumentNullException("url");
      }

      var elements = document.GetElementsByTagName(tagName);

      foreach (var element in elements)
      {
        var attributeValue = element.GetAttributeValue(attributeName, null);

        if (attributeValue == null)
        {
          continue;
        }

        attributeValue = ResolveElementUrl(attributeValue, url);

        if (!string.IsNullOrEmpty(attributeValue))
        {
          AttributeTransformationResult attributeTransformationResult;

          if (attributeValueTransformer != null)
          {
            attributeTransformationResult = attributeValueTransformer.Invoke(new AttributeTransformationInput { AttributeValue = attributeValue, Element = element });
          }
          else
          {
            attributeTransformationResult = new AttributeTransformationResult { TransformedValue = attributeValue };
          }

          element.SetAttributeValue(attributeName, attributeTransformationResult.TransformedValue);

          if (!string.IsNullOrEmpty(attributeTransformationResult.OriginalValueAttributeName))
          {
            element.SetAttributeValue(attributeTransformationResult.OriginalValueAttributeName, attributeValue);
          }
        }
      }
    }

    private static string ResolveElementUrl(string url, string articleUrl)
    {
      if (url == null)
      {
        throw new ArgumentNullException();
      }

      if (_MailtoHrefRegex.IsMatch(url))
      {
        return url;
      }

      Uri baseUri;      

      if (!Uri.TryCreate(articleUrl, UriKind.Absolute, out baseUri))
      {
        return url;
      }

      /* If the link is simply a query string, then simply attach it to the original URL */
      if (url.StartsWith("?"))
      {        
        return baseUri.Scheme + "://" + baseUri.Host + baseUri.AbsolutePath + url;       
      }

      Uri absoluteUri;

      if (Uri.TryCreate(baseUri, url, out absoluteUri))
      {
        return absoluteUri.ToString();
      }

      return url;
    }

    private string GetReadingStyleClass(ReadingStyle readingStyle)
    {
      return GetUserStyleClass("style", readingStyle.ToString());
    }

    private string GetReadingMarginClass(ReadingMargin readingMargin)
    {
      return GetUserStyleClass("margin", readingMargin.ToString());
    }

    private string GetReadingSizeClass(ReadingSize readingSize)
    {
      return GetUserStyleClass("size", readingSize.ToString());
    }

    private void AddPointsToElementScore(XElement element, int pointsToAdd)
    {
      float currentScore = _elementsScores.ContainsKey(element) ? _elementsScores[element] : 0.0f;

      _elementsScores[element] = currentScore + pointsToAdd;
    }

    private float GetElementScore(XElement element)
    {
      return _elementsScores.ContainsKey(element) ? _elementsScores[element] : 0.0f;
    }

    private void SetElementScore(XElement element, float score)
    {
      _elementsScores[element] = score;
    }

    #endregion

    #region Properties

    ///<summary>
    /// A function to transform the value of 'src' attribute on 'img' elements. Can be null.
    ///</summary>
    public Func<AttributeTransformationInput, AttributeTransformationResult> ImageSourceTranformer
    {
      get { return _imageSourceTranformer; }
      set { _imageSourceTranformer = value; }
    }

    ///<summary>
    /// A function to transform the value of 'href' attribute on 'a' elements. Can be null.
    ///</summary>
    public Func<AttributeTransformationInput, AttributeTransformationResult> AnchorHrefTranformer
    {
      get { return _anchorHrefTransformer; }
      set { _anchorHrefTransformer = value; }
    }

    #endregion
  }
}
