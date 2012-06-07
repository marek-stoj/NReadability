using System;

namespace NReadability
{
  public class DomSerializationParams
  {
    #region Factory methods

    /// <summary>
    /// Creates an instance of DomSerializationParams with parameters set to their defaults.
    /// </summary>
    public static DomSerializationParams CreateDefault()
    {
      return new DomSerializationParams();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Determines whether the output will be formatted.
    /// </summary>
    public bool PrettyPrint { get; set; }

    /// <summary>
    /// Determines whether DOCTYPE will be included at the beginning of the output.
    /// </summary>
    public bool DontIncludeContentTypeMetaElement { get; set; }

    /// <summary>
    /// Determines whether mobile-specific elements (such as eg. meta HandheldFriendly) will be added/replaced in the output.
    /// </summary>
    public bool DontIncludeMobileSpecificMetaElements { get; set; }

    /// <summary>
    /// Determines whether a meta tag with a content-type specification will be added/replaced in the output.
    /// </summary>
    public bool DontIncludeDocTypeMetaElement { get; set; }

    /// <summary>
    /// Determines whether a meta tag with a generator specification will be added/replaced in the output.
    /// </summary>
    public bool DontIncludeGeneratorMetaElement { get; set; }

    #endregion
  }
}
