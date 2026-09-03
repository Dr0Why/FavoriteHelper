using System.Reflection;

[assembly: AssemblyVersion(FavoriteHelper.ReleaseVersion.FileVersion)]
[assembly: AssemblyFileVersion(FavoriteHelper.ReleaseVersion.FileVersion)]
[assembly: AssemblyInformationalVersion(FavoriteHelper.ReleaseVersion.ProductVersion)]

namespace FavoriteHelper
{
    internal static class ReleaseVersion
    {
        internal const string ProductVersion = "6.3.0";
        internal const string FileVersion = "6.3.0.0";
    }
}
