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

namespace NReadability
{
  internal class EncodedStringWriter : StringWriter
  {
    private static readonly Encoding _DefaultEncoding = Encoding.UTF8;

    private readonly Encoding _encoding;

    #region Constructor(s)

    public EncodedStringWriter(StringBuilder sb, Encoding encoding)
      : base(sb)
    {
      if (encoding == null)
      {
        throw new ArgumentNullException("encoding");
      }

      _encoding = encoding;
    }

    public EncodedStringWriter(StringBuilder sb)
      : this(sb, _DefaultEncoding)
    {
    }

    #endregion

    #region Properties

    public override Encoding Encoding
    {
      get { return _encoding; }
    }

    #endregion
  }
}
