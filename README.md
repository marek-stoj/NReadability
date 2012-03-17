NReadability
======================

Description
----------------------

NReadability cleans up hard-to-read articles on the Web. It's a tool for
removing clutter from HTML pages so that they are more enjoyable to read.

The NReadability package consists of the .NET class library and a simple
console application.

NReadability is a C# port of [Arc90's Readability bookmarklet][1].

Installation
----------------------

You can start using NReadability right away by installing the NuGet package:

<div style="background-color: #202020; border: 4px solid #C0C0C0; border-radius: 5px 5px 5px 5px; box-shadow: 2px 2px 3px #6E6E6E; color: #E2E2E2; display: block; font: 1.5em/1.5em 'andale mono','lucida console',monospace; overflow: auto; padding: 15px;">
  <p>
    <code>
      PM&gt; Install-Package NReadability
    <code>
  </p>
</div>

Getting Started
----------------------

In order to transcode content downloaded from the Web:

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
