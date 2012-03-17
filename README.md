NReadability
======================

NReadability cleans up hard-to-read articles on the Web. It's a tool for
removing clutter from HTML pages so that they are more enjoyable to read.

The NReadability package consists of the .NET class library and a simple
console application.

NReadability is a C# port of [Arc90's Readability bookmarklet][1].

Usage
----------------------

```c#
var nReadabilityTranscoder = new NReadabilityTranscoder();

string content;

using (var wc = new WebClient())
{
  content =
    wc.DownloadString("https://github.com/marek-stoj/NReadability");
}

bool mainContentExtracted;

string transcodedContent =
  nReadabilityTranscoder.Transcode(
    content,
    out mainContentExtracted);

Console.WriteLine(transcodedContent);
```

[1]: http://lab.arc90.com/experiments/readability/
