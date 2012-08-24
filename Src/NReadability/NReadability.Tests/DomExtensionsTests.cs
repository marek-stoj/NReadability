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
using System.Xml.Linq;
using NUnit.Framework;

namespace NReadability.Tests
{
  [TestFixture]
  public class DomExtensionsTests
  {
    #region GetAttributesString Tests

    [Test]
    public void GetAttributesString_should_throw_exception_if_separator_is_null()
    {
      var element = new XElement("div");

      Assert.Throws(typeof(ArgumentNullException), () => element.GetAttributesString(null));
    }

    [Test]
    public void GetAttributesString_should_return_empty_string_if_element_has_no_attributes()
    {
      var element = new XElement("div");

      Assert.AreEqual("", element.GetAttributesString("|"));
    }

    [Test]
    public void GetAttributesString_should_return_a_string_with_a_single_attribute_if_element_has_only_one_attribute()
    {
      const string attributeValue = "container";
      var element = new XElement("div");

      element.SetAttributeValue("id", attributeValue);

      Assert.AreEqual(attributeValue, element.GetAttributesString("|"));
    }

    [Test]
    public void GetAttributesString_should_return_a_string_with_separated_attributes_if_element_has_more_than_one_attribute()
    {
      const string attributeValue1 = "container";
      const string attributeValue2 = "widget";
      const string separator = "|";
      var element = new XElement("div");

      element.SetAttributeValue("id", attributeValue1);
      element.SetAttributeValue("class", attributeValue2);

      Assert.AreEqual(attributeValue1 + separator + attributeValue2, element.GetAttributesString(separator));
    }

    #endregion

    #region GetInnerHtml and SetInnerHtml tests

    [Test]
    public void Test_GetInnerHtml_text()
    {
      const string innerHtml = "text1\r\ntext2";
      var element = XElement.Parse("<div>" + innerHtml + "</div>");

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_GetInnerHtml_text_multiline()
    {
      const string innerHtml = "text1\r\ntext2";
      var element = XElement.Parse("<div>" + innerHtml + "</div>");

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_GetInnerHtml_html()
    {
      const string innerHtml = "text1<p>text2</p>text3";
      var element = XElement.Parse("<div>" + innerHtml + "</div>");

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_GetInnerHtml_html_multiline()
    {
      const string innerHtml = "text1\r\n<p>\r\ntext2\r\n</p>\r\ntext3\r\n";
      var element = XElement.Parse("<div>" + innerHtml + "</div>");

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_SetInnerHtml_text()
    {
      const string innerHtml = "text";
      var element = new XElement("div");

      element.SetInnerHtml(innerHtml);

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_SetInnerHtml_text_multiline()
    {
      const string innerHtml = "\r\ntext1\r\ntext\r\n";
      var element = new XElement("div");

      element.SetInnerHtml(innerHtml);

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_SetInnerHtml_html()
    {
      const string innerHtml = "text1<p>text2</p>text3";
      var element = new XElement("div");

      element.SetInnerHtml(innerHtml);

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_SetInnerHtml_html_multiline()
    {
      const string innerHtml = "\r\ntext1\r\n<p>\r\ntext2\r\n</p>\r\ntext3\r\n";
      var element = new XElement("div");

      element.SetInnerHtml(innerHtml);

      Assert.AreEqual(innerHtml, element.GetInnerHtml());
    }

    [Test]
    public void Test_SetInnerHtml_html_with_entity_amp()
    {
      const string innerHtml = "&amp;";
      var element = new XElement("div");

      element.SetInnerHtml(innerHtml);

      Assert.IsTrue(element.GetInnerHtml().Contains("&"));
    }

    [Test]
    public void Test_SetInnerHtml_html_with_entity_raquo()
    {
      const string innerHtml = "&raquo;";
      var element = new XElement("div");

      element.SetInnerHtml(innerHtml);

      Assert.IsTrue(element.GetInnerHtml().Contains("»"));
    }

    #endregion
  }
}
