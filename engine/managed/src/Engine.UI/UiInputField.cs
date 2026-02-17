using System;
using Engine.Core.Handles;

namespace Engine.UI;

public sealed class UiInputField : UiElement
{
    private TextureHandle _backgroundTexture;
    private TextureHandle _fontTexture;
    private string _text = string.Empty;
    private string _placeholder = string.Empty;
    private int _maxLength = 256;

    public UiInputField(
        string id,
        TextureHandle backgroundTexture,
        TextureHandle fontTexture,
        string text = "",
        string placeholder = "",
        Action<string>? onTextChanged = null)
        : base(id)
    {
        BackgroundTexture = backgroundTexture;
        FontTexture = fontTexture;
        Text = text;
        Placeholder = placeholder;
        OnTextChanged = onTextChanged;
    }

    public TextureHandle BackgroundTexture
    {
        get => _backgroundTexture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Background texture handle must be valid.", nameof(value));
            }

            _backgroundTexture = value;
        }
    }

    public TextureHandle FontTexture
    {
        get => _fontTexture;
        set
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("Font texture handle must be valid.", nameof(value));
            }

            _fontTexture = value;
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length > _maxLength)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Text length cannot exceed max length {_maxLength}.");
            }

            _text = value;
        }
    }

    public string Placeholder
    {
        get => _placeholder;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _placeholder = value;
        }
    }

    public int MaxLength
    {
        get => _maxLength;
        set
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Max length must be greater than zero.");
            }

            if (_text.Length > value)
            {
                throw new InvalidOperationException("Current text is longer than the requested max length.");
            }

            _maxLength = value;
        }
    }

    public bool IsFocused { get; private set; }

    public Action<string>? OnTextChanged { get; set; }

    public string DisplayText => string.IsNullOrEmpty(_text) ? _placeholder : _text;

    internal void Focus() => IsFocused = true;

    internal void Blur() => IsFocused = false;

    internal void AppendText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsFocused || text.Length == 0)
        {
            return;
        }

        int available = _maxLength - _text.Length;
        if (available <= 0)
        {
            return;
        }

        string toAppend = text.Length <= available ? text : text[..available];
        _text += toAppend;
        OnTextChanged?.Invoke(_text);
    }

    internal void Backspace()
    {
        if (!IsFocused || _text.Length == 0)
        {
            return;
        }

        _text = _text[..^1];
        OnTextChanged?.Invoke(_text);
    }
}
