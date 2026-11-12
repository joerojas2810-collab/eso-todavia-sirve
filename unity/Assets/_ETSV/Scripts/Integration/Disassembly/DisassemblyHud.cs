// [ETSV] Integración — HUD mínimo de desmontaje (Instrucción 06 §8).
// UI Toolkit construida en código (sin assets nuevos que cablear en el editor):
// reutiliza el PanelSettings del HUD de la base cuando existe. Muestra objeto,
// componente, fijación, herramienta requerida, progreso, calidad, estado y pistas
// contextuales — todo derivado del estado SINCRONIZADO (NetworkList<CompSync>),
// nunca de decisiones locales.

using UnityEngine;
using UnityEngine.UIElements;
using Etsv.Domain;

namespace Etsv.Integration
{
    [RequireComponent(typeof(UIDocument))]
    public class DisassemblyHud : MonoBehaviour
    {
        private VisualElement _root;
        private VisualElement _panel;
        private Label _objectName, _compName, _stateLabel, _qualityLabel;
        private Label _toolLabel, _hintLabel, _toolbarLabel;
        private VisualElement _progressFill;

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            // Reutilizar el PanelSettings de la base (Player UI) si está cargado.
            if (doc.panelSettings == null)
            {
                var existing = FindFirstObjectByType<UIDocument>();
                if (existing != null && existing != doc) doc.panelSettings = existing.panelSettings;
            }
            if (doc.panelSettings == null)
            {
                doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            }
            doc.sortingOrder = 10; // por encima del HUD base (crosshair)

            _root = doc.rootVisualElement;
            Build();
        }

        private void Build()
        {
            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.left = Length.Percent(50);
            _panel.style.bottom = 72;
            _panel.style.translate = new Translate(Length.Percent(-50), Length.Pixel(0));
            _panel.style.width = 420;
            _panel.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.82f);
            _panel.style.borderTopLeftRadius = 12; _panel.style.borderTopRightRadius = 12;
            _panel.style.borderBottomLeftRadius = 12; _panel.style.borderBottomRightRadius = 12;
            _panel.style.paddingLeft = 16; _panel.style.paddingRight = 16;
            _panel.style.paddingTop = 10; _panel.style.paddingBottom = 10;
            _panel.style.display = DisplayStyle.None;
            _root.Add(_panel);

            _objectName = MakeLabel(18, FontStyle.Bold, new Color(0.95f, 0.95f, 0.98f));
            _compName = MakeLabel(15, FontStyle.Normal, new Color(0.8f, 0.82f, 0.88f));
            _stateLabel = MakeLabel(13, FontStyle.Bold, new Color(0.6f, 0.85f, 0.65f));
            _qualityLabel = MakeLabel(13, FontStyle.Normal, new Color(0.85f, 0.8f, 0.6f));
            _toolLabel = MakeLabel(13, FontStyle.Bold, new Color(0.95f, 0.85f, 0.5f));
            _toolbarLabel = MakeLabel(12, FontStyle.Normal, new Color(0.7f, 0.72f, 0.78f));
            _hintLabel = MakeLabel(14, FontStyle.Bold, Color.white);

            var row1 = new VisualElement(); row1.style.flexDirection = FlexDirection.Row;
            row1.style.justifyContent = Justify.SpaceBetween;
            row1.Add(_stateLabel); row1.Add(_qualityLabel);
            _panel.Add(_objectName);
            _panel.Add(_compName);
            _panel.Add(row1);
            _panel.Add(_toolLabel);

            // barra de progreso
            var bar = new VisualElement();
            bar.style.height = 10;
            bar.style.marginTop = 6; bar.style.marginBottom = 6;
            bar.style.backgroundColor = new Color(0.15f, 0.16f, 0.2f, 1f);
            bar.style.borderTopLeftRadius = 5; bar.style.borderTopRightRadius = 5;
            bar.style.borderBottomLeftRadius = 5; bar.style.borderBottomRightRadius = 5;
            _progressFill = new VisualElement();
            _progressFill.style.height = 10;
            _progressFill.style.width = Length.Percent(0);
            _progressFill.style.backgroundColor = new Color(0.35f, 0.85f, 0.4f, 1f);
            _progressFill.style.borderTopLeftRadius = 5; _progressFill.style.borderBottomLeftRadius = 5;
            bar.Add(_progressFill);
            _panel.Add(bar);

            _panel.Add(_hintLabel);
            _panel.Add(_toolbarLabel);
        }

        private Label MakeLabel(int size, FontStyle style, Color color)
        {
            var l = new Label();
            l.style.fontSize = size;
            l.style.unityFontStyleAndWeight = style;
            l.style.color = color;
            return l;
        }

        private void Update()
        {
            var c = DisassemblyPlayerController.Current;
            // barra de herramientas siempre visible durante el juego
            string toolbar = ToolbarText(c);
            if (_toolbarLabel.text != toolbar) _toolbarLabel.text = toolbar;

            if (c == null || !c.HasTarget)
            {
                if (_panel.style.display != DisplayStyle.None) _panel.style.display = DisplayStyle.None;
                return;
            }

            if (_panel.style.display != DisplayStyle.Flex) _panel.style.display = DisplayStyle.Flex;

            // Cabecera: nombre del objeto arriba del panel (reutiliza labels existentes).
            SetText(_objectName, c.ObjectName);
            SetText(_compName, c.CompName + (c.FastenerLabel != null ? " · " + c.FastenerLabel : ""));
            SetText(_stateLabel, c.StateLabel);
            SetText(_qualityLabel, "Calidad " + c.Quality + "%");
            SetText(_toolLabel, c.RequiredToolLabel);
            SetText(_hintLabel, c.Hint);

            float pct = Mathf.Clamp01((float)c.Progress01) * 100f;
            _progressFill.style.width = Length.Percent(pct);
            bool showBar = c.FastenerLabel != null;
            _progressFill.parent.style.display = showBar ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetText(Label l, string v)
        {
            string s = v ?? "";
            if (l.text != s) l.text = s;
        }

        private static string ToolbarText(DisassemblyPlayerController c)
        {
            var sb = new System.Text.StringBuilder("Herramientas: ");
            for (int i = 0; i < GameCatalog.Tools.Length; i++)
            {
                var t = GameCatalog.Tools[i];
                bool sel = c != null && c.ToolIdx == i;
                sb.Append(sel ? "[" : "");
                sb.Append(t.Key).Append(" ").Append(t.Icon).Append(" ").Append(t.Name);
                sb.Append(sel ? "]" : "");
                if (i < GameCatalog.Tools.Length - 1) sb.Append("   ");
            }
            return sb.ToString();
        }
    }
}
