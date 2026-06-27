namespace Texy
{
    /// <summary>Creates the active <see cref="ITextureProvider"/> from the current settings.</summary>
    public static class ProviderFactory
    {
        public static ITextureProvider Create(TexySettings settings)
        {
            switch (settings.ActiveEngine)
            {
                case TexySettings.Engine.AI:
                    return new AITextureProvider(settings);
                default:
                    return new ProceduralTextureProvider();
            }
        }
    }
}
