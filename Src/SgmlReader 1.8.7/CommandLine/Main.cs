using System;
using System.Xml;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Collections;

namespace Sgml {
    /// <summary>
    /// This class provides a command line interface to the SgmlReader.
    /// </summary>
    public class CommandLine {

        string proxy = null;
        string output = null;
        bool formatted = false;
        bool noxmldecl = false;
        Encoding encoding = null;

        [STAThread]
        static void Main(string[] args) {
            try {
                CommandLine t = new CommandLine();
                t.Run(args);
            } catch (Exception e) {
                Console.WriteLine("Error: " + e.Message);
            }
            return;
        }

        public void Run(string[] args) {
            SgmlReader reader = new SgmlReader();
            string inputUri = null;

            for (int i = 0; i < args.Length; i++) {
                string arg = args[i];
                if (arg[0] == '-' || arg[0] == '/') {
                    switch (arg.Substring(1)) {
                        case "e":
                            string errorlog = args[++i];
                            if (errorlog.ToLower() == "$stderr") {
                                reader.ErrorLog = Console.Error;
                            } 
                            else {
                                reader.ErrorLogFile = errorlog;
                            }
                            break;
                        case "html":
                            reader.DocType = "HTML";
                            break;
                        case "dtd":
                            reader.SystemLiteral = args[++i];
                            break;
                        case "proxy":
                            proxy = args[++i];
                            reader.WebProxy = proxy;
                            break;
                        case "encoding":
                            encoding = Encoding.GetEncoding(args[++i]);
                            break;
                        case "f":
                            formatted = true;
                            reader.WhitespaceHandling = WhitespaceHandling.None;
                            break;
                        case "noxml":
                            noxmldecl = true;
                            break;
                        case "doctype":
                            reader.StripDocType = false;
                            break;
                        case "lower":
                            reader.CaseFolding = CaseFolding.ToLower;
                            break;
                        case "upper":
                            reader.CaseFolding = CaseFolding.ToUpper;
                            break;

                        default:
                            Console.WriteLine("Usage: SgmlReader <options> [InputUri] [OutputFile]");
                            Console.WriteLine("-e log         Optional log file name, name of '$STDERR' will write errors to stderr");
                            Console.WriteLine("-f             Whether to pretty print the output.");
                            Console.WriteLine("-html          Specify the built in HTML dtd");
                            Console.WriteLine("-dtd url       Specify other SGML dtd to use");
                            Console.WriteLine("-base          Add base tag to output HTML");
                            Console.WriteLine("-noxml         Do not add XML declaration to the output");
                            Console.WriteLine("-proxy svr:80  Proxy server to use for http requests");
                            Console.WriteLine("-encoding name Specify an encoding for the output file (default UTF-8)");
                            Console.WriteLine("-lower         Convert input tags to lower case");
                            Console.WriteLine("-upper         Convert input tags to upper case");
                            Console.WriteLine();
                            Console.WriteLine("InputUri       The input file or http URL (default stdin).  ");
                            Console.WriteLine("               Supports wildcards for local file names.");
                            Console.WriteLine("OutputFile     Output file name (default stdout)");
                            Console.WriteLine("               If input file contains wildcards then this just specifies the output file extension (default .xml)");
                            return;
                    }
                } 
                else {
                    if (inputUri == null) {
                        inputUri = arg;
                        string ext = Path.GetExtension(arg).ToLower();
                        if (ext == ".htm" || ext == ".html")
                            reader.DocType = "HTML";
                    }
                    else if (output == null) output = arg;
                }
            }
            if (inputUri != null && !inputUri.StartsWith("http://") && inputUri.IndexOfAny(new char[] { '*', '?' }) >= 0) {
                // wild card processing of a directory of files.
                string path = Path.GetDirectoryName(inputUri);
                if (path == "") path = ".\\";
                string ext = ".xml";
                if (output != null) 
                    ext = Path.GetExtension(output);
                foreach (string uri in Directory.GetFiles(path, Path.GetFileName(inputUri))) {
                    Console.WriteLine("Processing: " + uri);
                    string file = Path.GetFileName(uri);
                    output = Path.GetDirectoryName(uri) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(file) + ext;
                    Process(reader, uri);
                    reader.Close();
                }        
                return;
            } 
            Process(reader, inputUri);
            reader.Close();
           
            return ;
        }

        void Process(SgmlReader reader, string uri) {   
            if (uri == null) {
                reader.InputStream = Console.In;
            } else {
                reader.Href = uri;
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
            reader.Read();
            while (!reader.EOF) {
                w.WriteNode(reader, true);
            }
            w.Flush();
            w.Close();          
        }



    }    
}
