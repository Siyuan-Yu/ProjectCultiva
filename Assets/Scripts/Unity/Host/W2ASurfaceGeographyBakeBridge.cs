using XianXia.Data.Content;

namespace XianXia.Unity.Host
{
    /// <summary>Narrow assembly bridge used only by the Editor bake menu.</summary>
    public static class W2ASurfaceGeographyBakeBridge
    {
        public static void Bake(string sourcePath, string surfacePath, string outputPath) =>
            W2ASurfaceGeographyBaker.BakeFile(sourcePath, surfacePath, outputPath);
    }
}
