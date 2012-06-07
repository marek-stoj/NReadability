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

namespace NReadability
{
  /// <summary>
  /// Determines how the extracted article will be styled.
  /// </summary>
  public enum ReadingStyle
  {
    /// <summary>
    /// Newspaper style.
    /// </summary>
    Newspaper,

    /// <summary>
    /// Novel style.
    /// </summary>
    Novel,

    /// <summary>
    /// Ebook style.
    /// </summary>
    Ebook,

    /// <summary>
    /// Terminal style.
    /// </summary>
    Terminal,
  }

  /// <summary>
  /// Determines how wide the margin of the extracted article will be.
  /// </summary>
  public enum ReadingMargin
  {
    /// <summary>
    /// Extra-narrow margin.
    /// </summary>
    XNarrow,

    /// <summary>
    /// Narrow margin.
    /// </summary>
    Narrow,

    /// <summary>
    /// Medium margin.
    /// </summary>
    Medium,

    /// <summary>
    /// Wide margin.
    /// </summary>
    Wide,

    /// <summary>
    /// Extra-wide margin.
    /// </summary>
    XWide,
  }

  /// <summary>
  /// Determines how large the font of the extracted article will be.
  /// </summary>
  public enum ReadingSize
  {
    /// <summary>
    /// Extra-small font.
    /// </summary>
    XSmall,

    /// <summary>
    /// Small font.
    /// </summary>
    Small,

    /// <summary>
    /// Medium font.
    /// </summary>
    Medium,

    /// <summary>
    /// Large font.
    /// </summary>
    Large,

    /// <summary>
    /// Extra-large font.
    /// </summary>
    XLarge,
  }
}
