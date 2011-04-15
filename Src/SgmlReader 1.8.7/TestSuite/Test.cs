using System;
using System.Xml;
using System.Collections;
using System.IO;
using System.Text;
using System.Net;
using System.Diagnostics;

namespace Sgml
{
	/// <summary>
	/// Summary description for Class1.
	/// </summary>
	class TestSuite
	{
        
        int tests = 0;
        int passed = 0;
        int ignored = 0;

        bool domain = false;
        bool crawl = false;
        bool debug = false;
        bool basify = false;
        bool testdoc = false;
        string proxy = null;
        Encoding encoding = null;
        string output = null;
        bool formatted = false;
        bool noxmldecl = false;
        bool verbose = false;

        /// <summary>
		/// The main entry point for the application.
		/// </summary>
        [STAThread]
        static void Main(string[] args) {
            TestSuite suite = new TestSuite();
            suite.ParseCommandLine(args);
            suite.Run();
        }

        void ParseCommandLine(string[] args) {
            for (int i = 0; i < args.Length; i++){
                string arg = args[i];
                if (arg[0] == '-'){
                    switch (arg.Substring(1)){
                        case "debug":
                            debug = true;
                            break;
                        case "base":
                            basify = true;
                            break;
                        case "crawl":
                            crawl = true;
                            if (args[++i] == "domain")
                                domain = true;
                            break;          
                        case "testdoc":
                            testdoc = true;
                            break;
                        case "verbose":
                            verbose = true;
                            break;
                    }
                }
            }

        }

