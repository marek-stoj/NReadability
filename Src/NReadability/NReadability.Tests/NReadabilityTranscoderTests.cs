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
using NUnit.Framework;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Text;

namespace NReadability.Tests
{
  [TestFixture]
  public class NReadabilityTranscoderTests
  {
    private NReadabilityTranscoder _nReadabilityTranscoder;

    private static readonly SgmlDomBuilder _sgmlDomBuilder;
    private static readonly SgmlDomSerializer _sgmlDomSerializer;

    #region Constructor(s)

    static NReadabilityTranscoderTests()
    {
      _sgmlDomBuilder = new SgmlDomBuilder();
      _sgmlDomSerializer = new SgmlDomSerializer();
    }

    #endregion

    #region SetUp and TearDown

    [SetUp]
    public void SetUp()
    {
      _nReadabilityTranscoder = new NReadabilityTranscoder();
    }

    #endregion

    #region StripUnlikelyCandidates tests

    [Test]
    public void Unlikely_candidates_should_be_removed()
    {
      const string content = "<div class=\"sidebar\">Some content.</div>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      _nReadabilityTranscoder.StripUnlikelyCandidates(document);

      string newContent = _sgmlDomSerializer.SerializeDocument(document);

      AssertHtmlContentIsEmpty(newContent);
    }

    [Test]
    public void Unlikely_candidates_which_maybe_are_candidates_should_not_be_removed()
    {
      const string content = "<div id=\"article\" class=\"sidebar\"><a href=\"#\">Some widget</a></div>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      _nReadabilityTranscoder.StripUnlikelyCandidates(document);

      string newContent = _sgmlDomSerializer.SerializeDocument(document);

      AssertHtmlContentsAreEqual(content, newContent);
    }

    [Test]
    public void Text_nodes_within_a_div_with_block_elements_should_be_replaced_with_paragraphs()
    {
      const string content = "<div>text node1<a href=\"#\">Link</a>text node2</div>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      _nReadabilityTranscoder.StripUnlikelyCandidates(document);

      Assert.AreEqual(2, CountTags(document, "p"));
    }

    #endregion

    #region CollapseRedundantParagraphDivs tests

    [Test]
    public void Redundant_paragraph_divs_should_be_collapsed()
    {
      const string paragraph =
        "<p>" + "\r\n" +
        "    Some paragraph." + "\r\n" +
        "  </p>";

      const string content =
        "<div>" + "\r\n" +
        "  " + paragraph + "\r\n" +
        "</div>" + "\r\n";

      var document = _sgmlDomBuilder.BuildDocument(content);

      _nReadabilityTranscoder.CollapseRedundantParagraphDivs(document);

      string newContent = _sgmlDomSerializer.SerializeDocument(document);

      AssertHtmlContentsAreEqual(paragraph, newContent);
    }

    #endregion

    #region GetLinksDensity tests

    [Test]
    public void Element_with_no_links_should_have_links_density_equal_to_zero()
    {
      const string content = "<div id=\"container\"></div>";
      var document = _sgmlDomBuilder.BuildDocument(content);
      float linksDensity = _nReadabilityTranscoder.GetLinksDensity(document.GetElementById("container"));

      AssertFloatsAreEqual(0.0f, linksDensity);
    }

    [Test]
    public void Element_consisting_of_only_a_link_should_have_links_density_equal_to_one()
    {
      const string content = "<div id=\"container\"><a href=\"#\">some link</a></div>";
      var document = _sgmlDomBuilder.BuildDocument(content);
      float linksDensity = _nReadabilityTranscoder.GetLinksDensity(document.GetElementById("container"));

      AssertFloatsAreEqual(1.0f, linksDensity);
    }

    [Test]
    public void Element_containing_a_link_length_of_which_is_half_the_element_length_should_have_links_density_equal_to_half()
    {
      const string content = "<div id=\"container\"><a href=\"#\">some link</a>some link</div>";
      var document = _sgmlDomBuilder.BuildDocument(content);
      float linksDensity = _nReadabilityTranscoder.GetLinksDensity(document.GetElementById("container"));

      AssertFloatsAreEqual(0.5f, linksDensity);
    }

    #endregion

    #region DetermineTopCandidateElement tests

    [Test]
    public void Top_candidate_element_should_be_possible_to_determine_even_if_body_is_not_present()
    {
      const string content = "";
      var document = _sgmlDomBuilder.BuildDocument(content);

      List<XElement> candidatesForArticleContent =
        _nReadabilityTranscoder.FindCandidatesForArticleContent(document)
          .ToList();

      Assert.AreEqual(0, candidatesForArticleContent.Count);

      var topCandidateElement = _nReadabilityTranscoder.DetermineTopCandidateElement(document, candidatesForArticleContent);

      Assert.IsNotNull(topCandidateElement);
    }

    [Test]
    public void DetermineTopCandidateElement_should_fallback_to_body_if_there_are_no_candidates()
    {
      const string content = "<body><p>Some paragraph.</p><p>Some paragraph.</p>some text</body>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      List<XElement> candidatesForArticleContent =
        _nReadabilityTranscoder.FindCandidatesForArticleContent(document)
          .ToList();

      Assert.AreEqual(0, candidatesForArticleContent.Count);

      var topCandidateElement = _nReadabilityTranscoder.DetermineTopCandidateElement(document, candidatesForArticleContent);

      Assert.IsNotNull(topCandidateElement);
      Assert.AreEqual(3, topCandidateElement.Nodes().Count());
      Assert.AreEqual("p", ((XElement)topCandidateElement.Nodes().First()).Name.LocalName);
      Assert.AreEqual("p", ((XElement)topCandidateElement.Nodes().Skip(1).First()).Name.LocalName);
      Assert.AreEqual(XmlNodeType.Text, topCandidateElement.Nodes().Skip(2).First().NodeType);
    }

