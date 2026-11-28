namespace Swoovr.Client;

/// <summary>Issue 448 (D457): wording for profile details on the web.</summary>
public static class ProfileDetailText
{
    /// <summary>"5′10″" from centimetres, shown beside the height field (the profile itself shows both units from the API).</summary>
    public static string Height(int centimetres)
    {
        var inches = (int)Math.Round(centimetres / 2.54);
        return $"About {inches / 12}′{inches % 12}″";
    }
}
