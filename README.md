NReadability
======================

Description
----------------------

NReadability cleans up hard-to-read articles on the Web. It's a tool for
removing clutter from HTML pages so that they are more enjoyable to read.

The NReadability package consists of the .NET class library and a simple
console application.

NReadability is a C# port of [Arc90's Readability bookmarklet][1].

Usage
----------------------

Transcode content downloaded from the Web:

```c#
var transcoder = new NReadabilityTranscoder();
string content;

using (var wc = new WebClient())
{
  content = wc.DownloadString("https://github.com/marek-stoj/NReadability");
}

bool success;

string transcodedContent =
  transcoder.Transcode(content, out success);
```

Or even simpler:

```c#
var transcoder = new NReadabilityWebTranscoder();
bool success;

string transcodedContent =
  transcoder.Transcode("https://github.com/marek-stoj/NReadability", out success);
```

[1]: http://lab.arc90.com/experiments/readability/