    [Test]
    public void DetermineTopCandidateElement_should_choose_a_container_with_longer_paragraph()
    {
      const string content = "<div id=\"first-div\"><p>Praesent in arcu vitae erat sodales consequat. Nam tellus purus, volutpat ac elementum tempus, sagittis sed lacus. Sed lacus ligula, sodales id vehicula at, semper a turpis. Curabitur et augue odio, sed auctor massa. Ut odio massa, fringilla eu elementum sit amet, eleifend congue erat. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed ultrices turpis dignissim metus porta id iaculis purus facilisis. Curabitur auctor purus eu nulla venenatis non ultrices nibh venenatis. Aenean dapibus pellentesque felis, ac malesuada nibh fringilla malesuada. In non mi vitae ipsum vehicula adipiscing. Sed a velit ipsum. Sed at velit magna, in euismod neque. Proin feugiat diam at lectus dapibus sed malesuada orci malesuada. Mauris sit amet orci tortor. Sed mollis, turpis in cursus elementum, sapien ante semper leo, nec venenatis velit sapien id elit. Praesent vel nulla mauris, nec tincidunt ipsum. Nulla at augue vestibulum est elementum sodales.</p></div><div id=\"second-div\"><p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Proin lacus ipsum, blandit sit amet cursus ut, posuere quis velit. Vivamus ut lectus quam, venenatis posuere erat. Sed pellentesque suscipit rhoncus. Vestibulum dictum est ut elit molestie vel facilisis dui tincidunt. Nulla adipiscing metus in nulla condimentum non mattis lacus tempus. Phasellus sed ipsum in felis molestie molestie. Sed sagittis massa orci, ut sagittis sem. Cras eget feugiat nulla. Nunc lacus turpis, porttitor eget congue quis, accumsan sed nunc. Vivamus imperdiet luctus molestie. Suspendisse eu est sed ligula pretium blandit. Proin eget metus nisl, at convallis metus. In commodo nibh a arcu pellentesque iaculis. Cras tincidunt vehicula malesuada. Duis tellus mi, ultrices sit amet dapibus sit amet, semper ac elit. Cras lobortis, urna eget consectetur consectetur, enim velit tempus neque, et tincidunt risus quam id mi. Morbi sit amet odio magna, vitae tempus sem. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Curabitur at lectus sit amet augue tincidunt ornare sed vitae lorem. Cum sociis natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus.</p></div>";
      var document = _sgmlDomBuilder.BuildDocument(content);
      List<XElement> candidatesForArticleContent =
        _nReadabilityTranscoder.FindCandidatesForArticleContent(document)
          .ToList();

      Assert.AreEqual(2, candidatesForArticleContent.Count);

      var topCandidateElement = _nReadabilityTranscoder.DetermineTopCandidateElement(document, candidatesForArticleContent);

      Assert.IsNotNull(topCandidateElement);
      Assert.AreEqual("second-div", topCandidateElement.GetId());
    }

    #endregion

    #region CreateArticleContent tests

    [Test]
    public void CreateArticleContent_should_work_even_if_html_content_is_empty()
    {
      const string content = "";
      var document = _sgmlDomBuilder.BuildDocument(content);
      var candidatesForArticleContent = _nReadabilityTranscoder.FindCandidatesForArticleContent(document);
      var topCandidateElement = _nReadabilityTranscoder.DetermineTopCandidateElement(document, candidatesForArticleContent);

      Assert.IsNotNull(topCandidateElement);

      var articleContentElement = _nReadabilityTranscoder.CreateArticleContentElement(document, topCandidateElement);

      Assert.IsNotNull(articleContentElement);
      Assert.AreEqual("div", articleContentElement.Name.LocalName);
      Assert.IsNotNullOrEmpty(articleContentElement.GetId());

      // only one empty div should be inside
      Assert.AreEqual(1, articleContentElement.Nodes().Count());
    }

    [Test]
    public void CreateArticleContent_should_extract_a_paragraph()
    {
      const string content = "<div id=\"first-div\"><p>Praesent in arcu vitae erat sodales consequat. Nam tellus purus, volutpat ac elementum tempus, sagittis sed lacus. Sed lacus ligula, sodales id vehicula at, semper a turpis. Curabitur et augue odio, sed auctor massa. Ut odio massa, fringilla eu elementum sit amet, eleifend congue erat. Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed ultrices turpis dignissim metus porta id iaculis purus facilisis. Curabitur auctor purus eu nulla venenatis non ultrices nibh venenatis. Aenean dapibus pellentesque felis, ac malesuada nibh fringilla malesuada. In non mi vitae ipsum vehicula adipiscing. Sed a velit ipsum. Sed at velit magna, in euismod neque. Proin feugiat diam at lectus dapibus sed malesuada orci malesuada. Mauris sit amet orci tortor. Sed mollis, turpis in cursus elementum, sapien ante semper leo, nec venenatis velit sapien id elit. Praesent vel nulla mauris, nec tincidunt ipsum. Nulla at augue vestibulum est elementum sodales.</p></div><div id=\"\">some text</div>";
      var document = _sgmlDomBuilder.BuildDocument(content);
      var candidatesForArticleContent = _nReadabilityTranscoder.FindCandidatesForArticleContent(document);
      var topCandidateElement = _nReadabilityTranscoder.DetermineTopCandidateElement(document, candidatesForArticleContent);

      Assert.IsNotNull(topCandidateElement);

      var articleContentElement = _nReadabilityTranscoder.CreateArticleContentElement(document, topCandidateElement);

      Assert.IsNotNull(articleContentElement);
      Assert.AreEqual("div", articleContentElement.Name.LocalName);
      Assert.AreEqual(1, articleContentElement.Nodes().Count());
      Assert.AreEqual("first-div", ((XElement)articleContentElement.Nodes().First()).GetId());
      Assert.AreEqual(1, ((XElement)articleContentElement.Nodes().First()).Nodes().Count());
      Assert.AreEqual("p", ((XElement)((XElement)articleContentElement.Nodes().First()).Nodes().First()).Name.LocalName);
    }

