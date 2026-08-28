public struct CharacterUIContext {
    public bool displayCrossair;
    public int maxClipSize;
    public int clipSize;

    public static CharacterUIContext CreateAimUI(bool displayCrosshair)
    {
        return new CharacterUIContext
        {
            displayCrossair = displayCrosshair
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