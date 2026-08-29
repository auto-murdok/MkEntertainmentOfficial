using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Hover/press feedback for a main-menu button: lerps the label color, grows
/// the underline bar and brightens the panel while the pointer is over the
/// hit area. Runs on plain lerps toward cached targets — no coroutines, no
/// per-frame allocations, and it settles naturally if the pointer leaves
/// mid-transition.
/// </summary>
public class MenuButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private const float LerpSpeed = 14f;
    private const float BarWidthHover = 1.35f;
    private const float BarWidthIdle = 0.45f;
    private const float PressScale = 0.97f;

    private TMP_Text _label;
    private Image _bar;
    private Image _panel;

    private Color _idle;
    private Color _hover;
    private Color _barIdle;
    private Color _barHover;

    private bool _hovered;
    private bool _pressed;
    private float _barWidth;
    private float _barBaseWidth;
    private Color _panelIdle;
    private Color _panelHover;

    private RectTransform _rect;

    public void Configure(TMP_Text label, Image bar, Image panel, Color barIdle, Color barHover, Color labelIdle, Color labelHover)
    {
        _label = label;
        _bar = bar;
        _panel = panel;
        _barIdle = barIdle;
        _barHover = barHover;
        _idle = labelIdle;
        _hover = labelHover;
        _barWidth = BarWidthIdle;
        if (bar != null)
        {
            _barBaseWidth = bar.rectTransform.sizeDelta.x;
            bar.rectTransform.sizeDelta = new Vector2(_barBaseWidth * BarWidthIdle, bar.rectTransform.sizeDelta.y);
        }
        if (panel != null)
        {
            _panelIdle = panel.color;
            _panelHover = new Color(_panelIdle.r + 0.1f, _panelIdle.g + 0.02f, _panelIdle.b + 0.02f, _panelIdle.a + 0.18f);
        }
        _rect = (RectTransform)transform;
    }

    private void Update()
    {
        if (_label != null)
        {
            _label.color = Color.Lerp(_label.color, _hovered ? _hover : _idle, Time.unscaledDeltaTime * LerpSpeed);
        }

        if (_bar != null)
        {
            _bar.color = Color.Lerp(_bar.color, _hovered ? _barHover : _barIdle, Time.unscaledDeltaTime * LerpSpeed);
            _barWidth = Mathf.Lerp(_barWidth, _hovered ? BarWidthHover : BarWidthIdle, Time.unscaledDeltaTime * LerpSpeed);
            RectTransform barRect = _bar.rectTransform;
            barRect.sizeDelta = new Vector2(_barBaseWidth * _barWidth, barRect.sizeDelta.y);
        }

        if (_panel != null)
        {
            _panel.color = Color.Lerp(_panel.color, _hovered ? _panelHover : _panelIdle, Time.unscaledDeltaTime * LerpSpeed);
        }

        if (_rect != null)
        {
            Vector3 targetScale = Vector3.one * (_pressed ? PressScale : 1f);
            _rect.localScale = Vector3.Lerp(_rect.localScale, targetScale, Time.unscaledDeltaTime * LerpSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        _pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) => _pressed = true;

    public void OnPointerUp(PointerEventData eventData) => _pressed = false;
}
