/*
* 
* An XmlReader implementation for loading SGML (including HTML) converting it
* to well formed XML, by adding missing quotes, empty attribute values, ignoring
* duplicate attributes, case folding on tag names, adding missing closing tags
* based on SGML DTD information, and so on.
*
* Copyright (c) 2002 Microsoft Corporation. All rights reserved. (Chris Lovett)
* 
* Copyright (c) 2007-2008 MindTouch. All rights reserved.
* 
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;

namespace Sgml
{
    /// <summary>
    /// SGML is case insensitive, so here you can choose between converting
    /// to lower case or upper case tags.  "None" means that the case is left
    /// alone, except that end tags will be folded to match the start tags.
    /// </summary>
    public enum CaseFolding
    {
        /// <summary>
        /// Do not convert case, except for converting end tags to match start tags.
        /// </summary>
        None,

        /// <summary>
        /// Convert tags to upper case.
        /// </summary>
        ToUpper,

        /// <summary>
        /// Convert tags to lower case.
        /// </summary>
        ToLower
    }

    /// <summary>
    /// This stack maintains a high water mark for allocated objects so the client
    /// can reuse the objects in the stack to reduce memory allocations, this is
    /// used to maintain current state of the parser for element stack, and attributes
    /// in each element.
    /// </summary>
    internal class HWStack
    {
        private object[] m_items;
        private int m_size;
        private int m_count;
        private int m_growth;

        /// <summary>
        /// Initialises a new instance of the HWStack class.
        /// </summary>
        /// <param name="growth">The amount to grow the stack space by, if more space is needed on the stack.</param>
        public HWStack(int growth)
        {
            this.m_growth = growth;
        }

        /// <summary>
        /// The number of items currently in the stack.
        /// </summary>
        public int Count
        {
            get
            {
                return this.m_count;
            }
            set
            {
                this.m_count = value;
            }
        }

        /// <summary>
        /// The size (capacity) of the stack.
        /// </summary>
        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Kept for potential future usage.")]
        public int Size
        {
            get
            {
                return this.m_size;
            }
        }

        /// <summary>
        /// Returns the item at the requested index or null if index is out of bounds
        /// </summary>
        /// <param name="i">The index of the item to retrieve.</param>
        /// <returns>The item at the requested index or null if index is out of bounds.</returns>
        public object this[int i]
        {
            get
            {
                return (i >= 0 && i < this.m_size) ? m_items[i] : null;
            }
            set
            {
                this.m_items[i] = value;
            }
        }

        /// <summary>
        /// Removes and returns the item at the top of the stack
        /// </summary>
        /// <returns>The item at the top of the stack.</returns>
        public object Pop()
        {
            this.m_count--;
            if (this.m_count > 0)
            {
                return m_items[this.m_count - 1];
            }

            return null;
        }

        /// <summary>
        /// Pushes a new slot at the top of the stack.
        /// </summary>
        /// <returns>The object at the top of the stack.</returns>
        /// <remarks>
        /// This method tries to reuse a slot, if it returns null then
        /// the user has to call the other Push method.
        /// </remarks>
        public object Push()
        {
            if (this.m_count == this.m_size)
            {
                int newsize = this.m_size + this.m_growth;
                object[] newarray = new object[newsize];
                if (this.m_items != null)
                    Array.Copy(this.m_items, newarray, this.m_size);

                this.m_size = newsize;
                this.m_items = newarray;
            }
            return m_items[this.m_count++];
        }

        /// <summary>
        /// Remove a specific item from the stack.
        /// </summary>
        /// <param name="i">The index of the item to remove.</param>
        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Kept for potential future usage.")]
        public void RemoveAt(int i)
        {
            this.m_items[i] = null;
            Array.Copy(this.m_items, i + 1, this.m_items, i, this.m_count - i - 1);
            this.m_count--;
        }
    }

    /// <summary>
    /// This class represents an attribute.  The AttDef is assigned
    /// from a validation process, and is used to provide default values.
    /// </summary>
    internal class Attribute
    {
        internal string Name;    // the atomized name.
        internal AttDef DtdType; // the AttDef of the attribute from the SGML DTD.
        internal char QuoteChar; // the quote character used for the attribute value.
        private string m_literalValue; // the attribute value

        /// <summary>
        /// Attribute objects are reused during parsing to reduce memory allocations, 
        /// hence the Reset method.
        /// </summary>
        public void Reset(string name, string value, char quote)
        {
            this.Name = name;
            this.m_literalValue = value;
            this.QuoteChar = quote;
            this.DtdType = null;
        }

        public string Value
        {
            get
            {
                if (this.m_literalValue != null) 
                    return this.m_literalValue;
                if (this.DtdType != null) 
                    return this.DtdType.Default;
                return null;
            }
/*            set
            {
                this.m_literalValue = value;
            }*/
        }

        public bool IsDefault
        {
            get
            {
                return (this.m_literalValue == null);
            }
        }
    }    

    /// <summary>
    /// This class models an XML node, an array of elements in scope is maintained while parsing
    /// for validation purposes, and these Node objects are reused to reduce object allocation,
    /// hence the reset method.  
    /// </summary>
    internal class Node
    {
        internal XmlNodeType NodeType;
        internal string Value;
        internal XmlSpace Space;
        internal string XmlLang;
        internal bool IsEmpty;        
        internal string Name;
        internal ElementDecl DtdType; // the DTD type found via validation
        internal State CurrentState;
        internal bool Simulated; // tag was injected into result stream.
        HWStack attributes = new HWStack(10);

        /// <summary>
        /// Attribute objects are reused during parsing to reduce memory allocations, 
        /// hence the Reset method. 
        /// </summary>
        public void Reset(string name, XmlNodeType nt, string value) {           
            this.Value = value;
            this.Name = name;
            this.NodeType = nt;
            this.Space = XmlSpace.None;
            this.XmlLang= null;
            this.IsEmpty = true;
            this.attributes.Count = 0;
            this.DtdType = null;
        }

        public Attribute AddAttribute(string name, string value, char quotechar, bool caseInsensitive) {
            Attribute a;
            // check for duplicates!
            for (int i = 0, n = this.attributes.Count; i < n; i++) {
                a = (Attribute)this.attributes[i];
                if (string.Equals(a.Name, name, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    return null;
                }
            }
            // This code makes use of the high water mark for attribute objects,
            // and reuses exisint Attribute objects to avoid memory allocation.
            a = (Attribute)this.attributes.Push();
            if (a == null) {
                a = new Attribute();
                this.attributes[this.attributes.Count-1] = a;
            }
            a.Reset(name, value, quotechar);
            return a;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811", Justification = "Kept for potential future usage.")]
        public void RemoveAttribute(string name)
        {
            for (int i = 0, n = this.attributes.Count; i < n; i++)
            {
                Attribute a  = (Attribute)this.attributes[i];
                if (string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    this.attributes.RemoveAt(i);
                    return;
                }
            }
        }
        public void CopyAttributes(Node n) {
            for (int i = 0, len = n.attributes.Count; i < len; i++) {
                Attribute a = (Attribute)n.attributes[i];
                Attribute na = this.AddAttribute(a.Name, a.Value, a.QuoteChar, false);
                na.DtdType = a.DtdType;
            }
        }

        public int AttributeCount {
            get {
                return this.attributes.Count;
            }
        }

        public int GetAttribute(string name) {
            for (int i = 0, n = this.attributes.Count; i < n; i++) {
                Attribute a = (Attribute)this.attributes[i];
                if (string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) {
                    return i;
                }
            }
            return -1;
        }

        public Attribute GetAttribute(int i) {
            if (i>=0 && i<this.attributes.Count) {
                Attribute a = (Attribute)this.attributes[i];
                return a;
            }
            return null;
        }
    }

    internal enum State
    {
        Initial,    // The initial state (Read has not been called yet)
        Markup,     // Expecting text or markup
        EndTag,     // Positioned on an end tag
        Attr,       // Positioned on an attribute
        AttrValue,  // Positioned in an attribute value
        Text,       // Positioned on a Text node.
        PartialTag, // Positioned on a text node, and we have hit a start tag
        AutoClose,  // We are auto-closing tags (this is like State.EndTag), but end tag was generated
        CData,      // We are on a CDATA type node, eg. <scipt> where we have special parsing rules.
        PartialText,
        PseudoStartTag, // we pushed a pseudo-start tag, need to continue with previous start tag.
        Eof
    }


    /// <summary>
    /// SgmlReader is an XmlReader API over any SGML document (including built in 
    /// support for HTML).  
    /// </summary>
    public class SgmlReader : XmlReader
    {
        /// <summary>
        /// The value returned when a namespace is queried and none has been specified.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1705", Justification = "SgmlReader's standards for constants are different to Microsoft's and in line with older C++ style constants naming conventions.  Visually, constants using this style are more easily identifiable as such.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1707", Justification = "SgmlReader's standards for constants are different to Microsoft's and in line with older C++ style constants naming conventions.  Visually, constants using this style are more easily identifiable as such.")]
        public const string UNDEFINED_NAMESPACE = "#unknown";

        private SgmlDtd m_dtd;
        private Entity m_current;
        private State m_state;
        private char m_partial;
        private string m_endTag;
        private HWStack m_stack;
        private Node m_node; // current node (except for attributes)
        // Attributes are handled separately using these members.
        private Attribute m_a;
        private int m_apos; // which attribute are we positioned on in the collection.
        private Uri m_baseUri;
        private StringBuilder m_sb;
        private StringBuilder m_name;
        private TextWriter m_log;
        private bool m_foundRoot;
        private bool m_ignoreDtd;

        // autoclose support
        private Node m_newnode;
        private int m_poptodepth;
        private int m_rootCount;
        private bool m_isHtml;
        private string m_rootElementName;

        private string m_href;
        private string m_errorLogFile;
        private Entity m_lastError;
        private string m_proxy;
        private TextReader m_inputStream;
        private string m_syslit;
        private string m_pubid;
        private string m_subset;
        private string m_docType;
        private WhitespaceHandling m_whitespaceHandling;
        private CaseFolding m_folding = CaseFolding.None;
        private bool m_stripDocType = true;
        //private string m_startTag;
        private Dictionary<string, string> unknownNamespaces = new Dictionary<string,string>();

        /// <summary>
        /// Initialises a new instance of the SgmlReader class.
        /// </summary>
        public SgmlReader() {
            Init();
        }

        /// <summary>
        /// Initialises a new instance of the SgmlReader class with an existing <see cref="XmlNameTable"/>, which is NOT used.
        /// </summary>
        /// <param name="nt">The nametable to use.</param>
        public SgmlReader(XmlNameTable nt) {
            Init();
        }

        /// <summary>
        /// Specify the SgmlDtd object directly.  This allows you to cache the Dtd and share
        /// it across multipl SgmlReaders.  To load a DTD from a URL use the SystemLiteral property.
        /// </summary>
        public SgmlDtd Dtd
        {
            get
            {
                if (this.m_dtd == null)
                {
                    LazyLoadDtd(this.m_baseUri);
                }

                return this.m_dtd; 
            }
            set
            {
                this.m_dtd = value;
            }
        }

        private void LazyLoadDtd(Uri baseUri)
        {
            if (this.m_dtd == null && !this.m_ignoreDtd)
            {
                if (string.IsNullOrEmpty(this.m_syslit))
                {
                    if (this.m_docType != null && StringUtilities.EqualsIgnoreCase(this.m_docType, "html"))
                    {
                        Assembly a = typeof(SgmlReader).Assembly;
                        string name = a.FullName.Split(',')[0]+".Html.dtd";
                        Stream stm = a.GetManifestResourceStream(name);
                        if (stm != null)
                        {
                            StreamReader sr = new StreamReader(stm);
                            this.m_dtd = SgmlDtd.Parse(baseUri, "HTML", sr, null, this.m_proxy, null);
                        }
                    }
                }
                else
                { 
                    if (baseUri != null)
                    {
                        baseUri = new Uri(baseUri, this.m_syslit);
                    }
                    else if (this.m_baseUri != null)
                    {
                        baseUri = new Uri(this.m_baseUri, this.m_syslit);
                    }
                    else
                    {
                        baseUri = new Uri(new Uri(Directory.GetCurrentDirectory() + "\\"), this.m_syslit);
                    }
                    this.m_dtd = SgmlDtd.Parse(baseUri, this.m_docType, this.m_pubid, baseUri.AbsoluteUri, this.m_subset, this.m_proxy, null);
                }
            }

            if (this.m_dtd != null && this.m_dtd.Name != null)
            {
                switch(this.CaseFolding)
                {
                case CaseFolding.ToUpper:
                    this.m_rootElementName = this.m_dtd.Name.ToUpperInvariant();
                    break;
                case CaseFolding.ToLower:
                    this.m_rootElementName = this.m_dtd.Name.ToLowerInvariant();
                    break;
                default:
                    this.m_rootElementName = this.m_dtd.Name;
                    break;
                }

                this.m_isHtml = StringUtilities.EqualsIgnoreCase(this.m_dtd.Name, "html");
            }
        }

        /// <summary>
        /// The name of root element specified in the DOCTYPE tag.
        /// </summary>
        public string DocType
        {
            get
            {
                return this.m_docType;
            }
            set
            {
                this.m_docType = value;
            }
        }

        /// <summary>
        /// The root element of the document.
        /// </summary>
        public string RootElementName
        {
            get
            {
                return m_rootElementName;
            }
        }

        /// <summary>
        /// The PUBLIC identifier in the DOCTYPE tag
        /// </summary>
        public string PublicIdentifier
        {
            get
            {
                return this.m_pubid;
            }
            set
            {
                this.m_pubid = value;
            }
        }

        /// <summary>
        /// The SYSTEM literal in the DOCTYPE tag identifying the location of the DTD.
        /// </summary>
        public string SystemLiteral
        {
            get
            {
                return this.m_syslit;
            }
            set
            {
                this.m_syslit = value;
            }
        }

        /// <summary>
        /// The DTD internal subset in the DOCTYPE tag
        /// </summary>
        public string InternalSubset
        {
            get
            {
                return this.m_subset;
            }
            set
            {
                this.m_subset = value;
            }
        }

        /// <summary>
        /// The input stream containing SGML data to parse.
        /// You must specify this property or the Href property before calling Read().
        /// </summary>
        public TextReader InputStream
        {
            get
            {
                return this.m_inputStream;
            }
            set
            {
                this.m_inputStream = value;
                Init();
            }
        }

        /// <summary>
        /// Sometimes you need to specify a proxy server in order to load data via HTTP
        /// from outside the firewall.  For example: "itgproxy:80".
        /// </summary>
        public string WebProxy
        {
            get
            {
                return this.m_proxy;
            }
            set
            {
                this.m_proxy = value;
            }
        }

        /// <summary>
        /// The base Uri is used to resolve relative Uri's like the SystemLiteral and
        /// Href properties.  This is a method because BaseURI is a read-only
        /// property on the base XmlReader class.
        /// </summary>
        public void SetBaseUri(string uri)
        {
            this.m_baseUri = new Uri(uri);
        }

        /// <summary>
        /// Specify the location of the input SGML document as a URL.
        /// </summary>
        public string Href
        {
            get
            {
                return this.m_href;
            }
            set
            {
                this.m_href = value; 
                Init();
                if (this.m_baseUri == null)
                {
                    if (this.m_href.IndexOf("://") > 0)
                    {
                        this.m_baseUri = new Uri(this.m_href);
                    }
                    else
                    {
                        this.m_baseUri = new Uri("file:///" + Directory.GetCurrentDirectory() + "//");
                    }
                }
            }
        }

        /// <summary>
        /// Whether to strip out the DOCTYPE tag from the output (default true)
        /// </summary>
        public bool StripDocType
        {
            get
            {
                return this.m_stripDocType;
            }
            set
            {
                this.m_stripDocType = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to ignore any DTD reference.
        /// </summary>
        /// <value><c>true</c> if DTD references should be ignored; otherwise, <c>false</c>.</value>
        public bool IgnoreDtd
        {
            get { return m_ignoreDtd; }
            set { m_ignoreDtd = value; }
        }

        /// <summary>
        /// The case conversion behaviour while processing tags.
        /// </summary>
        public CaseFolding CaseFolding
        {
            get
            {
                return this.m_folding;
            }
            set
            {
                this.m_folding = value;
            }
        }

        /// <summary>
        /// DTD validation errors are written to this stream.
        /// </summary>
        public TextWriter ErrorLog
        {
            get
            {
                return this.m_log;
            }
            set
            {
                this.m_log = value;
            }
        }

        /// <summary>
        /// DTD validation errors are written to this log file.
        /// </summary>
        public string ErrorLogFile
        {
            get
            {
                return this.m_errorLogFile;
            }
            set
            {
                this.m_errorLogFile = value;
                this.m_log = new StreamWriter(value);
            }
        }

        private void Log(string msg, params string[] args)
        {
            if (ErrorLog != null)
            {
                string err = string.Format(CultureInfo.CurrentUICulture, msg, args);
                if (this.m_lastError != this.m_current)
                {
                    err = err + "    " + this.m_current.Context();
                    this.m_lastError = this.m_current;
                    ErrorLog.WriteLine("### Error:" + err);
                }
                else
                {
                    string path = "";
                    if (this.m_current.ResolvedUri != null)
                    {
                        path = this.m_current.ResolvedUri.AbsolutePath;
                    }

                    ErrorLog.WriteLine("### Error in {0}#{1}, line {2}, position {3}: {4}", path, this.m_current.Name, this.m_current.Line, this.m_current.LinePosition, err);
                }
            }
        }

        private void Log(string msg, char ch)
        {
            Log(msg, ch.ToString());
        }

        private void Init()
        {
            this.m_state = State.Initial;
            this.m_stack = new HWStack(10);
            this.m_node = Push(null, XmlNodeType.Document, null);
            this.m_node.IsEmpty = false;
            this.m_sb = new StringBuilder();
            this.m_name = new StringBuilder();
            this.m_poptodepth = 0;
            this.m_current = null;
            this.m_partial = '\0';
            this.m_endTag = null;
            this.m_a = null;
            this.m_apos = 0;
            this.m_newnode = null;
            this.m_rootCount = 0;
            this.m_foundRoot = false;
            this.unknownNamespaces.Clear();
        }

        private Node Push(string name, XmlNodeType nt, string value)
        {
            Node result = (Node)this.m_stack.Push();
            if (result == null)
            {
                result = new Node();
                this.m_stack[this.m_stack.Count - 1] = result;
            }

            result.Reset(name, nt, value);
            this.m_node = result;
            return result;
        }

        private void SwapTopNodes()
        {
            int top = this.m_stack.Count - 1;
            if (top > 0)
            {
                Node n = (Node)this.m_stack[top - 1];
                this.m_stack[top - 1] = this.m_stack[top];
                this.m_stack[top] = n;
            }
        }

        private Node Push(Node n)
        {
            // we have to do a deep clone of the Node object because
            // it is reused in the stack.
            Node n2 = Push(n.Name, n.NodeType, n.Value);
            n2.DtdType = n.DtdType;
            n2.IsEmpty = n.IsEmpty;
            n2.Space = n.Space;
            n2.XmlLang = n.XmlLang;
            n2.CurrentState = n.CurrentState;
            n2.CopyAttributes(n);
            this.m_node = n2;
            return n2;
        }

        private void Pop()
        {
            if (this.m_stack.Count > 1)
            {
                this.m_node = (Node)this.m_stack.Pop();
            }
        }

        private Node Top()
        {
            int top = this.m_stack.Count - 1;
            if (top > 0)
            {
                return (Node)this.m_stack[top];
            }

            return null;
        }

        /// <summary>
        /// The node type of the node currently being parsed.
        /// </summary>
        public override XmlNodeType NodeType
        {
            get
            {
                if (this.m_state == State.Attr)
                {
                    return XmlNodeType.Attribute;
                }
                else if (this.m_state == State.AttrValue)
                {
                    return XmlNodeType.Text;
                }
                else if (this.m_state == State.EndTag || this.m_state == State.AutoClose)
                {
                    return XmlNodeType.EndElement;
                }

                return this.m_node.NodeType;
            }
        }

        /// <summary>
        /// The name of the current node, if currently positioned on a node or attribute.
        /// </summary>
        public override string Name
        {
            get
            {
                string result = null;
                if (this.m_state == State.Attr)
                {
                    result = XmlConvert.EncodeName(this.m_a.Name);
                }
                else if (this.m_state != State.AttrValue)
                {
                    result = this.m_node.Name;
                }

                return result;
            }
        }

        /// <summary>
        /// The local name of the current node, if currently positioned on a node or attribute.
        /// </summary>
        public override string LocalName
        {
            get
            {
                string result = Name;
                if (result != null)
                {
                    int colon = result.IndexOf(':');
                    if (colon != -1)
                    {
                        result = result.Substring(colon + 1);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// The namespace of the current node, if currently positioned on a node or attribute.
        /// </summary>
        /// <remarks>
        /// If not positioned on a node or attribute, <see cref="UNDEFINED_NAMESPACE"/> is returned.
        /// </remarks>
        [SuppressMessage("Microsoft.Performance", "CA1820", Justification="Cannot use IsNullOrEmpty in a switch statement and swapping the elegance of switch for a load of 'if's is not worth it.")]
        public override string NamespaceURI
        {
            get
            {
                // SGML has no namespaces, unless this turned out to be an xmlns attribute.
                if (this.m_state == State.Attr && string.Equals(this.m_a.Name, "xmlns", StringComparison.OrdinalIgnoreCase))
                {
                    return "http://www.w3.org/2000/xmlns/";
                }

                string prefix = Prefix;
                switch (Prefix)
                {
                case "xmlns":
                    return "http://www.w3.org/2000/xmlns/";
                case "xml":
                    return "http://www.w3.org/XML/1998/namespace";
                case null: // Should never occur since Prefix never returns null
                case "":
                    if (NodeType == XmlNodeType.Attribute)
                    {
                        // attributes without a prefix are never in any namespace
                        return string.Empty;
                    }
                    else if (NodeType == XmlNodeType.Element)
                    {
                        // check if a 'xmlns:prefix' attribute is defined
                        for (int i = this.m_stack.Count - 1; i > 0; --i)
                        {
                            Node node = this.m_stack[i] as Node;
                            if ((node != null) && (node.NodeType == XmlNodeType.Element))
                            {
                                int index = node.GetAttribute("xmlns");
                                if (index >= 0)
                                {
                                    string value = node.GetAttribute(index).Value;
                                    if (value != null)
                                    {
                                        return value;
                                    }
                                }
                            }
                        }
                    }

                    return string.Empty;
                default: {
                        string value;
                        if((NodeType == XmlNodeType.Attribute) || (NodeType == XmlNodeType.Element)) {

                            // check if a 'xmlns:prefix' attribute is defined
                            string key = "xmlns:" + prefix;
                            for(int i = this.m_stack.Count - 1; i > 0; --i) {
                                Node node = this.m_stack[i] as Node;
                                if((node != null) && (node.NodeType == XmlNodeType.Element)) {
                                    int index = node.GetAttribute(key);
                                    if(index >= 0) {
                                        value = node.GetAttribute(index).Value;
                                        if(value != null) {
                                            return value;
                                        }
                                    }
                                }
                            }
                        }

                        // check if we've seen this prefix before
                        if(!unknownNamespaces.TryGetValue(prefix, out value)) {
                            if(unknownNamespaces.Count > 0) {
                                value = UNDEFINED_NAMESPACE + unknownNamespaces.Count.ToString();
                            } else {
                                value = UNDEFINED_NAMESPACE;
                            }
                            unknownNamespaces[prefix] = value;
                        }
                        return value;
                    }
                }
            }
        }

        /// <summary>
        /// The prefix of the current node's name.
        /// </summary>
        public override string Prefix
        { 
            get
            {
                string result = Name;
                if (result != null)
                {
                    int colon = result.IndexOf(':');
                    if(colon != -1) {
                        result = result.Substring(0, colon);
                    } else {
                        result = string.Empty;
                    }
                }
                return result ?? string.Empty;
            }
        }

        /// <summary>
        /// Whether the current node has a value or not.
        /// </summary>
        public override bool HasValue
        { 
            get
            {
                if (this.m_state == State.Attr || this.m_state == State.AttrValue)
                {
                    return true;
                }

                return (this.m_node.Value != null);
            }
        }

        /// <summary>
        /// The value of the current node.
        /// </summary>
        public override string Value
        {
            get
            {
                if (this.m_state == State.Attr || this.m_state == State.AttrValue)
                {
                    return this.m_a.Value;
                }

                return this.m_node.Value;
            }
        }

        /// <summary>
        /// Gets the depth of the current node in the XML document.
        /// </summary>
        /// <value>The depth of the current node in the XML document.</value>
        public override int Depth
        { 
            get
            {
                if (this.m_state == State.Attr)
                {
                    return this.m_stack.Count;
                }
                else if (this.m_state == State.AttrValue)
                {
                    return this.m_stack.Count + 1;
                }

                return this.m_stack.Count - 1;
            }
        }

        /// <summary>
        /// Gets the base URI of the current node.
        /// </summary>
        /// <value>The base URI of the current node.</value>
        public override string BaseURI
        {
            get
            {
                return this.m_baseUri == null ? "" : this.m_baseUri.AbsoluteUri;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current node is an empty element (for example, &lt;MyElement/&gt;).
        /// </summary>
        public override bool IsEmptyElement
        {
            get
            {
                if (this.m_state == State.Markup || this.m_state == State.Attr || this.m_state == State.AttrValue)
                {
                    return this.m_node.IsEmpty;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current node is an attribute that was generated from the default value defined in the DTD or schema.
        /// </summary>
        /// <value>
        /// true if the current node is an attribute whose value was generated from the default value defined in the DTD or
        /// schema; false if the attribute value was explicitly set.
        /// </value>
        public override bool IsDefault
        {
            get
            {
                if (this.m_state == State.Attr || this.m_state == State.AttrValue)
                    return this.m_a.IsDefault;

                return false;
            }
        }

        /// <summary>
        /// Gets the quotation mark character used to enclose the value of an attribute node.
        /// </summary>
        /// <value>The quotation mark character (" or ') used to enclose the value of an attribute node.</value>
        /// <remarks>
        /// This property applies only to an attribute node.
        /// </remarks>
        public override char QuoteChar
        {
            get
            {
                if (this.m_a != null)
                    return this.m_a.QuoteChar;

                return '\0';
            }
        }

        /// <summary>
        /// Gets the current xml:space scope.
        /// </summary>
        /// <value>One of the <see cref="XmlSpace"/> values. If no xml:space scope exists, this property defaults to XmlSpace.None.</value>
        public override XmlSpace XmlSpace
        {
            get
            {
                for (int i = this.m_stack.Count - 1; i > 1; i--)
                {
                    Node n = (Node)this.m_stack[i];
                    XmlSpace xs = n.Space;
                    if (xs != XmlSpace.None)
                        return xs;
                }

                return XmlSpace.None;
            }
        }

        /// <summary>
        /// Gets the current xml:lang scope.
        /// </summary>
        /// <value>The current xml:lang scope.</value>
        public override string XmlLang
        {
            get
            {
                for (int i = this.m_stack.Count - 1; i > 1; i--)
                {
                    Node n = (Node)this.m_stack[i];
                    string xmllang = n.XmlLang;
                    if (xmllang != null)
                        return xmllang;
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Specifies how white space is handled.
        /// </summary>
        public WhitespaceHandling WhitespaceHandling
        {
            get
            {
                return this.m_whitespaceHandling;
            } 
            set
            {
                this.m_whitespaceHandling = value;
            }
        }

        /// <summary>
        /// Gets the number of attributes on the current node.
        /// </summary>
        /// <value>The number of attributes on the current node.</value>
        public override int AttributeCount
        {
            get
            {
                if (this.m_state == State.Attr || this.m_state == State.AttrValue) 
                    return 0;
                else if (this.m_node.NodeType == XmlNodeType.Element || this.m_node.NodeType == XmlNodeType.DocumentType)
                    return this.m_node.AttributeCount;
                else
                    return 0;
            }
        }

        /// <summary>
        /// Gets the value of an attribute with the specified <see cref="Name"/>.
        /// </summary>
        /// <param name="name">The name of the attribute to retrieve.</param>
        /// <returns>The value of the specified attribute. If the attribute is not found, a null reference (Nothing in Visual Basic) is returned. </returns>
        public override string GetAttribute(string name)
        {
            if (this.m_state != State.Attr && this.m_state != State.AttrValue)
            {
                int i = this.m_node.GetAttribute(name);
                if (i >= 0)
                    return GetAttribute(i);
            }

            return null;
        }

        /// <summary>
        /// Gets the value of the attribute with the specified <see cref="LocalName"/> and <see cref="NamespaceURI"/>.
        /// </summary>
        /// <param name="name">The local name of the attribute.</param>
        /// <param name="namespaceURI">The namespace URI of the attribute.</param>
        /// <returns>The value of the specified attribute. If the attribute is not found, a null reference (Nothing in Visual Basic) is returned. This method does not move the reader.</returns>
        public override string GetAttribute(string name, string namespaceURI)
        {
            return GetAttribute(name); // SGML has no namespaces.
        }

        /// <summary>
        /// Gets the value of the attribute with the specified index.
        /// </summary>
        /// <param name="i">The index of the attribute.</param>
        /// <returns>The value of the specified attribute. This method does not move the reader.</returns>
        public override string GetAttribute(int i)
        {
            if (this.m_state != State.Attr && this.m_state != State.AttrValue)
            {
                Attribute a = this.m_node.GetAttribute(i);
                if (a != null)
                    return a.Value;
            }

            throw new ArgumentOutOfRangeException("i");
        }

        /// <summary>
        /// Gets the value of the attribute with the specified index.
        /// </summary>
        /// <param name="i">The index of the attribute.</param>
        /// <returns>The value of the specified attribute. This method does not move the reader.</returns>
        public override string this[int i]
        {
            get
            {
                return GetAttribute(i);
            }
        }

        /// <summary>
        /// Gets the value of an attribute with the specified <see cref="Name"/>.
        /// </summary>
        /// <param name="name">The name of the attribute to retrieve.</param>
        /// <returns>The value of the specified attribute. If the attribute is not found, a null reference (Nothing in Visual Basic) is returned. </returns>
        public override string this[string name]
        { 
            get
            {
                return GetAttribute(name);
            }
        }

        /// <summary>
        /// Gets the value of the attribute with the specified <see cref="LocalName"/> and <see cref="NamespaceURI"/>.
        /// </summary>
        /// <param name="name">The local name of the attribute.</param>
        /// <param name="namespaceURI">The namespace URI of the attribute.</param>
        /// <returns>The value of the specified attribute. If the attribute is not found, a null reference (Nothing in Visual Basic) is returned. This method does not move the reader.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1023", Justification = "This design is that of Microsoft's XmlReader class and overriding its method is merely continuing the same design.")]
        public override string this[string name, string namespaceURI]
        { 
            get
            {
                return GetAttribute(name, namespaceURI);
            }
        }

        /// <summary>
        /// Moves to the atttribute with the specified <see cref="Name"/>.
        /// </summary>
        /// <param name="name">The qualified name of the attribute.</param>
        /// <returns>true if the attribute is found; otherwise, false. If false, the reader's position does not change.</returns>
        public override bool MoveToAttribute(string name)
        {
            int i = this.m_node.GetAttribute(name);
            if (i >= 0)
            {
                MoveToAttribute(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Moves to the attribute with the specified <see cref="LocalName"/> and <see cref="NamespaceURI"/>.
        /// </summary>
        /// <param name="name">The local name of the attribute.</param>
        /// <param name="ns">The namespace URI of the attribute.</param>
        /// <returns>true if the attribute is found; otherwise, false. If false, the reader's position does not change.</returns>
        public override bool MoveToAttribute(string name, string ns)
        {
            return MoveToAttribute(name);
        }

        /// <summary>
        /// Moves to the attribute with the specified index.
        /// </summary>
        /// <param name="i">The index of the attribute to move to.</param>
        public override void MoveToAttribute(int i)
        {
            Attribute a = this.m_node.GetAttribute(i);
            if (a != null)
            {
                this.m_apos = i;
                this.m_a = a;
                if (this.m_state != State.Attr)
                {
                    this.m_node.CurrentState = this.m_state; //save current state.
                }

                this.m_state = State.Attr;
                return;
            }

            throw new ArgumentOutOfRangeException("i");
        }

        /// <summary>
        /// Moves to the first attribute.
        /// </summary>
        /// <returns></returns>
        public override bool MoveToFirstAttribute()
        {
            if (this.m_node.AttributeCount > 0)
            {
                MoveToAttribute(0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Moves to the next attribute.
        /// </summary>
        /// <returns>true if there is a next attribute; false if there are no more attributes.</returns>
        /// <remarks>
        /// If the current node is an element node, this method is equivalent to <see cref="MoveToFirstAttribute"/>. If <see cref="MoveToNextAttribute"/> returns true,
        /// the reader moves to the next attribute; otherwise, the position of the reader does not change.
        /// </remarks>
        public override bool MoveToNextAttribute()
        {
            if (this.m_state != State.Attr && this.m_state != State.AttrValue)
            {
                return MoveToFirstAttribute();
            }
            else if (this.m_apos < this.m_node.AttributeCount - 1)
            {
                MoveToAttribute(this.m_apos + 1);
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Moves to the element that contains the current attribute node.
        /// </summary>
        /// <returns>
        /// true if the reader is positioned on an attribute (the reader moves to the element that owns the attribute); false if the reader is not positioned
        /// on an attribute (the position of the reader does not change).
        /// </returns>
        public override bool MoveToElement()
        {
            if (this.m_state == State.Attr || this.m_state == State.AttrValue)
            {
                this.m_state = this.m_node.CurrentState;
                this.m_a = null;
                return true;
            }
            else
                return (this.m_node.NodeType == XmlNodeType.Element);
        }

        /// <summary>
        /// Gets whether the content is HTML or not.
        /// </summary>
        public bool IsHtml
        {
            get
            {
                return this.m_isHtml;
            }
        }

        /// <summary>
        /// Returns the encoding of the current entity.
        /// </summary>
        /// <returns>The encoding of the current entity.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024", Justification = "This method to get the encoding does not simply read a value, but potentially causes significant processing of the input stream.")]
        public Encoding GetEncoding()
        {
            if (this.m_current == null)
            {
                OpenInput();
            }

            return this.m_current.Encoding;
        }

        private void OpenInput()
        {
            LazyLoadDtd(this.m_baseUri);

            if (this.Href != null)
            {
                this.m_current = new Entity("#document", null, this.m_href, this.m_proxy);
            }
            else if (this.m_inputStream != null)
            {
                this.m_current = new Entity("#document", null, this.m_inputStream, this.m_proxy);           
            }
            else
            {
                throw new InvalidOperationException("You must specify input either via Href or InputStream properties");
            }

            this.m_current.IsHtml = this.IsHtml;
            this.m_current.Open(null, this.m_baseUri);
            if (this.m_current.ResolvedUri != null)
                this.m_baseUri = this.m_current.ResolvedUri;

            if (this.m_current.IsHtml && this.m_dtd == null)
            {
                this.m_docType = "HTML";
                LazyLoadDtd(this.m_baseUri);
            }
        }

        /// <summary>
        /// Reads the next node from the stream.
        /// </summary>
        /// <returns>true if the next node was read successfully; false if there are no more nodes to read.</returns>
        public override bool Read()
        {
            if (m_current == null)
            {
                OpenInput();
            }

            State start = this.m_state;
            if (m_node.Simulated)
            {
                // return the next node
                m_node.Simulated = false;
                this.m_node = Top();
                this.m_state = this.m_node.CurrentState;
                return true;
            }

            bool foundnode = false;
            while (!foundnode)
            {
                switch (this.m_state)
                {
                    case State.Initial:
                        this.m_state = State.Markup;
                        this.m_current.ReadChar();
                        goto case State.Markup;
                    case State.Eof:
                        if (this.m_current.Parent != null)
                        {
                            this.m_current.Close();
                            this.m_current = this.m_current.Parent;
                        }
                        else
                        {                           
                            return false;
                        }
                        break;
                    case State.EndTag:
                        if (string.Equals(this.m_endTag, this.m_node.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            Pop(); // we're done!
                            this.m_state = State.Markup;
                            goto case State.Markup;
                        }                     
                        Pop(); // close one element
                        foundnode = true;// return another end element.
                        break;
                    case State.Markup:
                        if (this.m_node.IsEmpty)
                        {
                            Pop();
                        }
                        Node n = this.m_node;
                        foundnode = ParseMarkup();
                        break;
                    case State.PartialTag:
                        Pop(); // remove text node.
                        this.m_state = State.Markup;
                        foundnode = ParseTag(this.m_partial);
                        break;
                    case State.PseudoStartTag:
                        foundnode = ParseStartTag('<');                        
                        break;
                    case State.AutoClose:
                        Pop(); // close next node.
                        if (this.m_stack.Count <= this.m_poptodepth)
                        {
                            this.m_state = State.Markup;
                            if (this.m_newnode != null)
                            {
                                Push(this.m_newnode); // now we're ready to start the new node.
                                this.m_newnode = null;
                                this.m_state = State.Markup;
                            }
                            else if (this.m_node.NodeType == XmlNodeType.Document)
                            {
                                this.m_state = State.Eof;
                                goto case State.Eof;
                            }
                        } 
                        foundnode = true;
                        break;
                    case State.CData:
                        foundnode = ParseCData();
                        break;
                    case State.Attr:
                        goto case State.AttrValue;
                    case State.AttrValue:
                        this.m_state = State.Markup;
                        goto case State.Markup;
                    case State.Text:
                        Pop();
                        goto case State.Markup;
                    case State.PartialText:
                        if (ParseText(this.m_current.Lastchar, false))
                        {
                            this.m_node.NodeType = XmlNodeType.Whitespace;
                        }

                        foundnode = true;
                        break;
                }

                if (foundnode && this.m_node.NodeType == XmlNodeType.Whitespace && this.m_whitespaceHandling == WhitespaceHandling.None)
                {
                    // strip out whitespace (caller is probably pretty printing the XML).
                    foundnode = false;
                }
                if (!foundnode && this.m_state == State.Eof && this.m_stack.Count > 1)
                {
                    this.m_poptodepth = 1;
                    this.m_state = State.AutoClose;
                    this.m_node = Top();
                    return true;
                }
            }
            if (!m_foundRoot && (this.NodeType == XmlNodeType.Element ||
                    this.NodeType == XmlNodeType.Text ||
                    this.NodeType == XmlNodeType.CDATA))
            {
                m_foundRoot = true;
                if (this.IsHtml && (this.NodeType != XmlNodeType.Element ||
                    !string.Equals(this.LocalName, "html", StringComparison.OrdinalIgnoreCase)))
                {
                    // Simulate an HTML root element!
                    this.m_node.CurrentState = this.m_state;
                    Node root = Push("html", XmlNodeType.Element, null);
                    SwapTopNodes(); // make html the outer element.
                    this.m_node = root;
                    root.Simulated = true;
                    root.IsEmpty = false;
                    this.m_state = State.Markup;
                    //this.state = State.PseudoStartTag;
                    //this.startTag = name;
                }

                return true;
            }

            return true;
        }

        private bool ParseMarkup()
        {
            char ch = this.m_current.Lastchar;
            if (ch == '<')
            {
                ch = this.m_current.ReadChar();
                return ParseTag(ch);
            } 
            else if (ch != Entity.EOF)
            {
                if (this.m_node.DtdType != null && this.m_node.DtdType.ContentModel.DeclaredContent == DeclaredContent.CDATA)
                {
                    // e.g. SCRIPT or STYLE tags which contain unparsed character data.
                    this.m_partial = '\0';
                    this.m_state = State.CData;
                    return false;
                }
                else if (ParseText(ch, true))
                {
                    this.m_node.NodeType = XmlNodeType.Whitespace;
                }

                return true;
            }

            this.m_state = State.Eof;
            return false;
        }

        private const string declterm = " \t\r\n><";
        private bool ParseTag(char ch)
        {
            if (ch == '%')
            {
                return ParseAspNet();
            }
            else if (ch == '!')
            {
                ch = this.m_current.ReadChar();
                if (ch == '-')
                {
                    return ParseComment();
                }
                else if (ch == '[')
                {
                    return ParseConditionalBlock();
                }
                else if (ch != '_' && !char.IsLetter(ch))
                {
                    // perhaps it's one of those nasty office document hacks like '<![if ! ie ]>'
                    string value = this.m_current.ScanToEnd(this.m_sb, "Recovering", ">"); // skip it
                    Log("Ignoring invalid markup '<!"+value+">");
                    return false;
                }
                else
                {
                    string name = this.m_current.ScanToken(this.m_sb, SgmlReader.declterm, false);
                    if (string.Equals(name, "DOCTYPE", StringComparison.OrdinalIgnoreCase))
                    {
                        ParseDocType();

                        // In SGML DOCTYPE SYSTEM attribute is optional, but in XML it is required,
                        // therefore if there is no SYSTEM literal then add an empty one.
                        if (this.GetAttribute("SYSTEM") == null && this.GetAttribute("PUBLIC") != null)
                        {
                            this.m_node.AddAttribute("SYSTEM", "", '"', this.m_folding == CaseFolding.None);
                        }

                        if (m_stripDocType)
                        {
                            return false;
                        }
                        else
                        {
                            this.m_node.NodeType = XmlNodeType.DocumentType;
                            return true;
                        }
                    }
                    else
                    {
                        Log("Invalid declaration '<!{0}...'.  Expecting '<!DOCTYPE' only.", name);
                        this.m_current.ScanToEnd(null, "Recovering", ">"); // skip it
                        return false;
                    }
                }
            } 
            else if (ch == '?')
            {
                this.m_current.ReadChar();// consume the '?' character.
                return ParsePI();
            }
            else if (ch == '/')
            {
                return ParseEndTag();
            }
            else
            {
                return ParseStartTag(ch);
            }
        }

        private string ScanName(string terminators)
        {
            string name = this.m_current.ScanToken(this.m_sb, terminators, false);
            switch (this.m_folding)
            {
                case CaseFolding.ToUpper:
                    name = name.ToUpperInvariant();
                    break;
                case CaseFolding.ToLower:
                    name = name.ToLowerInvariant();
                    break;
            }
            return name;
        }

        private static bool VerifyName(string name)
        {
            try
            {
                XmlConvert.VerifyName(name);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private const string tagterm = " \t\r\n=/><";
        private const string aterm = " \t\r\n='\"/>";
        private const string avterm = " \t\r\n>";
        private bool ParseStartTag(char ch)
        {
            string name = null;
            if (m_state != State.PseudoStartTag)
            {
                if (SgmlReader.tagterm.IndexOf(ch) >= 0)
                {
                    this.m_sb.Length = 0;
                    this.m_sb.Append('<');
                    this.m_state = State.PartialText;
                    return false;
                }

                name = ScanName(SgmlReader.tagterm);
            }
            else
            {
                // TODO: Changes by mindtouch mean that  this.startTag is never non-null.  The effects of this need checking.

                //name = this.startTag;
                m_state = State.Markup;
            }

            Node n = Push(name, XmlNodeType.Element, null);
            n.IsEmpty = false;
            Validate(n);
            ch = this.m_current.SkipWhitespace();
            while (ch != Entity.EOF && ch != '>')
            {
                if (ch == '/')
                {
                    n.IsEmpty = true;
                    ch = this.m_current.ReadChar();
                    if (ch != '>')
                    {
                        Log("Expected empty start tag '/>' sequence instead of '{0}'", ch);
                        this.m_current.ScanToEnd(null, "Recovering", ">");
                        return false;
                    }
                    break;
                } 
                else if (ch == '<')
                {
                    Log("Start tag '{0}' is missing '>'", name);
                    break;
                }

                string aname = ScanName(SgmlReader.aterm);
                ch = this.m_current.SkipWhitespace();
                if (string.Equals(aname, ",", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(aname, "=", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(aname, ":", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(aname, ";", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = null;
                char quote = '\0';
                if (ch == '=' || ch == '"' || ch == '\'')
                {
                    if (ch == '=' )
                    {
                        this.m_current.ReadChar();
                        ch = this.m_current.SkipWhitespace();
                    }

                    if (ch == '\'' || ch == '\"')
                    {
                        quote = ch;
                        value = ScanLiteral(this.m_sb, ch);
                    }
                    else if (ch != '>')
                    {
                        string term = SgmlReader.avterm;
                        value = this.m_current.ScanToken(this.m_sb, term, false);
                    }
                }

                if (ValidAttributeName(aname))
                {
                    Attribute a = n.AddAttribute(aname, value ?? aname, quote, this.m_folding == CaseFolding.None);
                    if (a == null)
                    {
                        Log("Duplicate attribute '{0}' ignored", aname);
                    }
                    else
                    {
                        ValidateAttribute(n, a);
                    }
                }

                ch = this.m_current.SkipWhitespace();
            }

            if (ch == Entity.EOF)
            {
                this.m_current.Error("Unexpected EOF parsing start tag '{0}'", name);
            } 
            else if (ch == '>')
            {
                this.m_current.ReadChar(); // consume '>'
            }

            if (this.Depth == 1)
            {
                if (this.m_rootCount == 1)
                {
                    // Hmmm, we found another root level tag, soooo, the only
                    // thing we can do to keep this a valid XML document is stop
                    this.m_state = State.Eof;
                    return false;
                }
                this.m_rootCount++;
            }

            ValidateContent(n);
            return true;
        }

        private bool ParseEndTag()
        {
            this.m_state = State.EndTag;
            this.m_current.ReadChar(); // consume '/' char.
            string name = this.ScanName(SgmlReader.tagterm);
            char ch = this.m_current.SkipWhitespace();
            if (ch != '>')
            {
                Log("Expected empty start tag '/>' sequence instead of '{0}'", ch);
                this.m_current.ScanToEnd(null, "Recovering", ">");
            }

            this.m_current.ReadChar(); // consume '>'

            this.m_endTag = name;

            // Make sure there's a matching start tag for it.                        
            bool caseInsensitive = (this.m_folding == CaseFolding.None);
            this.m_node = (Node)this.m_stack[this.m_stack.Count - 1];
            for (int i = this.m_stack.Count - 1; i > 0; i--)
            {
                Node n = (Node)this.m_stack[i];
                if (string.Equals(n.Name, name, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                {
                    this.m_endTag = n.Name;
                    return true;
                }
            }

            Log("No matching start tag for '</{0}>'", name);
            this.m_state = State.Markup;
            return false;
        }

        private bool ParseAspNet()
        {
            string value = "<%" + this.m_current.ScanToEnd(this.m_sb, "AspNet", "%>") + "%>";
            Push(null, XmlNodeType.CDATA, value);         
            return true;
        }

        private bool ParseComment()
        {
            char ch = this.m_current.ReadChar();
            if (ch != '-')
            {
                Log("Expecting comment '<!--' but found {0}", ch);
                this.m_current.ScanToEnd(null, "Comment", ">");
                return false;
            }

            string value = this.m_current.ScanToEnd(this.m_sb, "Comment", "-->");
            
            // Make sure it's a valid comment!
            int i = value.IndexOf("--");

            while (i >= 0)
            {
                int j = i + 2;
                while (j < value.Length && value[j] == '-')
                    j++;

                if (i > 0)
                {
                    value = value.Substring(0, i - 1) + "-" + value.Substring(j);
                } 
                else
                {
                    value = "-" + value.Substring(j);
                }

                i = value.IndexOf("--");
            }

            if (value.Length > 0 && value[value.Length - 1] == '-')
            {
                value += " "; // '-' cannot be last character
            }

            Push(null, XmlNodeType.Comment, value);         
            return true;
        }

        private const string cdataterm = "\t\r\n[]<>";
        private bool ParseConditionalBlock()
        {
            char ch = m_current.ReadChar(); // skip '['
            ch = m_current.SkipWhitespace();
            string name = m_current.ScanToken(m_sb, cdataterm, false);
            if (name.StartsWith("if "))
            {
                // 'downlevel-revealed' comment (another atrocity of the IE team)
                m_current.ScanToEnd(null, "CDATA", ">");
                return false;
            }
            else if (!string.Equals(name, "CDATA", StringComparison.OrdinalIgnoreCase))
            {
                Log("Expecting CDATA but found '{0}'", name);
                m_current.ScanToEnd(null, "CDATA", ">");
                return false;
            }
            else
            {
                ch = m_current.SkipWhitespace();
                if (ch != '[')
                {
                    Log("Expecting '[' but found '{0}'", ch);
                    m_current.ScanToEnd(null, "CDATA", ">");
                    return false;
                }

                string value = m_current.ScanToEnd(m_sb, "CDATA", "]]>");

                Push(null, XmlNodeType.CDATA, value);
                return true;
            }
        }

        private const string dtterm = " \t\r\n>";
        private void ParseDocType()
        {
            char ch = this.m_current.SkipWhitespace();
            string name = this.ScanName(SgmlReader.dtterm);
            Push(name, XmlNodeType.DocumentType, null);
            ch = this.m_current.SkipWhitespace();
            if (ch != '>')
            {
                string subset = "";
                string pubid = "";
                string syslit = "";

                if (ch != '[')
                {
                    string token = this.m_current.ScanToken(this.m_sb, SgmlReader.dtterm, false);
                    if (string.Equals(token, "PUBLIC", StringComparison.OrdinalIgnoreCase))
                    {
                        ch = this.m_current.SkipWhitespace();
                        if (ch == '\"' || ch == '\'')
                        {
                            pubid = this.m_current.ScanLiteral(this.m_sb, ch);
                            this.m_node.AddAttribute(token, pubid, ch, this.m_folding == CaseFolding.None);
                        }
                    } 
                    else if (!string.Equals(token, "SYSTEM", StringComparison.OrdinalIgnoreCase))
                    {
                        Log("Unexpected token in DOCTYPE '{0}'", token);
                        this.m_current.ScanToEnd(null, "DOCTYPE", ">");
                    }
                    ch = this.m_current.SkipWhitespace();
                    if (ch == '\"' || ch == '\'')
                    {
                        token = "SYSTEM";
                        syslit = this.m_current.ScanLiteral(this.m_sb, ch);
                        this.m_node.AddAttribute(token, syslit, ch, this.m_folding == CaseFolding.None);  
                    }
                    ch = this.m_current.SkipWhitespace();
                }

                if (ch == '[')
                {
                    subset = this.m_current.ScanToEnd(this.m_sb, "Internal Subset", "]");
                    this.m_node.Value = subset;
                }

                ch = this.m_current.SkipWhitespace();
                if (ch != '>')
                {
                    Log("Expecting end of DOCTYPE tag, but found '{0}'", ch);
                    this.m_current.ScanToEnd(null, "DOCTYPE", ">");
                }

                if (this.m_dtd != null && !string.Equals(this.m_dtd.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("DTD does not match document type");
                }

                this.m_docType = name;
                this.m_pubid = pubid;
                this.m_syslit = syslit;
                this.m_subset = subset;
                LazyLoadDtd(this.m_current.ResolvedUri);
            }

            this.m_current.ReadChar();
        }

        private const string piterm = " \t\r\n?";
        private bool ParsePI()
        {
            string name = this.m_current.ScanToken(this.m_sb, SgmlReader.piterm, false);
            string value = null;
            if (this.m_current.Lastchar != '?')
            {
                // Notice this is not "?>".  This is because Office generates bogus PI's that end with "/>".
                value = this.m_current.ScanToEnd(this.m_sb, "Processing Instruction", ">");
                value = value.TrimEnd('/');
            }
            else
            {
                // error recovery.
                value = this.m_current.ScanToEnd(this.m_sb, "Processing Instruction", ">");
            }

            // check if the name has a prefix; if so, ignore it
            int colon = name.IndexOf(':');
            if(colon > 0) {
                name = name.Substring(colon + 1);
            }

            // skip xml declarations, since these are generated in the output instead.
            if (!string.Equals(name, "xml", StringComparison.OrdinalIgnoreCase))
            {
                Push(name, XmlNodeType.ProcessingInstruction, value);
                return true;
            }

            return false;
        }

        private bool ParseText(char ch, bool newtext)
        {
            bool ws = !newtext || this.m_current.IsWhitespace;
            if (newtext)
                this.m_sb.Length = 0;

            //this.sb.Append(ch);
            //ch = this.current.ReadChar();
            this.m_state = State.Text;
            while (ch != Entity.EOF)
            {
                if (ch == '<')
                {
                    ch = this.m_current.ReadChar();
                    if (ch == '/' || ch == '!' || ch == '?' || char.IsLetter(ch))
                    {
                        // Hit a tag, so return XmlNodeType.Text token
                        // and remember we partially started a new tag.
                        this.m_state = State.PartialTag;
                        this.m_partial = ch;
                        break;
                    } 
                    else
                    {
                        // not a tag, so just proceed.
                        this.m_sb.Append('<');
                        this.m_sb.Append(ch);
                        ws = false;
                        ch = this.m_current.ReadChar();
                    }
                } 
                else if (ch == '&')
                {
                    ExpandEntity(this.m_sb, '<');
                    ws = false;
                    ch = this.m_current.Lastchar;
                }
                else
                {
                    if (!this.m_current.IsWhitespace)
                        ws = false;
                    this.m_sb.Append(ch);
                    ch = this.m_current.ReadChar();
                }
            }

            string value = this.m_sb.ToString();
            Push(null, XmlNodeType.Text, value);
            return ws;
        }

        /// <summary>
        /// Consumes and returns a literal block of text, expanding entities as it does so.
        /// </summary>
        /// <param name="sb">The string builder to use.</param>
        /// <param name="quote">The delimiter for the literal.</param>
        /// <returns>The consumed literal.</returns>
        /// <remarks>
        /// This version is slightly different from <see cref="Entity.ScanLiteral"/> in that
        /// it also expands entities.
        /// </remarks>
        private string ScanLiteral(StringBuilder sb, char quote)
        {
            sb.Length = 0;
            char ch = this.m_current.ReadChar();
            while (ch != Entity.EOF && ch != quote && ch != '>')
            {
                if (ch == '&')
                {
                    ExpandEntity(sb, quote);
                    ch = this.m_current.Lastchar;
                }               
                else
                {
                    sb.Append(ch);
                    ch = this.m_current.ReadChar();
                }
            }
            if(ch == quote) {
                this.m_current.ReadChar(); // consume end quote.
            }
            return sb.ToString();
        }

        private bool ParseCData()
        {
            // Like ParseText(), only it doesn't allow elements in the content.  
            // It allows comments and processing instructions and text only and
            // text is not returned as text but CDATA (since it may contain angle brackets).
            // And initial whitespace is ignored.  It terminates when we hit the
            // end tag for the current CDATA node (e.g. </style>).
            bool ws = this.m_current.IsWhitespace;
            this.m_sb.Length = 0;
            char ch = this.m_current.Lastchar;
            if (this.m_partial != '\0')
            {
                Pop(); // pop the CDATA
                switch (this.m_partial)
                {
                    case '!':
                        this.m_partial = ' '; // and pop the comment next time around
                        return ParseComment();
                    case '?':
                        this.m_partial = ' '; // and pop the PI next time around
                        return ParsePI();
                    case '/':
                        this.m_state = State.EndTag;
                        return true;    // we are done!
                    case ' ':
                        break; // means we just needed to pop the Comment, PI or CDATA.
                }
            }            
            
            // if this.partial == '!' then parse the comment and return
            // if this.partial == '?' then parse the processing instruction and return.            
            while (ch != Entity.EOF)
            {
                if (ch == '<')
                {
                    ch = this.m_current.ReadChar();
                    if (ch == '!')
                    {
                        ch = this.m_current.ReadChar();
                        if (ch == '-')
                        {
                            // return what CDATA we have accumulated so far
                            // then parse the comment and return to here.
                            if (ws)
                            {
                                this.m_partial = ' '; // pop comment next time through
                                return ParseComment();
                            } 
                            else
                            {
                                // return what we've accumulated so far then come
                                // back in and parse the comment.
                                this.m_partial = '!';
                                break; 
                            }
#if FIX
                        } else if (ch == '['){
                            // We are about to wrap this node as a CDATA block because of it's
                            // type in the DTD, but since we found a CDATA block in the input
                            // we have to parse it as a CDATA block, otherwise we will attempt
                            // to output nested CDATA blocks which of course is illegal.
                            if (this.ParseConditionalBlock()){
                                this.partial = ' ';
                                return true;
                            }
#endif
                        }
                        else
                        {
                            // not a comment, so ignore it and continue on.
                            this.m_sb.Append('<');
                            this.m_sb.Append('!');
                            this.m_sb.Append(ch);
                            ws = false;
                        }
                    } 
                    else if (ch == '?')
                    {
                        // processing instruction.
                        this.m_current.ReadChar();// consume the '?' character.
                        if (ws)
                        {
                            this.m_partial = ' '; // pop PI next time through
                            return ParsePI();
                        } 
                        else
                        {
                            this.m_partial = '?';
                            break;
                        }
                    }
                    else if (ch == '/')
                    {
                        // see if this is the end tag for this CDATA node.
                        string temp = this.m_sb.ToString();
                        if (ParseEndTag() && string.Equals(this.m_endTag, this.m_node.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            if (ws || string.IsNullOrEmpty(temp))
                            {
                                // we are done!
                                return true;
                            } 
                            else
                            {
                                // return CDATA text then the end tag
                                this.m_partial = '/';
                                this.m_sb.Length = 0; // restore buffer!
                                this.m_sb.Append(temp);
                                this.m_state = State.CData;
                                break;
                            }
                        } 
                        else
                        {
                            // wrong end tag, so continue on.
                            this.m_sb.Length = 0; // restore buffer!
                            this.m_sb.Append(temp);
                            this.m_sb.Append("</" + this.m_endTag + ">");
                            ws = false;

                            // NOTE (steveb): we have one character in the buffer that we need to process next
                            ch = this.m_current.Lastchar;
                            continue;
                        }
                    }
                    else
                    {
                        // must be just part of the CDATA block, so proceed.
                        this.m_sb.Append('<');
                        this.m_sb.Append(ch);
                        ws = false;
                    }
                } 
                else
                {
                    if (!this.m_current.IsWhitespace && ws)
                        ws = false;
                    this.m_sb.Append(ch);
                }

                ch = this.m_current.ReadChar();
            }

            // NOTE (steveb): check if we reached EOF, which means it's over
            if(ch == Entity.EOF) {
                this.m_state = State.Eof;
                return false;
            }

            string value = this.m_sb.ToString();

            // NOTE (steveb): replace any nested CDATA sections endings
            value = value.Replace("<![CDATA[", string.Empty);
            value = value.Replace("]]>", string.Empty);
            value = value.Replace("/**/", string.Empty);

            Push(null, XmlNodeType.CDATA, value);
            if (this.m_partial == '\0')
                this.m_partial = ' ';// force it to pop this CDATA next time in.

            return true;
        }

        private void ExpandEntity(StringBuilder sb, char terminator)
        {
            char ch = this.m_current.ReadChar();
            if (ch == '#')
            {
                string charent = this.m_current.ExpandCharEntity();
                sb.Append(charent);
                ch = this.m_current.Lastchar;
            } 
            else
            {
                this.m_name.Length = 0;
                while (ch != Entity.EOF &&
                    (char.IsLetter(ch) || ch == '_' || ch == '-') || ((this.m_name.Length > 0) && char.IsDigit(ch)))
                {
                    this.m_name.Append(ch);
                    ch = this.m_current.ReadChar();
                }
                string name = this.m_name.ToString();
                if (this.m_dtd != null && !string.IsNullOrEmpty(name))
                {
                    Entity e = (Entity)this.m_dtd.FindEntity(name);
                    if (e != null)
                    {
                        if (e.IsInternal)
                        {
                            sb.Append(e.Literal);
                            if (ch != terminator && ch != '&' && ch != Entity.EOF)
                                ch = this.m_current.ReadChar();

                            return;
                        } 
                        else
                        {
                            Entity ex = new Entity(name, e.PublicId, e.Uri, this.m_current.Proxy);
                            e.Open(this.m_current, new Uri(e.Uri));
                            this.m_current = ex;
                            this.m_current.ReadChar();
                            return;
                        }
                    } 
                    else
                    {
                        Log("Undefined entity '{0}'", name);
                    }
                }
                // Entity is not defined, so just keep it in with the rest of the
                // text.
                sb.Append("&");
                sb.Append(name);
                if(ch != terminator && ch != '&' && ch != Entity.EOF)
                {
                    sb.Append(ch);
                    ch = this.m_current.ReadChar();
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the reader is positioned at the end of the stream.
        /// </summary>
        /// <value>true if the reader is positioned at the end of the stream; otherwise, false.</value>
        public override bool EOF
        {
            get
            {
                return this.m_state == State.Eof;
            }
        }

        /// <summary>
        /// Changes the <see cref="ReadState"/> to Closed.
        /// </summary>
        public override void Close()
        {
            if (this.m_current != null)
            {
                this.m_current.Close();
                this.m_current = null;
            }

            if (this.m_log != null)
            {
                this.m_log.Close();
                this.m_log = null;
            }
        }

        /// <summary>
        /// Gets the state of the reader.
        /// </summary>
        /// <value>One of the ReadState values.</value>
        public override ReadState ReadState
        {
            get
            {
                if (this.m_state == State.Initial)
                    return ReadState.Initial;
                else if (this.m_state == State.Eof)
                    return ReadState.EndOfFile;
                else
                    return ReadState.Interactive;
            }
        }

        /// <summary>
        /// Reads the contents of an element or text node as a string.
        /// </summary>
        /// <returns>The contents of the element or an empty string.</returns>
        public override string ReadString()
        {
            if (this.m_node.NodeType == XmlNodeType.Element)
            {
                this.m_sb.Length = 0;
                while (Read())
                {
                    switch (this.NodeType)
                    {
                        case XmlNodeType.CDATA:
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Whitespace:
                        case XmlNodeType.Text:
                            this.m_sb.Append(this.m_node.Value);
                            break;
                        default:
                            return this.m_sb.ToString();
                    }
                }

                return this.m_sb.ToString();
            }

            return this.m_node.Value;
        }

        /// <summary>
        /// Reads all the content, including markup, as a string.
        /// </summary>
        /// <returns>
        /// All the XML content, including markup, in the current node. If the current node has no children,
        /// an empty string is returned. If the current node is neither an element nor attribute, an empty
        /// string is returned.
        /// </returns>
        public override string ReadInnerXml()
        {
            StringWriter sw = new StringWriter(CultureInfo.InvariantCulture);
            XmlTextWriter xw = new XmlTextWriter(sw);
            xw.Formatting = Formatting.Indented;
            switch (this.NodeType)
            {
                case XmlNodeType.Element:
                    Read();
                    while (!this.EOF && this.NodeType != XmlNodeType.EndElement)
                    {
                        xw.WriteNode(this, true);
                    }
                    Read(); // consume the end tag
                    break;
                case XmlNodeType.Attribute:
                    sw.Write(this.Value);
                    break;
                default:
                    // return empty string according to XmlReader spec.
                    break;
            }

            xw.Close();
            return sw.ToString();
        }

        /// <summary>
        /// Reads the content, including markup, representing this node and all its children.
        /// </summary>
        /// <returns>
        /// If the reader is positioned on an element or an attribute node, this method returns all the XML content, including markup, of the current node and all its children; otherwise, it returns an empty string.
        /// </returns>
        public override string ReadOuterXml()
        {
            StringWriter sw = new StringWriter(CultureInfo.InvariantCulture);
            XmlTextWriter xw = new XmlTextWriter(sw);
            xw.Formatting = Formatting.Indented;
            xw.WriteNode(this, true);
            xw.Close();
            return sw.ToString();
        }

        /// <summary>
        /// Gets the XmlNameTable associated with this implementation.
        /// </summary>
        /// <value>The XmlNameTable enabling you to get the atomized version of a string within the node.</value>
        public override XmlNameTable NameTable
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a namespace prefix in the current element's scope.
        /// </summary>
        /// <param name="prefix">The prefix whose namespace URI you want to resolve. To match the default namespace, pass an empty string.</param>
        /// <returns>The namespace URI to which the prefix maps or a null reference (Nothing in Visual Basic) if no matching prefix is found.</returns>
        public override string LookupNamespace(string prefix)
        {
            return null; // there are no namespaces in SGML.
        }

        /// <summary>
        /// Resolves the entity reference for EntityReference nodes.
        /// </summary>
        /// <exception cref="InvalidOperationException">SgmlReader does not resolve or return entities.</exception>
        public override void ResolveEntity()
        {
            // We never return any entity reference nodes, so this should never be called.
            throw new InvalidOperationException("Not on an entity reference.");
        }

        /// <summary>
        /// Parses the attribute value into one or more Text, EntityReference, or EndEntity nodes.
        /// </summary>
        /// <returns>
        /// true if there are nodes to return. false if the reader is not positioned on an attribute node when the initial call is made or if all the
        /// attribute values have been read. An empty attribute, such as, misc="", returns true with a single node with a value of string.Empty.
        /// </returns>
        public override bool ReadAttributeValue()
        {
            if (this.m_state == State.Attr)
            {
                this.m_state = State.AttrValue;
                return true;
            }
            else if (this.m_state == State.AttrValue)
            {
                return false;
            }
            else
                throw new InvalidOperationException("Not on an attribute.");
        }   

        private void Validate(Node node)
        {
            if (this.m_dtd != null)
            {
                ElementDecl e = this.m_dtd.FindElement(node.Name);
                if (e != null)
                {
                    node.DtdType = e;
                    if (e.ContentModel.DeclaredContent == DeclaredContent.EMPTY)
                        node.IsEmpty = true;
                }
            }
        }

        private static void ValidateAttribute(Node node, Attribute a)
        {
            ElementDecl e = node.DtdType;
            if (e != null)
            {
                AttDef ad = e.FindAttribute(a.Name);
                if (ad != null)
                {
                    a.DtdType = ad;
                }
            }
        }

        private static bool ValidAttributeName(string name)
        {
            try
            {
                XmlConvert.VerifyNMTOKEN(name);
                int index = name.IndexOf(':');
                if (index >= 0)
                {
                    XmlConvert.VerifyNCName(name.Substring(index + 1));
                }

                return true;
            }
            catch (XmlException)
            {
                return false;
            }
            catch (ArgumentNullException)
            {
                // (steveb) this is probably a bug in XmlConvert.VerifyNCName when passing in an empty string
                return false;
            }
        }

        private void ValidateContent(Node node)
        {
            if (node.NodeType == XmlNodeType.Element)
            {
                if (!VerifyName(node.Name))
                {
                    Pop();
                    Push(null, XmlNodeType.Text, "<" + node.Name + ">");
                    return;
                }
            }

            if (this.m_dtd != null)
            {
                // See if this element is allowed inside the current element.
                // If it isn't, then auto-close elements until we find one
                // that it is allowed to be in.                                  
                string name = node.Name.ToUpperInvariant(); // DTD is in upper case
                int i = 0;
                int top = this.m_stack.Count - 2;
                if (node.DtdType != null) { 
                    // it is a known element, let's see if it's allowed in the
                    // current context.
                    for (i = top; i > 0; i--)
                    {
                        Node n = (Node)this.m_stack[i];
                        if (n.IsEmpty)
                            continue; // we'll have to pop this one
                        ElementDecl f = n.DtdType;
                        if (f != null)
                        {
                            if ((i == 2) && string.Equals(f.Name, "BODY", StringComparison.OrdinalIgnoreCase)) // NOTE (steveb): never close the BODY tag too early
                                break;
                            else if (string.Equals(f.Name, this.m_dtd.Name, StringComparison.OrdinalIgnoreCase))
                                break; // can't pop the root element.
                            else if (f.CanContain(name, this.m_dtd))
                            {
                                break;
                            }
                            else if (!f.EndTagOptional)
                            {
                                // If the end tag is not optional then we can't
                                // auto-close it.  We'll just have to live with the
                                // junk we've found and move on.
                                break;
                            }
                        } 
                        else
                        {
                            // Since we don't understand this tag anyway,
                            // we might as well allow this content!
                            break;
                        }
                    }
                }

                if (i == 0)
                {
                    // Tag was not found or is not allowed anywhere, ignore it and 
                    // continue on.
                    return;
                }
                else if (i < top)
                {
                    Node n = (Node)this.m_stack[top];
                    if (i == top - 1 && string.Equals(name, n.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        // e.g. p not allowed inside p, not an interesting error.
                    }
                    else
                    {
#if DEBUG
                        string closing = "";
                        for (int k = top; k >= i+1; k--) {
                            if (closing != "") closing += ",";
                            Node n2 = (Node)this.m_stack[k];
                            closing += "<" + n2.Name + ">";
                        }
                        Log("Element '{0}' not allowed inside '{1}', closing {2}.", name, n.Name, closing);
#endif
                    }

                    this.m_state = State.AutoClose;
                    this.m_newnode = node;
                    Pop(); // save this new node until we pop the others
                    this.m_poptodepth = i + 1;
                }
            }
        }
    }
}