using UnityEngine;
using UnityEngine.UIElements;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// The vocabulary every panel builds with. Small, boring helpers on purpose: the point
    /// is that no panel writes raw style code twice and that the theme is the only place a
    /// colour is decided.
    /// </summary>
    public static class CraftingUIStyle
    {
        /// <summary>Root container: opaque panel with padding and rounded corners.</summary>
        public static VisualElement Panel(CraftingUITheme theme, string name = null)
        {
            var element = new VisualElement { name = name };
            element.style.backgroundColor = theme.Background;
            element.style.paddingLeft = theme.Spacing;
            element.style.paddingRight = theme.Spacing;
            element.style.paddingTop = theme.Spacing;
            element.style.paddingBottom = theme.Spacing;
            SetRadius(element, theme.CornerRadius);
            SetBorder(element, theme.Border, theme.BorderWidth);
            return element;
        }

        /// <summary>A raised block inside a panel: one slot, one queue row, one recipe.</summary>
        public static VisualElement Card(CraftingUITheme theme, string name = null)
        {
            var element = new VisualElement { name = name };
            element.style.backgroundColor = theme.Surface;
            element.style.paddingLeft = theme.Spacing;
            element.style.paddingRight = theme.Spacing;
            element.style.paddingTop = theme.SpacingTight;
            element.style.paddingBottom = theme.SpacingTight;
            element.style.marginBottom = theme.SpacingTight;
            SetRadius(element, theme.CornerRadius);
            SetBorder(element, theme.Border, theme.BorderWidth);
            return element;
        }

        /// <summary>Horizontal flow with centred children.</summary>
        public static VisualElement Row(CraftingUITheme theme, string name = null)
        {
            var element = new VisualElement { name = name };
            element.style.flexDirection = FlexDirection.Row;
            element.style.alignItems = Align.Center;
            return element;
        }

        /// <summary>Vertical flow.</summary>
        public static VisualElement Column(string name = null)
        {
            var element = new VisualElement { name = name };
            element.style.flexDirection = FlexDirection.Column;
            return element;
        }

        /// <summary>Invisible element that eats the leftover space in a row.</summary>
        public static VisualElement Spacer()
        {
            var element = new VisualElement();
            element.style.flexGrow = 1f;
            return element;
        }

        public static Label Title(CraftingUITheme theme, string text)
        {
            var label = new Label(text);
            label.style.color = theme.TextPrimary;
            label.style.fontSize = theme.FontTitle;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = theme.SpacingTight;
            return label;
        }

        public static Label Body(CraftingUITheme theme, string text)
        {
            var label = new Label(text);
            label.style.color = theme.TextPrimary;
            label.style.fontSize = theme.FontBody;
            return label;
        }

        public static Label Muted(CraftingUITheme theme, string text)
        {
            var label = new Label(text);
            label.style.color = theme.TextMuted;
            label.style.fontSize = theme.FontSmall;
            return label;
        }

        /// <summary>Square icon holder. Shows the theme's empty track when the sprite is null.</summary>
        public static VisualElement Icon(CraftingUITheme theme, Sprite sprite, float size = 0f)
        {
            float resolved = size > 0f ? size : theme.IconSize;

            var element = new VisualElement();
            element.style.width = resolved;
            element.style.height = resolved;
            element.style.flexShrink = 0f;
            element.style.backgroundColor = sprite == null ? theme.TrackEmpty : Color.clear;
            element.style.backgroundImage = sprite == null
                ? new StyleBackground()
                : new StyleBackground(sprite);
            element.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            element.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            SetRadius(element, theme.CornerRadius);
            return element;
        }

        /// <summary>Replaces the sprite of an element built by <see cref="Icon"/>.</summary>
        public static void SetIcon(VisualElement element, CraftingUITheme theme, Sprite sprite)
        {
            if (element == null)
                return;

            element.style.backgroundColor = sprite == null ? theme.TrackEmpty : Color.clear;
            element.style.backgroundImage = sprite == null
                ? new StyleBackground()
                : new StyleBackground(sprite);
        }

        public static Button Action(CraftingUITheme theme, string text, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.color = theme.TextPrimary;
            button.style.backgroundColor = theme.SurfaceRaised;
            button.style.fontSize = theme.FontBody;
            button.style.marginLeft = 0f;
            button.style.marginRight = theme.SpacingTight;
            button.style.marginTop = 0f;
            button.style.marginBottom = 0f;
            button.style.paddingLeft = theme.Spacing;
            button.style.paddingRight = theme.Spacing;
            button.style.height = 26f;
            SetRadius(button, theme.CornerRadius);
            SetBorder(button, theme.Border, theme.BorderWidth);
            return button;
        }

        /// <summary>Greys a control out without removing it from the layout.</summary>
        public static void SetEnabledLook(VisualElement element, CraftingUITheme theme, bool enabled)
        {
            if (element == null)
                return;

            element.SetEnabled(enabled);
            element.style.opacity = enabled ? 1f : 0.45f;
        }

        public static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        public static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }

        public static void SetBorderColor(VisualElement element, Color color)
        {
            element.style.borderTopColor = color;
            element.style.borderRightColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
        }
    }
}