    #endregion

    #region PrepareDocument tets

    [Test]
    public void PrepareDocument_should_create_body_tag_if_it_is_not_present()
    {
      const string content = "";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.IsNull(document.GetBody());

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.IsNotNull(document.GetBody());
    }

    [Test]
    public void PrepareDocument_should_remove_scripts_and_stylesheets()
    {
      const string content = "<html><head><link rel=\"StyleSheet\" href=\"#\" /><style></style><style /><style type=\"text/css\"></style></head><body><script type=\"text/javascript\"></script><script type=\"text/javascript\" /><style type=\"text/css\"></style><link rel=\"styleSheet\"></link><script></script></body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.Greater(CountTags(document, "script", "style", "link"), 0);

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.AreEqual(0, CountTags(document, "script", "style", "link"));
    }

    [Test]
    public void PrepareDocument_should_remove_empty_noscript_between_head_and_body()
    {
      const string content = "<html><head></head><noscript /><body>abc</body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.Greater(CountTags(document, "noscript"), 0);

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.AreEqual(0, CountTags(document, "noscript"));
    }

    [Test]
    public void PrepareDocument_should_remove_non_empty_noscript_between_head_and_body()
    {
      const string content = "<html><head></head><noscript>abc</noscript><body>abc</body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.Greater(CountTags(document, "noscript"), 0);

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.AreEqual(0, CountTags(document, "noscript"));
    }

    [Test]
    public void PrepareDocument_should_remove_empty_noscript_in_body()
    {
      const string content = "<html><head></head><body><noscript />abc</body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.Greater(CountTags(document, "noscript"), 0);

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.AreEqual(0, CountTags(document, "noscript"));
    }

    [Test]
    public void PrepareDocument_should_remove_non_empty_noscript_in_body()
    {
      const string content = "<html><head></head><body><noscript>abc</noscript>abc</body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.Greater(CountTags(document, "noscript"), 0);

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.AreEqual(0, CountTags(document, "noscript"));
    }

    [Test]
    public void PrepareDocument_should_not_remove_neither_readability_scripts_nor_stylesheets()
    {
      const string content = "<html><head><link rel=\"stylesheet\" href=\"http://domain.com/readability.css\" /><script src=\"http://domain.com/readability.js\"></script></head><body><script src=\"http://domain.com/readability.js\"></script><link rel=\"stylesheet\" href=\"http://domain.com/readability.css\" /></body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      int countBefore = CountTags(document, "script", "link");

      _nReadabilityTranscoder.PrepareDocument(document);

      int countAfter = CountTags(document, "script", "link");

      Assert.AreEqual(countBefore, countAfter);
    }

    [Test]
    public void PrepareDocument_should_replace_double_br_tags_with_p_tags()
    {
      const string content = "<html><body>some text<br /><br />some other text</body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.AreEqual(0, CountTags(document, "p"));
      Assert.Greater(CountTags(document, "br"), 0);

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.AreEqual(0, CountTags(document, "br"));
      Assert.AreEqual(1, CountTags(document, "p"));
    }

    [Test]
    public void PrepareDocument_should_replace_font_tags_with_span_tags()
    {
      const string content = "<html><body><font>some text</font></body></html>";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.AreEqual(0, CountTags(document, "span"));
      Assert.Greater(CountTags(document, "font"), 0);

      _nReadabilityTranscoder.PrepareDocument(document);

      Assert.AreEqual(0, CountTags(document, "font"));
      Assert.AreEqual(1, CountTags(document, "span"));
    }

    #endregion

    #region GlueDocument tests

    [Test]
    public void GlueDocument_should_include_head_element_if_it_is_not_present()
    {
      const string content = "";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.AreEqual(0, CountTags(document, "head"));

      _nReadabilityTranscoder.GlueDocument(document, null, document.GetBody());

      Assert.AreEqual(1, CountTags(document, "head"));
    }

    [Test]
    public void GlueDocument_should_include_readability_stylesheet()
    {
      const string content = "";
      var document = _sgmlDomBuilder.BuildDocument(content);

      Assert.AreEqual(0, CountTags(document, "style"));

      _nReadabilityTranscoder.GlueDocument(document, null, document.GetBody());

      Assert.AreEqual(1, CountTags(document, "style"));
    }

    [Test]
    public void GlueDocument_should_create_appropriate_containers_structure()
    {
      const string content = "";
      var document = _sgmlDomBuilder.BuildDocument(content);

      _nReadabilityTranscoder.GlueDocument(document, null, document.GetBody());

      Assert.IsNotNull(document.GetElementById(NReadabilityTranscoder.OverlayDivId));
      Assert.IsNotNull(document.GetElementById(NReadabilityTranscoder.InnerDivId));
    }

    #endregion

    #region GetUserStyleClass tests

