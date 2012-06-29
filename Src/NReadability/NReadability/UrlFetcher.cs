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
using System.Text;
using System;

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
            {
                return DecodeData(resp);
            }
        }

        #endregion

        /// <remarks>
        /// http://blogs.msdn.com/b/feroze_daud/archive/2004/03/30/104440.aspx
        /// </remarks>
        private static string DecodeData(WebResponse w)
        {
            //
            // first see if content length header has charset = calue
            //
            string charset = null;
            string ctype = w.Headers["content-type"];
            if (ctype != null)
            {
                int ind = ctype.IndexOf("charset=");
                if (ind != -1)
                {
                    charset = ctype.Substring(ind + 8);
                }
            }

            // save data to a memorystream
            MemoryStream rawdata = new MemoryStream();
            byte[] buffer = new byte[1024];
            using (Stream rs = w.GetResponseStream())
            {
                int read = rs.Read(buffer, 0, buffer.Length);
                while (read > 0)
                {
                    rawdata.Write(buffer, 0, read);
                    read = rs.Read(buffer, 0, buffer.Length);
                }
            }

            //
            // if ContentType is null, or did not contain charset, we search in body
            //
            if (charset == null)
            {
                MemoryStream ms = rawdata;
                ms.Seek(0, SeekOrigin.Begin);

                StreamReader srr = new StreamReader(ms, Encoding.ASCII);
                string meta = srr.ReadToEnd();

                if (meta != null)
                {
                    int start_ind = meta.IndexOf("charset=");
                    int end_ind = -1;
                    if (start_ind != -1)
                    {
                        end_ind = meta.IndexOf("\"", start_ind);
                        if (end_ind != -1)
                        {
                            int start = start_ind + 8;
                            charset = meta.Substring(start, end_ind - start + 1);
                            charset = charset.TrimEnd(new Char[] { '>', '"' });
                        }
                    }
                }
            }

            Encoding e = null;
            if (charset == null)
            {
                e = Encoding.ASCII; //default encoding
            }
            else
            {
                try
                {
                    e = Encoding.GetEncoding(charset);
                }
                catch (Exception)
                {
                    e = Encoding.ASCII;
                }
            }

            rawdata.Seek(0, SeekOrigin.Begin);
            using (var sr = new StreamReader(rawdata, e))
            {
                return sr.ReadToEnd();
            }
        }
    }
}
