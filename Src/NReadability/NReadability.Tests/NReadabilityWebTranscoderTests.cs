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
  [TestFixture]
  public class NReadabilityWebTranscoderTests
  {
    private NReadabilityTranscoder _nReadabilityTranscoder;
    private NReadabilityWebTranscoder _nReadabilityWebTranscoder;

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
          }
      };

    [SetUp]
    public void SetUp()
    {
      _nReadabilityTranscoder = new NReadabilityTranscoder();      
    }
    
    [Test]
    public void TestSampleInputs([Values(1,2,3,4)]int sampleInputNumber)
    {
      string sampleInputNumberStr = sampleInputNumber.ToString().PadLeft(2, '0');
      string[] urls = _Urls[sampleInputNumber - 1];
      string initialUrl = urls[0];
      IUrlFetcher fetcher = new UrlFetcherStub(sampleInputNumber, urls);
      _nReadabilityWebTranscoder = new NReadabilityWebTranscoder(_nReadabilityTranscoder, fetcher);
      bool mainContentExtracted;
      string transcodedContent = _nReadabilityWebTranscoder.Transcode(initialUrl, out mainContentExtracted);
      const string outputDir = "SampleWebOutput";

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

        default:
          throw new NotSupportedException("Unknown sample input number (" + sampleInputNumber + "). Have you added another sample input? If so, then add appropriate asserts here as well.");
      }

      Assert.IsTrue(mainContentExtracted);
    }
  }
}
