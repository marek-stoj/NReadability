<%@PAGE LANGUAGE=C# debug="true" validateRequest=false %>
<%@Import Namespace="System"%>
<%@Import Namespace="System.Xml"%>
<%@Import Namespace="System.Xml.Schema"%>
<%@Import Namespace="System.IO"%>
<%@Import Namespace="Sgml"%>
<html>
<style>
h2 { border-left: 10 solid navy; border-top: 1 solid navy; padding-left:15; }
</style>
<script runat="server">
string SgmlReaderTest(Uri baseUri, string html, TextWriter log, bool upper, bool formatted)
{
  try {
    SgmlReader r = new SgmlReader();
    r.SetBaseUri(Server.MapPath("."));
    r.DocType = "HTML";
    r.InputStream = new StringReader(html);
    if (upper) r.CaseFolding = CaseFolding.ToUpper;
    StringWriter sw = new StringWriter();
    XmlTextWriter w = new XmlTextWriter(sw);
    if (formatted) {
        w.Formatting = Formatting.Indented;
        r.WhitespaceHandling = WhitespaceHandling.None;
    }
    while (!r.EOF) {
        w.WriteNode(r, true);
    }
    w.Close();   
    return sw.ToString();              
  } catch (Exception e) {
    return e.ToString();
  }
}
void SubmitBtn_Click(Object Src, EventArgs E) 
{
    StringWriter log = new StringWriter();
    bool upper = (Request.Form["UPPER"] == "on");
    bool formatted = (Request.Form["PRETTY"] == "on");
    string result = SgmlReaderTest(
	  new Uri("file://"+Server.MapPath(".")),
	  DATA.InnerText, log, upper, formatted);
	XML.InnerText = result;
}
</script>
<h2>SgmlReader Demo</h2>
<form runat="server" method="POST" action="demo.aspx">
<h4 style="margin:0;background-color:navy;color:white">HTML</h4>
<textarea runat="server" rows=10 cols=70 id=DATA>
<html><body>
<!-- This typical SGML document contains unclosed tags, 
unquoted attributes, attributes with no values, 
duplicate attributes, and mismatched end tags  -->
<table width=200>
<tr><td align=left>
<tr><td><input type=checkbox checked>
<tr><td align=left align=right>
</table>
</html>
</textarea>
<h4 style="margin:0;background-color:navy;color:white">XML</h4>
<textarea runat="server" rows=20 cols=70 id=XML></textarea>
<br/>

<asp:button text="SUBMIT"  Onclick="SubmitBtn_Click" runat=server/>

<asp:checkbox text="Upper case" id="UPPER" runat=server/>
<asp:checkbox text="Pretty print" id="PRETTY" runat=server/>

<br/><br/>
See <a href="/srcview/srcview.aspx?path=/tools/sgmlreader/sgmlreader.src&file=Demo.aspx">Source Code</a>
for this page.

 </form>
</html> 