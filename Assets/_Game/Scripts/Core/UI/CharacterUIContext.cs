public struct CharacterUIContext {
    public bool displayCrosshair;
    public int maxClipSize;
    public int clipSize;

    public static CharacterUIContext CreateAimUI(bool displayCrosshair)
    {
        return new CharacterUIContext
        {
            displayCrosshair = displayCrosshair
        };
    }

    public static CharacterUIContext CreateShootUI(int clipSize, int maxClipSize)
    {
        return new CharacterUIContext
        {
            clipSize = clipSize,
            maxClipSize = maxClipSize
        };
    }
}