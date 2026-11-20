using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Common
{
    /// <summary>Shared UI art direction for authored prefabs and panels assembled during gameplay.</summary>
    public static class DarkFantasyTheme
    {
        private const string Root = "Art/DarkFantasy/UI/";

        public static readonly Color Ink = new Color32(14, 18, 25, 248);
        public static readonly Color Panel = new Color32(30, 35, 46, 247);
        public static readonly Color Inset = new Color32(20, 26, 36, 242);
        public static readonly Color Slot = new Color32(52, 59, 72, 255);
        public static readonly Color EmptySlot = new Color32(29, 35, 47, 255);
        public static readonly Color Selected = new Color32(131, 91, 45, 255);
        public static readonly Color Bronze = new Color32(140, 99, 52, 255);
        public static readonly Color Gold = new Color32(236, 188, 101, 255);
        public static readonly Color Ivory = new Color32(243, 234, 214, 255);
        public static readonly Color Muted = new Color32(182, 187, 193, 255);
        public static readonly Color Danger = new Color32(152, 53, 51, 255);
        public static readonly Color Action = new Color32(46, 104, 120, 255);
        public static readonly Color Success = new Color32(55, 125, 91, 255);

        private static Sprite panelFrame, buttonFrame, slotFrame, circleFrame, joystickRing, joystickKnob;
        public static Sprite PanelFrame => panelFrame != null ? panelFrame : (panelFrame = Resources.Load<Sprite>(Root + "PanelFrame"));
        public static Sprite ButtonFrame => buttonFrame != null ? buttonFrame : (buttonFrame = Resources.Load<Sprite>(Root + "ButtonFrame"));
        public static Sprite SlotFrame => slotFrame != null ? slotFrame : (slotFrame = Resources.Load<Sprite>(Root + "SlotFrame"));
        public static Sprite CircleFrame => circleFrame != null ? circleFrame : (circleFrame = Resources.Load<Sprite>(Root + "CircleFrame"));
        public static Sprite JoystickRing => joystickRing != null ? joystickRing : (joystickRing = Resources.Load<Sprite>(Root + "JoystickRing"));
        public static Sprite JoystickKnob => joystickKnob != null ? joystickKnob : (joystickKnob = Resources.Load<Sprite>(Root + "JoystickKnob"));

        public static void FramePanel(Image image, Color tint)
        {
            if (image == null) return;
            SetFrame(image, PanelFrame, tint, Image.Type.Sliced);
        }

        public static void FrameSlot(Image image, Color tint)
        {
            if (image == null) return;
            SetFrame(image, SlotFrame, tint, Image.Type.Sliced);
        }

        public static void FrameButton(Button button, Color tint, bool circular = false)
        {
            if (button == null) return;
            if (button.targetGraphic is Image image)
                SetFrame(image, circular ? CircleFrame : ButtonFrame, tint, circular ? Image.Type.Simple : Image.Type.Sliced);
            var colors = button.colors;
            // Tint comes from Image so selected/disabled states don't erase the graphic palette.
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.94f, 0.79f, 1f);
            colors.selectedColor = new Color(1f, 0.94f, 0.79f, 1f);
            colors.pressedColor = new Color(0.76f, 0.70f, 0.60f, 1f);
            colors.disabledColor = new Color(0.59f, 0.59f, 0.59f, 0.6f);
            button.colors = colors;
        }

        public static void SetFrame(Image image, Sprite sprite, Color tint, Image.Type type)
        {
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = type;
            }
            image.color = tint;
        }
    }
}
