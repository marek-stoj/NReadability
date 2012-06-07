namespace NReadability
{
  public class AttributeTransformationResult
  {
    /// <summary>
    /// Result of the transformation.
    /// </summary>
    public string TransformedValue { get; set; }

    /// <summary>
    /// Name of the attribute that will be used to store the original value. Can be null.
    /// </summary>
    public string OriginalValueAttributeName { get; set; }
  }
}