    [Test]
    public void TestGetUserStyleClass()
    {
      Assert.AreEqual("prefix", _nReadabilityTranscoder.GetUserStyleClass("prefix", ""));
      Assert.AreEqual("prefix-abc", _nReadabilityTranscoder.GetUserStyleClass("prefix", "abc"));
      Assert.AreEqual("prefix-abc", _nReadabilityTranscoder.GetUserStyleClass("prefix", "Abc"));
      Assert.AreEqual("prefix-a-bc", _nReadabilityTranscoder.GetUserStyleClass("prefix", "ABc"));
      Assert.AreEqual("prefix-a-bc-d", _nReadabilityTranscoder.GetUserStyleClass("prefix", "ABcD"));
    }

    #endregion

    #region Transcode tests

    [Test]
    [Sequential]
    // TODO: if time, add test case 7 (the sample is already in the repo but needs fixing)
    // TODO IMM HI: uncomment tests 12, 13
    //public void TestSampleInputs([Values(1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14)]int sampleInputNumber)
    public void TestSampleInputs([Values(1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 14, 15)]int sampleInputNumber)
    {
      // arrange
      string sampleInputNumberStr = sampleInputNumber.ToString().PadLeft(2, '0');
      string content = File.ReadAllText(string.Format(@"SampleInput\SampleInput_{0}.html", sampleInputNumberStr));
      var transcodingInput = new TranscodingInput(content);

      transcodingInput.Url = GetSampleInputUrl(sampleInputNumber);

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);

      const string outputDir = "SampleOutput";

      if (!Directory.Exists(outputDir))
      {
        Directory.CreateDirectory(outputDir);
      }

      string extractedContent = transcodingResult.ExtractedContent;

      File.WriteAllText(
        Path.Combine(outputDir, string.Format("SampleOutput_{0}.html", sampleInputNumberStr)),
        extractedContent,
        Encoding.UTF8);

