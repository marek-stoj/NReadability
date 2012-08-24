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
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace NReadability.Tests
{
  // TODO IMM HI: remove when we get rid of obsolete NReadabilityWebTranscoder.Transcode(...) methods
  [TestFixture]
  public class NReadabilityWebTranscoderTests_Old
  {
    /* This provides the list of URLs for the different test imports */

    private readonly string[][] _Urls =
      {
        new[]
          {
            @"http://www.nytimes.com/2010/11/14/world/asia/14myanmar.html?hp",
            @"http://www.nytimes.com/2010/11/14/world/asia/14myanmar.html?pagewanted=2&hp"
          },
        new[]
          {
            @"http://www.vanityfair.com/politics/features/2010/12/unbroken-excerpt-201012",
            @"http://www.vanityfair.com/politics/features/2010/12/unbroken-excerpt-201012?currentPage=2",
            @"http://www.vanityfair.com/politics/features/2010/12/unbroken-excerpt-201012?currentPage=3"
          },
        new[]
          {
            @"http://www.theatlantic.com/magazine/archive/2010/12/dirty-coal-clean-future/8307",
            @"http://www.theatlantic.com/magazine/archive/2010/12/dirty-coal-clean-future/8307/2",
            @"http://www.theatlantic.com/magazine/archive/2010/12/dirty-coal-clean-future/8307/3"
          },
        new[]
          {
            @"http://www.slate.com/id/2275733",
            @"http://www.slate.com/id/2275733/pagenum/2"
          },
        new[]
          {
            @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble.htm",
            @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble1.htm",
            @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble2.htm",
            @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble3.htm",
            @"http://entertainment.howstuffworks.com/leisure/brain-games/scrabble4.htm",
          },
        new[]
          {
            @"http://www.slate.com/articles/technology/technology/2011/10/steve_jobs_biography_the_new_book_doesn_t_explain_what_made_the_.html",
            @"http://www.slate.com/articles/technology/technology/2011/10/steve_jobs_biography_the_new_book_doesn_t_explain_what_made_the_.2.html",
          },
        new[]
          {
            @"http://www.brookings.edu/opinions/2011/0523_transit_berube_puentes.aspx",
            @"http://www.brookings.edu/opinions/2011/0524_nextwave_west.aspx", // false positive for paging
          },
        new[]
          {
            @"http://mashable.com/2008/10/30/slow-feed-movement-rss",
            @"http://mashable.com/2008/10/30/indecision2008-live-chat", // false positive for paging
          },
      };

    [Test]
    [Sequential]
    public void TestSampleInputs([Values(1, 2, 3, 4, 5, 6, 7, 8)]int sampleInputNumber)
    {
      const string outputDir = "SampleWebOutput";

      string sampleInputNumberStr = sampleInputNumber.ToString().PadLeft(2, '0');
      string[] urls = _Urls[sampleInputNumber - 1];
      string initialUrl = urls[0];

      var fetcher = new FileBasedUrlFetcherStub(sampleInputNumber, urls);
      var _nReadabilityTranscoder = new NReadabilityTranscoder();
      var _nReadabilityWebTranscoder = new NReadabilityWebTranscoder(_nReadabilityTranscoder, fetcher);

      bool mainContentExtracted;

      string transcodedContent =
        _nReadabilityWebTranscoder
          .Transcode(
            initialUrl,
            out mainContentExtracted);

      if (!Directory.Exists(outputDir))
      {
        Directory.CreateDirectory(outputDir);
      }

      File.WriteAllText(
        Path.Combine(outputDir, string.Format("SampleOutput_{0}.html", sampleInputNumberStr)),
        transcodedContent,
        Encoding.UTF8);

      switch (sampleInputNumber)
      {
        case 1:
          Assert.IsTrue(transcodedContent.Contains(" freedom of movement or expression would constitute a new and unacceptable denial"));
          Assert.IsTrue(transcodedContent.Contains("Those expectations were on display in the crowd outside her house on Saturday."));
          Assert.That(Regex.Matches(transcodedContent, "Myanmar Junta Frees Dissident Daw Aung San Suu Kyi").Count, Is.EqualTo(4));
          break;

        case 2:
          Assert.IsTrue(transcodedContent.Contains("For Louie and Phil, the conversations did more than keep their minds sharp."));
          Assert.IsTrue(transcodedContent.Contains("It was absolutely dark and absolutely silent, save for the chattering of Phil’s teeth."));
          Assert.IsTrue(transcodedContent.Contains("A serial runaway and artful dodger"));
          Assert.That(Regex.Matches(transcodedContent, @"Adrift but Unbroken \| Politics").Count, Is.EqualTo(2));
          break;

        case 3:
          Assert.IsTrue(transcodedContent.Contains("The Chinese system as a whole has great weaknesses as well as great strengths."));
          Assert.IsTrue(transcodedContent.Contains(" This emphasis on limits is what begins pointing us back to coal."));
          Assert.IsTrue(transcodedContent.Contains(". For example, the possibility of dramatic rises in ocean levels, which could affect the habitability"));
          Assert.That(Regex.Matches(transcodedContent, "Dirty Coal, Clean Future - Magazine").Count, Is.EqualTo(3)); // Makes sure the title isn't duplicated
          break;

        case 4:  // Test duplicate content on subsequent page
          Assert.That(Regex.Matches(transcodedContent, "his may seem paradoxical, or backward").Count, Is.EqualTo(1));
          break;

        case 5:
          // page 1
          Assert.IsTrue(transcodedContent.Contains("The pressure's on, and as you glance back and forth between your rack and the board, you can hardly believe your eyes at the play you can make."));
          Assert.IsTrue(transcodedContent.Contains("How can you take your game to the next level? Let's start by looking at game play."));
          // page 2
          Assert.IsTrue(transcodedContent.Contains("The object of Scrabble is to get the most points by creating words."));
          Assert.IsTrue(transcodedContent.Contains("Now that you know the parts of the game, let's take a look at how to play it."));
          // page 3
          Assert.IsTrue(transcodedContent.Contains("To determine who goes first, put all the tiles into the bag and mix them up."));
          Assert.IsTrue(transcodedContent.Contains("The game continues until one player uses all of his tiles and there aren't any in the pouch, or if there are no more tiles and no one can make a word. Add up the total of your unplayed tiles and deduct it from your score. If you've used all of your tiles, add the total of the unplayed tiles to your score. The winner has the most points."));
          // page 4
          Assert.IsTrue(transcodedContent.Contains("If you play often enough, you'll need to learn how to play the board in order to get the highest score"));
          Assert.IsTrue(transcodedContent.Contains("With the game's popularity, it now comes in many variations. Let's take a look at some different ways to play Scrabble."));
          // page 5
          Assert.IsTrue(transcodedContent.Contains("Many people play Scrabble on a traditional flat board with the grid imprinted on it."));
          Assert.IsTrue(transcodedContent.Contains("With its worldwide popularity, it only makes sense that Scrabble comes in languages other than English. "));
          break;

        case 6:
          // page 1
          Assert.IsTrue(transcodedContent.Contains("In the aftermath of his resignation and then his death"));
          Assert.IsTrue(transcodedContent.Contains("Curb Your Enthusiasm"));
          // page 2
          Assert.IsTrue(transcodedContent.Contains("Jobs also seemed to suspect that he"));
          Assert.IsTrue(transcodedContent.Contains("And, sadly, it may remain one forever."));
          break;

        case 7:
          // page 1
          Assert.IsTrue(transcodedContent.Contains("post also betrays some misconceptions regarding our report."));
          Assert.IsTrue(transcodedContent.Contains("After all, none of us can resist the occasional study"));
          // "page" 2 (false positive)
          Assert.IsFalse(transcodedContent.Contains("In expressing this view, Clinton joins many Americans who worry about online misinformation, loss of privacy, and identity theft."));
          break;

        case 8:
          // page 1
          Assert.IsTrue(transcodedContent.Contains("For the last couple of days we’ve been asking people"));
          Assert.IsTrue(transcodedContent.Contains("list your favorite tools for slowing down feeds in the comments"));
          // "page" 2 (false positive)
          Assert.IsFalse(transcodedContent.Contains("signature fake news programs"));
          break;

        default:
          throw new NotSupportedException("Unknown sample input number (" + sampleInputNumber + "). Have you added another sample input? If so, then add appropriate asserts here as well.");
      }

      Assert.IsTrue(mainContentExtracted);
    }
  }
}

/*
TODO IMM HI: upnext, next in, next on
http://www.observer.com/2011/manohla-dargis-whoopis-perceived-slight-misses-point?utm_medium=partial-text&utm_campaign=media
*/