        void Run(){
            Uri baseUri;
            try {
                baseUri = new Uri(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
            } catch {
                baseUri = new Uri("file://" + Path.Combine(Directory.GetCurrentDirectory(), System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.Replace('\\', '/')));
            }
            Console.WriteLine("baseUri = " + baseUri);
            if(File.Exists("html.suite")) {
                RunTest(baseUri, "html.suite");
            } else {
                RunTest(baseUri, Path.Combine("..", Path.Combine("..", "html.suite")));
            }
            if(File.Exists("ofx.suite")) {
                RunTest(baseUri, "ofx.suite");
            } else {
                RunTest(baseUri, Path.Combine("..", Path.Combine("..", "ofx.suite")));
            }
            RegressionTest1();
            RegressionTest2();
            return;       
        }

        void RunTest(Uri baseUri, string inputUri) {
            Uri resolved = new Uri(baseUri, inputUri);
            string path = resolved.LocalPath;

            this.passed = 0;
            this.tests = 0;
            this.ignored = 0;

            SgmlReader reader = new SgmlReader();
            if(verbose) {
                reader.ErrorLog = Console.Error;
            }
            RunTest(reader, path);   
                        
            Console.WriteLine("{0} Tests passed", this.passed);
            if ((this.passed + this.ignored) != this.tests) {
                Console.WriteLine("{0} Tests failed", this.tests-(this.passed + this.ignored));
            }
            if(this.ignored != 0) {
                Console.WriteLine("{0} Tests ignored", this.ignored);
            }
            Console.WriteLine();
            
            return;
        }

       
        /**************************************************************************
         * Run a test suite.  Tests suites are organized into expected input/output
         * blocks separated by back quotes (`).  It runs the input and compares it
         * with the expected output and reports any failures.
         **************************************************************************/
        void RunTest(SgmlReader reader, string file) {
            Console.WriteLine(file);
            StreamReader sr = new StreamReader(file);
            StringBuilder input = new StringBuilder();
            StringBuilder expectedOutput = new StringBuilder();
            StringBuilder current = null;
            StringBuilder args = new StringBuilder();

            Uri baseUri = new Uri(new Uri(Directory.GetCurrentDirectory()+"\\"), file);
            reader.SetBaseUri(baseUri.AbsoluteUri);
            
            int start = 1;
            int line = 1;
            int pos = 1;
            bool skipToEOL = false;
            bool readArgs = false;
            int i;
            do {
                i = sr.Read();
                char ch = (char)i;
                if (pos == 1 && ch == '`') {
                    ++pos;
                    if (current == null) {
                        current = input;
                        current.Length = 0;
                        readArgs = true;
                    } else if (current == input) {
                        current = expectedOutput;
                    }
                    else {
                        RunTest(reader, start, args.ToString(), input.ToString(), expectedOutput.ToString());
                        start = line;
                        input.Length = 0;
                        args.Length = 0;
                        expectedOutput.Length = 0;
                        current = input;
                        readArgs = true;
                    }
                    skipToEOL = true;
                } else {
                    ++pos;
                    if(current != null) {
                        if (readArgs){
                            args.Append(ch);
                        } else if (!skipToEOL){
                            current.Append(ch);
                        }
                    }
                    if (ch == '\r') {
                        line++; pos = 1;
                        if (sr.Peek() == '\n') {
                            i = sr.Read();
                            if (!skipToEOL) current.Append((char)i);
                            if (readArgs) args.Append(ch);
                        }
                        skipToEOL = false;
                        readArgs = false;
                    } else if (ch == '\n'){
                        skipToEOL = false;
                        readArgs = false;
                        line++; pos = 1;
                    }
                }
            } while (i != -1);

            if (current.Length>0 && expectedOutput.Length>0) {
                RunTest(reader, start, args.ToString(), input.ToString(), expectedOutput.ToString());
            }
        }

        void RunTest(SgmlReader reader, int line, string args, string input, string expectedOutput){
            try {
                bool testdoc = false;
                bool testclone = false;
                bool format = true;
                bool ignore = false;
                foreach(string arg in args.Split(' ')) {
                    string sarg = arg.Trim();
                    if(sarg.Length == 0)
                        continue;
                    if(sarg[0] == '-') {
                        switch(sarg.Substring(1)) {
                        case "html":
                            reader.DocType = "html";
                            break;
                        case "lower":
                            reader.CaseFolding = CaseFolding.ToLower;
                            break;
                        case "upper":
                            reader.CaseFolding = CaseFolding.ToUpper;
                            break;
                        case "testdoc":
                            testdoc = true;
                            break;
                        case "testclone":
                            testclone = true;
                            break;
                        case "noformat":
                            format = false;
                            break;
                        case "ignore":
                            ignore = true;
                            break;
                        }
                    }
                }
                this.tests++;
                if(ignore) {
                    this.ignored++;
                    return;
                }
                reader.InputStream = new StringReader(input);
                if(format) {
                    reader.WhitespaceHandling = WhitespaceHandling.None;
                } else {
                    reader.WhitespaceHandling = WhitespaceHandling.All;
                }
                StringWriter output = new StringWriter();
                XmlTextWriter w = new XmlTextWriter(output);
                if(format) {
                    w.Formatting = Formatting.Indented;
                }
                if(testdoc) {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(reader);
                    doc.WriteTo(w);
                } else if(testclone) {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(reader);
                    XmlNode clone = doc.Clone();
                    clone.WriteTo(w);
                } else {
                    reader.Read();
                    while(!reader.EOF) {
                        w.WriteNode(reader, true);
                    }
                }
                w.Close();
                string actualOutput = output.ToString();

                // validate output
                using(StringReader source = new StringReader(actualOutput)) {
                    XmlDocument doc = new XmlDocument();
                    doc.Load(source);
                }

                // compare output
                if(actualOutput.Trim().Replace("\r", "") != expectedOutput.Trim().Replace("\r", "")) {
                    Console.WriteLine();
                    Console.WriteLine("ERROR: Test failed on line {0}", line);
                    Console.WriteLine("---- Expected output");
                    Console.Write(expectedOutput);
                    Console.WriteLine("---- Actual output");
                    Console.WriteLine(actualOutput);
                } else {
                    this.passed++;
                }
            } catch(Exception e) {
                Console.WriteLine("FATAL ERROR: Test threw an exception on line {0}", line);
                Console.WriteLine(e.Message);
                Console.WriteLine(e.ToString());
                Console.WriteLine("---- Input");
                Console.Write(input);
            }
        }

        void Process(SgmlReader reader, string uri, bool loadAsStream) {   
            if (uri == null) {
                reader.InputStream = Console.In;
            } 
            else if (loadAsStream) {
                Uri location = new Uri(uri);
                if (location.IsFile) {   
                    reader.InputStream = new StreamReader(uri);
                } else {
                    WebRequest wr = WebRequest.Create(location);
                    reader.InputStream = new StreamReader(wr.GetResponse().GetResponseStream());
                }
            } else {
                reader.Href = uri;
            }

            if (debug) {
                Debug(reader);
                reader.Close();
                return;
            } 
            if (crawl) {
                StartCrawl(reader, uri, basify);
                return;
            } 

            if (this.encoding == null) {
                this.encoding = reader.GetEncoding();
            }

            
            XmlTextWriter w = null;
            if (output != null) {
                w = new XmlTextWriter(output, this.encoding);          
            } 
            else {
                w = new XmlTextWriter(Console.Out);
            }
            if (formatted) w.Formatting = Formatting.Indented;
            if (!noxmldecl) {
                w.WriteStartDocument();
            }
            if (testdoc) {
                XmlDocument doc = new XmlDocument();
                try {
                    doc.Load(reader);
                    doc.WriteTo(w);
                } catch (XmlException e) {
                    Console.WriteLine("Error:" + e.Message);
                    Console.WriteLine("at line " + e.LineNumber + " column " + e.LinePosition);
                }
            } else {
                reader.Read();
                while (!reader.EOF) {
                    w.WriteNode(reader, true);
                }
            }
            w.Flush();
            w.Close();          
        }


        
        /***************************************************************************
        * Useful debugging code...
        * **************************************************************************/
        void StartCrawl(SgmlReader reader, string uri, bool basify) {      
            Console.WriteLine("Loading '"+reader.BaseURI+"'");

            XmlDocument doc = new XmlDocument();
            try {         
                doc.XmlResolver = null; // don't do any downloads!
                doc.Load(reader);
            } 
            catch (Exception e) {
                Console.WriteLine("Error loading document\n"+e.Message);
            }       
            reader.Close();

            if (basify) {
                // html and head are option, if they are there use them otherwise not.
                XmlElement be = (XmlElement)doc.SelectSingleNode("//base");
                if (be == null) {
                    be = doc.CreateElement("base");
                    be.SetAttribute("href", doc.BaseURI);

                    XmlElement head = (XmlElement)doc.SelectSingleNode("//head");
                    if (head != null) {
                        head.InsertBefore(be, head.FirstChild);
                    }
                    else {
                        XmlElement html = (XmlElement)doc.SelectSingleNode("//html");
                        if (html != null) html.InsertBefore(be, html.FirstChild);
                        else doc.DocumentElement.InsertBefore(be, doc.DocumentElement.FirstChild);
                    }
                }
            }

            try {
                Crawl(reader.Dtd, doc, reader.ErrorLog);
            } 
            catch (Exception e) {
                Console.WriteLine("Uncaught exception: " + e.Message);
            }
        }


        enum NodeTypeFlags {
            None = 0,
            Element = 0x1,
            Attribute = 0x2,
            Text = 0x4,
            CDATA = 0x8,
            EntityReference = 0x10,
            Entity = 0x20,
            ProcessingInstruction = 0x40,
            Comment = 0x80,
            Document = 0x100,
            DocumentType = 0x200,
            DocumentFragment = 0x400,
            Notation = 0x800,
            Whitespace = 0x1000,
            SignificantWhitespace = 0x2000,
            EndElement = 0x4000,
            EndEntity = 0x8000,
            filler = 0x10000,
            XmlDeclaration = 0x20000,
        }

        NodeTypeFlags[] NodeTypeMap = new NodeTypeFlags[19] {
                                                                NodeTypeFlags.None,
                                                                NodeTypeFlags.Element,
                                                                NodeTypeFlags.Attribute,
                                                                NodeTypeFlags.Text,
                                                                NodeTypeFlags.CDATA,
                                                                NodeTypeFlags.EntityReference,
                                                                NodeTypeFlags.Entity,
                                                                NodeTypeFlags.ProcessingInstruction,
                                                                NodeTypeFlags.Comment,
                                                                NodeTypeFlags.Document,
                                                                NodeTypeFlags.DocumentType,
                                                                NodeTypeFlags.DocumentFragment,
                                                                NodeTypeFlags.Notation,
                                                                NodeTypeFlags.Whitespace,
                                                                NodeTypeFlags.SignificantWhitespace,
                                                                NodeTypeFlags.EndElement,
                                                                NodeTypeFlags.EndEntity,
                                                                NodeTypeFlags.filler,
                                                                NodeTypeFlags.XmlDeclaration,
        };


        void Debug(SgmlReader sr) {
            NodeTypeFlags[] AllowedContentMap = new NodeTypeFlags[19] {
                                                                          NodeTypeFlags.None, // none
                                                                          NodeTypeFlags.Element | NodeTypeFlags.Attribute | NodeTypeFlags.Text | NodeTypeFlags.CDATA | NodeTypeFlags.EntityReference | NodeTypeFlags.ProcessingInstruction | NodeTypeFlags.Comment | NodeTypeFlags.Whitespace | NodeTypeFlags.SignificantWhitespace | NodeTypeFlags.EndElement, // element
                                                                          NodeTypeFlags.Text | NodeTypeFlags.EntityReference, // attribute
                                                                          NodeTypeFlags.None, // text
                                                                          NodeTypeFlags.None, // cdata
                                                                          NodeTypeFlags.None, // entity reference
                                                                          NodeTypeFlags.None, // entity
                                                                          NodeTypeFlags.None, // processing instruction
                                                                          NodeTypeFlags.None, // comment
                                                                          NodeTypeFlags.Comment | NodeTypeFlags.DocumentType | NodeTypeFlags.Element | NodeTypeFlags.EndElement | NodeTypeFlags.ProcessingInstruction | NodeTypeFlags.Whitespace | NodeTypeFlags.SignificantWhitespace | NodeTypeFlags.XmlDeclaration, // document
                                                                          NodeTypeFlags.None, // document type
                                                                          NodeTypeFlags.None, // document fragment (not expecting these)
                                                                          NodeTypeFlags.None, // notation
                                                                          NodeTypeFlags.None, // whitespace
                                                                          NodeTypeFlags.None, // signification whitespace
                                                                          NodeTypeFlags.None, // end element
                                                                          NodeTypeFlags.None, // end entity
                                                                          NodeTypeFlags.None, // filler
                                                                          NodeTypeFlags.None, // xml declaration.
            };

            Stack s = new Stack();

            while (sr.Read()) {
                if (sr.NodeType == XmlNodeType.EndElement) {
                    s.Pop();
                }
                if (s.Count > 0) {
                    XmlNodeType pt = (XmlNodeType)s.Peek();
                    NodeTypeFlags p = NodeTypeMap[(int)pt];
                    NodeTypeFlags f = NodeTypeMap[(int)sr.NodeType];
                    if ((AllowedContentMap[(int)pt]& f) != f) {
                        Console.WriteLine("Invalid content!!");
                    }
                }
                if (s.Count != sr.Depth-1) {
                    Console.WriteLine("Depth is wrong!");
                }
                if ( (sr.NodeType == XmlNodeType.Element && !sr.IsEmptyElement) ||
                    sr.NodeType == XmlNodeType.Document) {
                    s.Push(sr.NodeType);
                }

                for (int i = 1; i < sr.Depth; i++) 
                    Console.Write("  ");
                Console.Write(sr.NodeType.ToString() + " " + sr.Name);
                if (sr.NodeType == XmlNodeType.Element && sr.AttributeCount > 0) {
                    sr.MoveToAttribute(0);
                    Console.Write(" (" + sr.Name+"="+sr.Value + ")");
                    sr.MoveToElement();
                }       
                if (sr.Value != null) {
                    Console.Write(" " + sr.Value.Replace("\n"," ").Replace("\r",""));
                }
                Console.WriteLine();
            }
        }

        int depth = 0;
        int count = 0;
        Hashtable visited = new Hashtable();

        bool Crawl(SgmlDtd dtd, XmlDocument doc, TextWriter log) {
            depth++;
            StringBuilder indent = new StringBuilder();
            for (int i = 0; i < depth; i++)
                indent.Append(" ");
      
            count++;
            Uri baseUri = new Uri(doc.BaseURI);
            XmlElement baseElmt = (XmlElement)doc.SelectSingleNode("/html/head/base");
            if (baseElmt != null) {
                string href = baseElmt.GetAttribute("href");
                if (href != "") {
                    try {
                        baseUri = new Uri(href);
                    }
                    catch (Exception ) {
                        Console.WriteLine("### Error parsing BASE href '"+href+"'");
                    }
                }
            }
            foreach (XmlElement a in doc.SelectNodes("//a")) {
                string href = a.GetAttribute("href");
                if (href != "" && href != null && depth<5) {
                    Uri local = new Uri(baseUri, href);
                    if (domain && baseUri.Host != local.Host)
                        continue;
                    string ext = Path.GetExtension(local.AbsolutePath).ToLower();
                    if (ext == ".jpg" || ext == ".gif" || ext==".mpg")
                        continue;
                    string url = local.AbsoluteUri;
                    if (!visited.ContainsKey(url)) {
                        visited.Add(url, url);
                        log.WriteLine(indent+"Loading '"+url+"'");
                        log.Flush();
                        StreamReader stm = null;
                        try {
                            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
                            wr.Timeout = 10000; 
                            if (proxy != null) wr.Proxy = new WebProxy(proxy);
                            wr.PreAuthenticate = false; 
                            // Pass the credentials of the process. 
                            wr.Credentials = CredentialCache.DefaultCredentials; 

                            WebResponse resp = wr.GetResponse();
                            Uri actual = resp.ResponseUri;
                            if (actual.AbsoluteUri != url) {
                                local = new Uri(actual.AbsoluteUri);
                                log.WriteLine(indent+"Redirected to '"+actual.AbsoluteUri+"'");
                                log.Flush();
                            }           
                            if (resp.ContentType != "text/html") {
                                log.WriteLine(indent+"Skipping ContentType="+resp.ContentType);
                                log.Flush();
                                resp.Close();
                            } 
                            else {
                                stm = new StreamReader(resp.GetResponseStream());
                            }
                        } 
                        catch (Exception e) {
                            log.WriteLine(indent+"### Error opening URL: " + e.Message);
                            log.Flush();
                        }
                        if (stm != null) {
                            SgmlReader reader = new SgmlReader();
                            reader.Dtd = dtd;
                            reader.SetBaseUri(local.AbsoluteUri);
                            reader.InputStream = stm;
                            reader.WebProxy = proxy;

                            XmlDocument d2 = new XmlDocument();
                            d2.XmlResolver = null; // don't do any downloads!
                            try {
                                d2.Load(reader);
                                reader.Close();
                                stm.Close();
                                if (!Crawl(dtd, d2, log))
                                    return false;
                            } 
                            catch (Exception e) {
                                log.WriteLine(indent+"### Error parsing document '"+local.AbsoluteUri+"', "+e.Message);
                                log.Flush();
                                reader.Close();
                            }
                        }
                    }
                }
            }
            depth--;
            return true;
        }

        void RegressionTest1() {
            // Make sure we can do MoveToElement after reading multiple attributes.
            SgmlReader r = new SgmlReader();
            r.InputStream = new StringReader("<test id='10' x='20'><a/><!--comment-->test</test>");
            if (r.Read()) {
                while (r.MoveToNextAttribute()) {
                    Trace.WriteLine(r.Name);
                }
                if (r.MoveToElement()) {
                    Trace.WriteLine(r.ReadInnerXml());
                }
            }
        }

        void RegressionTest2() {

            // test setup
            var source = "&test";
            var reader = new SgmlReader();
            reader.DocType = "HTML";
            reader.WhitespaceHandling = WhitespaceHandling.All;
            reader.StripDocType = true;
            reader.InputStream = new StringReader(source);
            reader.CaseFolding = CaseFolding.ToLower;

            // test
            var element = System.Xml.Linq.XElement.Load(reader);
            string value = element.Value;
            if(!string.IsNullOrEmpty(value) && value[value.Length - 1] == (char)65535) {
                Console.Error.WriteLine("ERROR: sgml reader added 65535 to input end");
            }
        }
	}
}