      switch (sampleInputNumber)
      {
        case 1: // washingtonpost.com - "Court Puts Off Decision On Indefinite Detention"
          Assert.IsTrue(extractedContent.Contains("The Supreme Court yesterday vacated a lower"));
          Assert.IsTrue(extractedContent.Contains("The justices did not rule on the merits"));
          Assert.IsTrue(extractedContent.Contains("But the government said the issues were now"));
          break;

        case 2: // devBlogi.pl - "Po co nam testerzy?"
          Assert.IsTrue(extractedContent.Contains("Moja siostra sprawiła swoim dzieciom szczeniaczka"));
          Assert.IsTrue(extractedContent.Contains("Z tresowaniem psów jest tak, że reakcja musi być"));
          Assert.IsTrue(extractedContent.Contains("Korzystając z okazji, chcielibyśmy dowiedzieć się"));
          break;

        case 3: // codinghorror.com - "Welcome Back Comments"
          Assert.IsTrue(extractedContent.Contains("I apologize for the scarcity of updates lately."));
          Assert.IsTrue(extractedContent.Contains("Most of all, I blame myself."));
          Assert.IsTrue(extractedContent.Contains("And, most of all, thanks to"));
          break;

        case 4: // sample page; only with paragraphs
          Assert.IsTrue(extractedContent.Contains("Lorem ipsum dolor sit amet, consectetur adipiscing elit."));
          Assert.IsTrue(extractedContent.Contains("Mauris nec massa ante, id fringilla nisi."));
          Assert.IsTrue(extractedContent.Contains("Nulla facilisi. Proin lacinia venenatis elit, nec ornare elit varius eu."));
          Assert.IsTrue(extractedContent.Contains("Duis vitae ultricies nibh."));
          Assert.IsTrue(extractedContent.Contains("Vestibulum dictum iaculis nisl, lobortis luctus justo porttitor eu."));
          break;

        case 5: // mnmlist.com - "clear distractions"
          Assert.IsTrue(extractedContent.Contains("When it comes to minimalism in"));
          Assert.IsTrue(extractedContent.Contains("Here’s how:"));
          Assert.IsTrue(extractedContent.Contains("Set limits on your work hours. If your time is limited, you’ll find ways to make the most of that limited time."));
          break;

        case 6: // sample page; nbsp
          Assert.IsTrue(extractedContent.Contains("1.  Item 1.")); // there's a non-breaking space here
          break;

        case 7: // http://nplusonemag.com/treasure-island
          Assert.IsTrue(extractedContent.Contains("stretched out storylines"));
          Assert.IsTrue(extractedContent.Contains("It is no longer a smart social move to brag about not owning a television."));
          Assert.IsTrue(extractedContent.Contains("Of course, some habits can be hard to give up completely."));
          break;

        case 8:  // NYTimes leading paragraph
          Assert.IsTrue(extractedContent.Contains("freed from house arrest on Saturday, setting her on the path"));
          Assert.IsTrue(extractedContent.Contains("confrontation with the generals who had kept her out of the public eye"));
          Assert.IsTrue(extractedContent.Contains("Western capitals was one of celebration"));
          break;

        case 9:  // http://www.udidahan.com/2010/08/31/race-conditions-dont-exist/ - rich sidebar should not be identified as main content
          Assert.IsTrue(extractedContent.Contains("Not in the business world anyway."));
          Assert.IsTrue(extractedContent.Contains("we could look at modeling the acceptance"));
          Assert.IsTrue(extractedContent.Contains("Keep an eye out."));
          break;

        case 10:  // http://www.slate.com/articles/technology/technology/2011/10/steve_jobs_biography_the_new_book_doesn_t_explain_what_made_the_.single.html
          Assert.IsTrue(extractedContent.Contains("In the aftermath of his resignation and then his death"));
          Assert.IsTrue(extractedContent.Contains("It turns out, though, that he was much worse than you ever suspected."));
          Assert.IsTrue(extractedContent.Contains("But Isaacson has compiled so many instances"));
          Assert.IsTrue(extractedContent.Contains("Yet Jobs also said that he wanted a biographer"));
          Assert.IsTrue(extractedContent.Contains("He embodied so many contradictions"));
          Assert.IsTrue(extractedContent.Contains("When friends and colleagues offer theories about Jobs"));
          Assert.IsTrue(extractedContent.Contains("Isaacson tries valiantly to add some depth to the profile."));
          Assert.IsTrue(extractedContent.Contains("Jobs also seemed to suspect that he wasn"));
          Assert.IsTrue(extractedContent.Contains("Instead of offering any substantive explanations"));
          Assert.IsTrue(extractedContent.Contains("death prompted a flurry of hagiographic tributes"));
          Assert.IsTrue(extractedContent.Contains("last 15 years of life, something in him changed"));
          break;

        case 11: // http://www.slate.com/articles/news_and_politics/foreigners/2011/10/jordan_s_king_abdullah_interviewed_the_arab_spring_is_a_disaster.single.html
          Assert.IsTrue(extractedContent.Contains("How do you see"));
          Assert.IsTrue(extractedContent.Contains("I went to Egypt after visiting"));
          Assert.IsTrue(extractedContent.Contains("How did your visit to Egypt go?"));
          Assert.IsTrue(extractedContent.Contains("We had a very good meeting."));
          Assert.IsTrue(extractedContent.Contains("I think it is astounding that Tantawi"));
          Assert.IsTrue(extractedContent.Contains("The feeling I got from the Egyptian leadership"));
          Assert.IsTrue(extractedContent.Contains("From the streets"));
          Assert.IsTrue(extractedContent.Contains("No, from the West."));
          Assert.IsTrue(extractedContent.Contains("They saw that Mubarak was sacrificed"));
          Assert.IsTrue(extractedContent.Contains("So they are being very cautious in the decisions they are taking."));
          Assert.IsTrue(extractedContent.Contains("Do you and other leaders"));
          Assert.IsTrue(extractedContent.Contains("I think everybody is wary"));
          Assert.IsTrue(extractedContent.Contains("And Jordan?"));
          Assert.IsTrue(extractedContent.Contains("I think two things make Jordan stand out."));
          Assert.IsTrue(extractedContent.Contains("Do you think President Bashi"));
          Assert.IsTrue(extractedContent.Contains("We have had very limited defectors"));
          Assert.IsTrue(extractedContent.Contains("Does that mean you have talked to"));
          Assert.IsTrue(extractedContent.Contains("I spoke to Bashar al-Assad twice in the springtime."));
          Assert.IsTrue(extractedContent.Contains("People are asking about an alternative"));
          Assert.IsTrue(extractedContent.Contains("I think nobody has an answer to Syria."));
          Assert.IsTrue(extractedContent.Contains("Do you think they can win?"));
          Assert.IsTrue(extractedContent.Contains("My view is when you use violence on your people"));
          Assert.IsTrue(extractedContent.Contains("What is your assessment of Libya"));
          Assert.IsTrue(extractedContent.Contains("It took everybody by surprise."));
          Assert.IsTrue(extractedContent.Contains("So you think the death of Colonel Gaddafi"));
          Assert.IsTrue(extractedContent.Contains("There is an old saying that peace"));
          Assert.IsTrue(extractedContent.Contains("I heard that Hamas leader Khalid Mashal"));
          Assert.IsTrue(extractedContent.Contains("If he comes here, it is part of looking at Palestinian reconciliation."));
          Assert.IsTrue(extractedContent.Contains("You support Palestinian President Mahmoud Abba"));
          Assert.IsTrue(extractedContent.Contains("It is out of desperation and frustration that they are going to the U.N."));
          Assert.IsTrue(extractedContent.Contains("Like our elections?"));
          Assert.IsTrue(extractedContent.Contains("It is a disaster. You have seen what has happened in Egypt"));
          Assert.IsTrue(extractedContent.Contains("The Israelis are worried the Egyptians will break the treaty."));
          Assert.IsTrue(extractedContent.Contains("That is a very, very strong possibility."));
          Assert.IsTrue(extractedContent.Contains("Do you intend to support Jordan"));
          Assert.IsTrue(extractedContent.Contains("We have a peace treaty with Israel and we will continue"));
          Assert.IsTrue(extractedContent.Contains("A lot of Israelis think your recent statements"));
          Assert.IsTrue(extractedContent.Contains("know if they are hostile. What I am saying is they are missing an opportunity"));
          Assert.IsTrue(extractedContent.Contains("I always look at the glass half full and I"));
          Assert.IsTrue(extractedContent.Contains("What did you think of Israel Prime Minister Benjamin Netanyahu"));
          Assert.IsTrue(extractedContent.Contains("It is politics at the end of the day."));
          Assert.IsTrue(extractedContent.Contains("It was strange for Israel to be negotiating with Hamas."));
          Assert.IsTrue(extractedContent.Contains("I think all of us have been asking each othe"));
          Assert.IsTrue(extractedContent.Contains("You just appointed a new prime minister."));
          Assert.IsTrue(extractedContent.Contains("Yes, for the past six months we have listened to what people want"));
          Assert.IsTrue(extractedContent.Contains("If you look five years down the line, do you see yourself relinquishing some power to the parliament"));
          Assert.IsTrue(extractedContent.Contains("Probably sooner. We haven"));
          Assert.IsTrue(extractedContent.Contains("You will still appoint the Senate"));
          Assert.IsTrue(extractedContent.Contains("There are two options. If there is a new parliament next year"));
          Assert.IsTrue(extractedContent.Contains("I think we are facing the same challenges as everyone in the West."));
          Assert.IsTrue(extractedContent.Contains("Once you have people rioting in the streets, how do you get foreign"));
          Assert.IsTrue(extractedContent.Contains("But you made a deal with the Saudis"));
          Assert.IsTrue(extractedContent.Contains("The Saudis have come through very strongly this year but"));
          Assert.IsTrue(extractedContent.Contains("And that is because you are having problems getting"));
          Assert.IsTrue(extractedContent.Contains("We are having problems because the gas pipeline keeps"));
          Assert.IsTrue(extractedContent.Contains("There are reports that over the next five years if you join the GCC"));
          Assert.IsTrue(extractedContent.Contains("There is going to be a package hopefully of at least a billion"));
          break;

        // TODO IMM HI: fix (problem with nested divs)
        case 12:  // http://www.telegraph.co.uk/comment/personal-view/8841737/What-Gilad-Shalit-tells-us-about-the-respect-for-life-in-Europe-Israel-and-Palestine.html
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*One of the supreme ironies among the European moral stances"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*And yet when that same Europe turns its gaze on the Middle East"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Normally, this would not be even worth mentioning."));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Israel first outlawed the death penalty in 1954"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Note that Israel passed this law five years"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*If the Israelis had hundreds of terrorists"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Palestine, on the other hand"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*The trade of over a thousand Palestinians"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*If a European, concerned about the nature"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*So instead of helping Europeans"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "at the return of prisoners, and"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "in order to present the moral equivalence of all the"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*In acquiescing with a narrative in which hatred and murder"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*It may seem cost-free to Westerners"));
          break;

