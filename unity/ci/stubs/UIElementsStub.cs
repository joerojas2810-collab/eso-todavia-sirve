// Stub local de UnityEngine.UIElements para verificación de tipos (no va al repo).
using System;
using UnityEngine;
namespace UnityEngine.UIElements
{
    public struct Length
    {
        public static Length Percent(float v) => default(Length);
        public static Length Pixel(float v) => default(Length);
        public static implicit operator Length(float v) => default(Length);
        public static implicit operator Length(int v) => default(Length);
    }
    public struct Translate
    {
        public Translate(Length x, Length y) {}
    }
    public enum DisplayStyle { None, Flex }
    public enum Position { Relative, Absolute }
    public enum FlexDirection { Column, Row }
    public enum Justify { FlexStart, Center, FlexEnd, SpaceBetween, SpaceAround }
    public enum FontStyle { Normal, Bold, Italic }

    public class StyleWrapper
    {
        public Position position;
        public Length left, right, top, bottom, width, height;
        public Translate translate;
        public Color backgroundColor;
        public Length borderTopLeftRadius, borderTopRightRadius, borderBottomLeftRadius, borderBottomRightRadius;
        public Length paddingLeft, paddingRight, paddingTop, paddingBottom;
        public Length marginTop, marginBottom;
        public DisplayStyle display;
        public FlexDirection flexDirection;
        public Justify justifyContent;
        public int fontSize;
        public FontStyle unityFontStyleAndWeight;
        public Color color;
    }

    public class VisualElement
    {
        public StyleWrapper style = new StyleWrapper();
        public VisualElement parent => null;
        public void Add(VisualElement child) {}
        public void Remove(VisualElement child) {}
        public T Q<T>(string name = null) where T : VisualElement => default(T);
    }
    public class Label : VisualElement
    {
        public string text;
    }
    public class Button : Label {}
    public class ScrollView : VisualElement {}

    public class PanelSettings : ScriptableObject {}

    public class UIDocument : MonoBehaviour
    {
        public VisualElement rootVisualElement => null;
        public PanelSettings panelSettings;
        public int sortingOrder;
        public VisualElement sourceAsset;
    }
}
