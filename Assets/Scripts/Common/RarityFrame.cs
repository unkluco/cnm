using UnityEngine;
using UnityEngine.UI;

namespace LightSouls.Common
{
    /// <summary>
    /// Viền màu theo độ hiếm quanh một ô vật phẩm. Gọi RarityFrame.Apply(ô, vậtPhẩm) mỗi khi ô được vẽ lại:
    /// lần đầu tự tạo con "RarityFrame" (ảnh 9-slice chỉ có viền), các lần sau chỉ đổi màu.
    /// Vật phẩm null thì ẩn viền. Mọi giao diện ô đồ (thanh nhanh, balo, hòm, cửa hàng) dùng chung hàm này.
    /// </summary>
    public static class RarityFrame
    {
        public const string FrameName = "RarityFrame";
        private static Sprite frameSprite;

        public static void Apply(RectTransform slot, ItemDefinitionSO item)
        {
            if (slot == null)
            {
                return;
            }

            bool show = item != null;
            Transform existing = slot.Find(FrameName);
            if (existing == null && !show)
            {
                return;
            }

            Image frame = existing != null ? existing.GetComponent<Image>() : Create(slot);
            frame.gameObject.SetActive(show);
            if (!show)
            {
                return;
            }

            // Viền luôn vẽ trên cùng ô (trên icon, nền) nhưng không chặn bấm.
            frame.transform.SetAsLastSibling();
            Color color = ItemRarityStyle.GetColor(item.Rarity);
            frame.color = color;

            var glow = frame.GetComponent<RarityFrameGlow>();
            if (ItemRarityStyle.Glows(item.Rarity))
            {
                if (glow == null)
                {
                    glow = frame.gameObject.AddComponent<RarityFrameGlow>();
                }

                glow.SetBaseColor(color);
                glow.enabled = true;
            }
            else if (glow != null)
            {
                glow.enabled = false;
                frame.color = color;
            }
        }

        private static Image Create(RectTransform slot)
        {
            var go = new GameObject(FrameName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(slot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-3f, -3f);
            rt.offsetMax = new Vector2(3f, 3f);

            var image = go.GetComponent<Image>();
            image.sprite = GetSprite();
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Ảnh 32x32: viền đặc 4 px, phía trong mờ dần thêm 6 px như ánh sáng hắt vào ô.</summary>
        private static Sprite GetSprite()
        {
            if (frameSprite != null)
            {
                return frameSprite;
            }

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    float alpha = edge < 4 ? 1f : Mathf.Clamp01(1f - (edge - 3) / 7f) * 0.4f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            frameSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
            frameSprite.hideFlags = HideFlags.DontSave;
            return frameSprite;
        }
    }
}