        // TODO IMM HI: fix (problem with nested divs)
        case 13:  // same URL as 12 but processed by Instapaper first
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*One of the supreme ironies among the European moral stances"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*And yet when that same Europe turns its gaze on the Middle East"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Normally, this would not be even worth mentioning."));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Israel first outlawed the death penalty in 1954"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Note that Israel passed this law five years"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*If the Israelis had hundreds of terrorists"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*Palestine, on the other hand"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*The trade of over a thousand Palestinians"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*If a European, concerned about the nature"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*So instead of helping Europeans"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "at the return of prisoners, and"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "in order to present the moral equivalence of all the"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*In acquiescing with a narrative in which hatred and murder"));
          Assert.IsTrue(Regex.IsMatch(extractedContent, "<p>\\s*It may seem cost-free to Westerners"));
          break;

        case 14: // http://www.theverge.com/2012/5/25/3042640/samsung-galaxy-s-iii-review
          Assert.IsTrue(extractedContent.Contains("Samsung stops teasing and finally delivers its flagship Android device"));
          Assert.IsTrue(extractedContent.Contains("The extra-large size of this phone, even with its great ergonomics, may prove to be"));
          break;

        case 15: // http://www.theverge.com/2012/6/21/3032067/casio-bluetooth-g-shock-watch-gb6900-review
          Assert.IsTrue(extractedContent.Contains("Bank devices in the 80s, but in recent years it hasn't been quite the innovator it once was. "));
          Assert.IsTrue(extractedContent.Contains("the Verge score is based on the average of the subscores below"));
          break;

