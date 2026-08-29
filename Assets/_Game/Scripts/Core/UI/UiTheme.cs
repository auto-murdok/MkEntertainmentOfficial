using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

// Shared premium-UI toolkit: procedural textures, the blood-red accent
// palette, the animated menu button, and EventSystem bootstrapping. One
// source of truth so the main menu and the game-over screen read as the
// same product.
public static class UiTheme
{
    // Palette: near-black base with blood-red accents.
    public static readonly Color Accent = new Color(0.72f, 0.09f, 0.07f, 1f);
    public static readonly Color AccentBright = new Color(0.95f, 0.22f, 0.13f, 1f);
    public static readonly Color ButtonIdle = new Color(0.92f, 0.88f, 0.86f, 0.82f);
    public static readonly Color ButtonHover = new Color(1f, 0.97f, 0.94f, 1f);
    public static readonly Color PanelTint = new Color(0.10f, 0.02f, 0.02f, 0.6f);

    public static void StretchFull(RectTransform rect)
    {
        if (rect == null) return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        StretchFull(go.transform as RectTransform);
        return image;
    }

    public static Sprite VerticalGradientSprite(Color top, Color bottom)
    {
        var texture = new Texture2D(4, 256, TextureFormat.RGBA32, false);
        for (int y = 0; y < 256; y++)
        {
            Color c = Color.Lerp(bottom, top, y / 255f);
            for (int x = 0; x < 4; x++)
            {
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, 4, 256), new Vector2(0.5f, 0.5f));
    }

    public static Sprite VignetteSprite()
    {
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float a = Mathf.Clamp01(Mathf.InverseLerp(0.55f, 1.15f, d));
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        }
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    public static Sprite PanelSprite()
    {
        const int size = 48;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                // Grayscale sprite: the tint (image.color) supplies the hue, so
                // the sliced border picks up the accent automatically.
                Color c = d < 2
                    ? new Color(1f, 1f, 1f, 0.9f)
                    : new Color(1f, 1f, 1f, 0.5f);
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply(false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6f, 6f, 6f, 6f));
    }

    // Premium menu button: invisible hit area + sliced panel + accent
    // underline + TMP label, animated by MenuButtonFX. Shared by the main
    // menu and the game-over screen so both read identically.
    public static Button CreateMenuButton(Transform parent, string name, string label, Vector2 anchor, Action onClick)
    {
        GameObject buttonGo = new GameObject(name);
        buttonGo.transform.SetParent(parent, false);
        Button button = buttonGo.AddComponent<Button>();

        // Invisible hit area; the visuals live on children so the button
        // component's own transition stays disabled (we animate manually).
        Image hitArea = buttonGo.GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = buttonGo.AddComponent<Image>();
        }
        hitArea.color = Color.clear;

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = new Vector2(560f, 96f);
        rect.anchoredPosition = Vector2.zero;

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(buttonGo.transform, false);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.sprite = PanelSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.color = PanelTint;
        StretchFull(panel.transform as RectTransform);

        GameObject bar = new GameObject("Underline");
        bar.transform.SetParent(buttonGo.transform, false);
        Image barImage = bar.AddComponent<Image>();
        barImage.color = Accent;
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0f);
        barRect.anchorMax = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(120f, 3f);
        barRect.anchoredPosition = new Vector2(0f, 6f);

        GameObject labelGo = new GameObject("Label");
        labelGo.transform.SetParent(buttonGo.transform, false);
        TMP_Text buttonText = labelGo.AddComponent<TextMeshProUGUI>();
        buttonText.text = label;
        buttonText.fontSize = 44f;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.characterSpacing = 6f;
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.color = ButtonIdle;
        buttonText.raycastTarget = false;
        StretchFull(labelGo.transform as RectTransform);

        MenuButtonFX fx = buttonGo.AddComponent<MenuButtonFX>();
        fx.Configure(buttonText, barImage, panelImage, Accent, AccentBright, ButtonIdle, ButtonHover);

        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());
        return button;
    }

    // Input System package is the project standard — the legacy
    // StandaloneInputModule breaks under "Input System only". Menus and
    // overlays in any scene get a working EventSystem through this.
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<InputSystemUIInputModule>();
    }
}
