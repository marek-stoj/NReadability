using System.Reflection;

namespace NReadability
{
  public static class Consts
  {
    private static readonly string _nReadabilityFullName;

    #region Constructor(s)

    static Consts()
    {
      _nReadabilityFullName = string.Format("NReadability {0}", Assembly.GetExecutingAssembly().GetName().Version);
    }
    
    #endregion

    #region Properties

    public static string NReadabilityFullName
    {
      get { return _nReadabilityFullName; }
    }

    #endregion

  }
}