        default:
          throw new NotSupportedException("Unknown sample input number (" + sampleInputNumber + "). Have you added another sample input? If so, then add appropriate asserts here as well.");
      }
    }

    [Test]
    public void TestReplacingImageUrls()
    {
      TestReplacingImageUrl("http://example.com/image.jpg", "http://immortal.pl/doc.html", "http://example.com/image.jpg");
      TestReplacingImageUrl("https://example.com/image.jpg", "http://immortal.pl", "https://example.com/image.jpg");
      TestReplacingImageUrl("ftp://example.com/image.jpg", "http://immortal.pl/doc.html", "ftp://example.com/image.jpg");
      TestReplacingImageUrl("A(*Sf6as7f 9A*(659A^SF 6987aSF", "http://immortal.pl/", "http://immortal.pl/A(*Sf6as7f 9A*(659A^SF 6987aSF");
      TestReplacingImageUrl("file:///C:/Users/Administrator/image.jpg", "http://immortal.pl/index.html", "file:///C:/Users/Administrator/image.jpg");

      TestReplacingImageUrl("image.png", "p//immortal.pl/", "image.png");
      TestReplacingImageUrl("image.png", "AS&F*(^ASF", "image.png");

      TestReplacingImageUrl("image.jpg", "http://immortal.pl", "http://immortal.pl/image.jpg");
      TestReplacingImageUrl("image.jpg", "http://immortal.pl/index.html", "http://immortal.pl/image.jpg");
      TestReplacingImageUrl("/image.jpg", "http://immortal.pl", "http://immortal.pl/image.jpg");
      TestReplacingImageUrl("/image.jpg", "http://immortal.pl/", "http://immortal.pl/image.jpg");

      TestReplacingImageUrl("static/gfx/image.gif", "http://immortal.pl", "http://immortal.pl/static/gfx/image.gif");
      TestReplacingImageUrl("static/gfx/image.gif", "http://immortal.pl/", "http://immortal.pl/static/gfx/image.gif");
      TestReplacingImageUrl("/static/gfx/image.gif", "http://immortal.pl", "http://immortal.pl/static/gfx/image.gif");
      TestReplacingImageUrl("/static/gfx/image.gif", "http://immortal.pl/", "http://immortal.pl/static/gfx/image.gif");

      TestReplacingImageUrl("/static/gfx/image.gif", "http://immortal.pl/article/doc.html", "http://immortal.pl/static/gfx/image.gif");

      TestReplacingImageUrl("static/gfx/image.gif", "http://immortal.pl/article", "http://immortal.pl/static/gfx/image.gif");
      TestReplacingImageUrl("static/gfx/image.gif", "http://immortal.pl/article/", "http://immortal.pl/article/static/gfx/image.gif");

      TestReplacingImageUrl("/static/gfx/image.gif", "http://immortal.pl/article/doc.html?someParam=1", "http://immortal.pl/static/gfx/image.gif");
      TestReplacingImageUrl("static/gfx/image.gif", "http://immortal.pl/article/", "http://immortal.pl/article/static/gfx/image.gif");

      TestReplacingImageUrl("image.png", "http://immortal.pl/article/doc.html", "http://immortal.pl/article/image.png");
      TestReplacingImageUrl("/image.png", "http://immortal.pl/article/doc.html", "http://immortal.pl/image.png");
      TestReplacingImageUrl("image.png", "http://immortal.pl/article/doc.html?someKey=some/Value?aksd", "http://immortal.pl/article/image.png");
      TestReplacingImageUrl("/image.png", "http://immortal.pl/article/doc.html?someKey=some/Value?aksd", "http://immortal.pl/image.png");

      // invalid base uris
      TestReplacingImageUrl("image.png", "immortal.pl/article/doc.html?someKey=some/Value?aksd", "image.png");
      TestReplacingImageUrl("image.png", "htt//immortal.pl/arti", "image.png");
      TestReplacingImageUrl("image.png", "http:immortal.pl", "image.png");
      TestReplacingImageUrl("image.png", "/immortal.pl", "image.png");
    }

    [Test]
    public void TestReplacingLinksUrls()
    {
      // arrange
      string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      string htmlContent = "<html><body>" + dummyParagraphs + "<p><a href=\"/wiki/article1\">link</a></p>" + dummyParagraphs + "</body></html>";

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://wikipedia.org/wiki/baseArticle",
          };

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("href=\"http://wikipedia.org/wiki/article1\""));
    }

    [Test]
    public void TestReplacingQueryStringLinkUrls()
    {
      // arrange
      string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      string htmlContent = "<html><body>" + dummyParagraphs + "<p><a href=\"?hello\">link</a></p>" + dummyParagraphs + "</body></html>";

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://wikipedia.org/wiki/baseArticle",
          };

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("href=\"http://wikipedia.org/wiki/baseArticle?hello\""));

      // arrange
      transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://wikipedia.org/wiki/baseArticle?goodbye",
          };

      // act
      transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("href=\"http://wikipedia.org/wiki/baseArticle?hello\""));
    }

    [Test]
    public void TestEmptyArticle()
    {
      // arrange
      const string htmlContent = "<html><body></body></html>";

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://wikipedia.org/wiki/baseArticle",
          };

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsFalse(transcodingResult.ContentExtracted);
    }

    [Test]
    public void TestMobileHeaders()
    {
      // arrange
      string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      string htmlContent = "<html><body>" + dummyParagraphs + "</body></html>";

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://wikipedia.org/wiki/baseArticle",
          };

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("<meta name=\"HandheldFriendly\" content=\"true\" />"));
    }

    [Test]
    public void MetaViewportElementShouldBeRemoved()
    {
      // arrange
      const string metaViewportElementStr = "<meta name=\"viewport\" content=\"width=1000\" />";
      const string htmlContent = "<html><head>" + metaViewportElementStr + "</head><body><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p></body></html>";

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://wikipedia.org/wiki/baseArticle",
          };

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);
      Assert.IsFalse(transcodingResult.ExtractedContent.Contains(metaViewportElementStr));
    }

    [Test]
    public void TestImageSourceTransformer()
    {
      // arrange
      Func<AttributeTransformationInput, AttributeTransformationResult> imgSrcTransformer =
        input =>
        new AttributeTransformationResult
          {
            TransformedValue = string.Format("http://imageresizer.com/u={0}", input.AttributeValue),
            OriginalValueAttributeName = "origsrc",
          };

      string originalSrcValue = "http://example.com/some_image.jpg";
      string expectedSrcValue = imgSrcTransformer.Invoke(new AttributeTransformationInput { AttributeValue = originalSrcValue, Element = null }).TransformedValue;

      string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      string htmlContent = "<html><body>" + dummyParagraphs + "<p><img src=\"" + originalSrcValue + "\" /></p>" + dummyParagraphs + "</body></html>";

      var nReadabilityTranscoder =
        new NReadabilityTranscoder
          {
            ImageSourceTranformer = imgSrcTransformer,
          };

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://immortal.pl/",
          };

      // act
      TranscodingResult transcodingResult = nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("src=\"" + expectedSrcValue + "\""));
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("origsrc=\"" + originalSrcValue + "\""));
    }

    [Test]
    public void TestAnchorHrefTransformer()
    {
      // arrange
      Func<AttributeTransformationInput, AttributeTransformationResult> anchorHrefTransformer =
        input =>
        new AttributeTransformationResult
          {
            TransformedValue = string.Format("http://redirector.com/u={0}", input.AttributeValue),
            OriginalValueAttributeName = "orighref",
          };

      string originalHrefValue = "http://example.com/some_article.html";
      string expectedHrefValue = anchorHrefTransformer.Invoke(new AttributeTransformationInput { AttributeValue = originalHrefValue, Element = null }).TransformedValue;

      string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      string htmlContent = "<html><body>" + dummyParagraphs + "<p><a href=\"" + originalHrefValue + "\">Some article</a></p>" + dummyParagraphs + "</body></html>";

      var nReadabilityTranscoder =
        new NReadabilityTranscoder
          {
            AnchorHrefTranformer = anchorHrefTransformer,
          };

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = "http://immortal.pl/",
          };

      // act
      TranscodingResult transcodingResult = nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("href=\"" + expectedHrefValue + "\""));
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("orighref=\"" + originalHrefValue + "\""));
    }

    [Test]
    public void Output_contains_meta_generator_element()
    {
      // arrange
      var transcodingInput = new TranscodingInput("test");

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ExtractedContent.Contains("meta name=\"Generator\""));
    }

    [Test]
    public void Transcode_can_extract_title_from_header()
    {
      // arrange
      const string expectedTitle = "Some title ąęłóżźńć";
      const string htmlContent = "<html><head><title>" + expectedTitle + "</title></head><body></body></html>";

      var transcodingInput = new TranscodingInput(htmlContent);

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.TitleExtracted);
      Assert.AreEqual(expectedTitle, transcodingResult.ExtractedTitle);
    }

    [Test]
    public void Transcode_can_extract_title_from_body_h1()
    {
      // arrange
      const string expectedTitle = "Some title ąęłóżźńć";
      const string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      const string htmlContent = "<html><body><div id=\"main\"><h1>" + expectedTitle + "</h1>" + dummyParagraphs + "</div></body></html>";

      var transcodingInput = new TranscodingInput(htmlContent);

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.TitleExtracted);
      Assert.AreEqual(expectedTitle, transcodingResult.ExtractedTitle);
    }

    [Test]
    public void Transcode_can_extract_title_from_body_h2()
    {
      // arrange
      const string expectedTitle = "Some title ąęłóżźńć";
      const string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      const string htmlContent = "<html><body><div id=\"main\"><h2>" + expectedTitle + "</h2>" + dummyParagraphs + "</div></body></html>";

      var transcodingInput = new TranscodingInput(htmlContent);

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.TitleExtracted);
      Assert.AreEqual(expectedTitle, transcodingResult.ExtractedTitle);
    }

    [Test]
    public void Transcode_cleans_up_title_after_extracting_it()
    {
      // arrange
      const string expectedTitle = "Гостиница - Ги де Мопассан";
      const string title = "Гостиница\n-  \r Ги  \t  де \n\n \r Мопассан \r\n";
      const string htmlContent = "<html><head><title>" + title + "</title></head><body></body></html>";

      var transcodingInput = new TranscodingInput(htmlContent);

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.TitleExtracted);
      Assert.AreEqual(expectedTitle, transcodingResult.ExtractedTitle);
    }

    #endregion

    #region Private helper methods

    private static void AssertHtmlContentIsEmpty(string content)
    {
      if (content != null)
      {
        content = content.Trim();
      }

      var document = new SgmlDomBuilder().BuildDocument(content);

      Assert.AreEqual(
        0,
        (from node in document.DescendantNodes()
         let name = node is XElement ? ((XElement)node).Name.LocalName : ""
         where !"html".Equals(name, StringComparison.OrdinalIgnoreCase)
            && !"head".Equals(name, StringComparison.OrdinalIgnoreCase)
            && !"meta".Equals(name, StringComparison.OrdinalIgnoreCase)
         select node).Count());
    }

    private static void AssertHtmlContentsAreEqual(string expectedContent, string actualContent)
    {
      string serializedExpectedContent =
        _sgmlDomSerializer.SerializeDocument(
          _sgmlDomBuilder.BuildDocument(expectedContent));

      string serializedActualContent =
        _sgmlDomSerializer.SerializeDocument(
          _sgmlDomBuilder.BuildDocument(actualContent));

      Assert.AreEqual(serializedExpectedContent, serializedActualContent);
    }

    private static void AssertFloatsAreEqual(float expected, float actual)
    {
      Assert.IsTrue(
        (actual - expected).IsCloseToZero(),
        string.Format(
          CultureInfo.InvariantCulture.NumberFormat,
          "Expected {0:F} but was {1:F}.",
          expected,
          actual));
    }

    private static int CountTags(XContainer container, params string[] args)
    {
      return container.Descendants()
        .Count(
          element =>
          args.Any(
            elementToSearch =>
            elementToSearch.Trim().ToLower()
              .Equals(
                element.Name.LocalName,
                StringComparison.OrdinalIgnoreCase)));
    }

    private static string GetSampleInputUrl(int sampleInputNumber)
    {
      switch (sampleInputNumber)
      {
        case 14:
          return "http://www.theverge.com/2012/5/25/3042640/samsung-galaxy-s-iii-review";

        case 15:
          return "http://www.theverge.com/2012/6/21/3032067/casio-bluetooth-g-shock-watch-gb6900-review";

        default:
          return null;
      }
    }

    private void TestReplacingImageUrl(string srcAttribute, string url, string expectedImageUrl)
    {
      // arrange
      string dummyParagraphs = "<p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p><p>Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet. Lorem ipsum dolor et amet.</p>";
      string htmlContent = "<html><body>" + dummyParagraphs + "<p><img src=\"" + srcAttribute + "\" /></p>" + dummyParagraphs + "</body></html>";

      var transcodingInput =
        new TranscodingInput(htmlContent)
          {
            Url = url,
          };

      // act
      TranscodingResult transcodingResult = _nReadabilityTranscoder.Transcode(transcodingInput);

      // assert
      Assert.IsTrue(transcodingResult.ContentExtracted);

      Assert.IsTrue(
        transcodingResult.ExtractedContent.Contains("src=\"" + expectedImageUrl + "\""),
        string.Format("Image url replacement failed. Src attribute: {0}, base url: {1}, expected image url: {2}", srcAttribute, url, expectedImageUrl));
    }

    #endregion
  }
}
