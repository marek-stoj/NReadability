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

using System.IO;
using System.Net;

namespace NReadability
{
  /// <summary>
  /// Fetches web content.
  /// </summary>
  public class UrlFetcher : IUrlFetcher
  {
    #region IUrlFetcher members

    public string Fetch(string url)
    {
      var fetchRequest = (HttpWebRequest)WebRequest.Create(url);      
      
      fetchRequest.Method = "GET";
      
      using (var resp = fetchRequest.GetResponse()) 
      using (var reader = new StreamReader(resp.GetResponseStream(), true))
      {
        return reader.ReadToEnd();
      }
    }

    #endregion
  }
}
