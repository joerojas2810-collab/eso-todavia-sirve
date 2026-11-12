// [ETSV] Integración — HUD mínimo de desmontaje + economía (Instrucciones 06 §8, 07 §9).
// UI Toolkit construida en código (sin assets nuevos que cablear en el editor):
// reutiliza el PanelSettings del HUD de la base cuando existe.
//  - Panel inferior: objeto/estación, componente, fijación, herramienta requerida,
//    progreso, calidad, estado y pistas contextuales — todo derivado del estado
//    SINCRONIZADO (NetworkList<CompSync>, NVs de economía), nunca de decisiones locales.
//  - Saldo compartido arriba a la derecha (NetworkVariable Money del host).
//  - Feed de transacciones (toasts): resultado de cada operación y motivos de
//    rechazo decodificados (ReasonCodes), por RPC discreto del host.

using System.Collections.Generic;
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
        private Label _moneyLabel;
        private readonly List<Toast> _toasts = new List<Toast>();
        private VisualElement _toastBox;

        private class Toast { public Label Label; public float Expires; }

        private void Start()
        {
            var doc = GetComponent<UIDocument>();
            if (doc.panelSettings == null)
            {
                var existing = FindFirstObjectByType<UIDocument>();
                if (existing != null && existing != doc) doc.panelSettings = existing.panelSettings;
            }
            if (doc.panelSettings == null) doc.panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            doc.sortingOrder = 10; // por encima del HUD base (crosshair)

            _root = doc.rootVisualElement;
            Build();
            ETSVSessionView.OnFeed += OnFeed;
        }

        private void OnDestroy()
        {
            ETSVSessionView.OnFeed -= OnFeed;
        }

        private void Build()
        {
            // ---- saldo compartido (arriba a la derecha) ----
            _moneyLabel = MakeLabel(20, FontStyle.Bold, new Color(0.55f, 0.95f, 0.6f));
            _moneyLabel.style.position = Position.Absolute;
            _moneyLabel.style.right = 18;
            _moneyLabel.style.top = 14;
            _root.Add(_moneyLabel);

            // ---- toasts (izquierda, arriba) ----
            _toastBox = new VisualElement();
            _toastBox.style.position = Position.Absolute;
            _toastBox.style.left = 18;
            _toastBox.style.top = 14;
            _root.Add(_toastBox);

            // ---- panel inferior de objetivo ----
            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.left = Length.Percent(50);
            _panel.style.bottom = 72;
            _panel.style.translate = new Translate(Length.Percent(-50), Length.Pixel(0));
            _panel.style.width = 430;
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

        // ---------------------------------------------------------------- feed de transacciones
        private void OnFeed(ETSVSessionView.FeedEntry f)
        {
            string who = "J" + f.PlayerId;
            string text;
            if (!f.Ok)
            {
                text = "✖ " + who + ": " + ReasonText(ReasonCodes.Decode(f.ReasonCode));
            }
            else
            {
                switch (f.Kind)
                {
                    case ETSVSessionView.TxKind.Venta: text = "✔ " + who + " vendió +$" + f.Value; break;
                    case ETSVSessionView.TxKind.Procesado: text = "✔ " + who + " procesó un componente"; break;
                    case ETSVSessionView.TxKind.Entrega: text = "✔ " + who + " entregó residuo especial $" + f.Value; break;
                    case ETSVSessionView.TxKind.Almacenado: text = "✔ " + who + " almacenó una pieza"; break;
                    case ETSVSessionView.TxKind.Retirado: text = "✔ " + who + " retiró una pieza"; break;
                    case ETSVSessionView.TxKind.VentaMaterial: text = "✔ " + who + " vendió material +$" + f.Value; break;
                    case ETSVSessionView.TxKind.Guardado: text = "✔ " + who + " guardó la partida"; break;
                    case ETSVSessionView.TxKind.Restaurado: text = "✔ Partida restaurada"; break;
                    default: text = who + " operó una estación"; break;
                }
            }
            AddToast(text, f.Ok);
        }

        private static string ReasonText(string reason)
        {
            switch (reason)
            {
                case "sin_pieza_en_manos": return "no lleva ninguna pieza";
                case "pieza_terminal": return "la pieza ya fue recuperada";
                case "propiedad_ajena": return "la pieza no es de la chatarrería";
                case "pieza_reservada": return "pieza reservada";
                case "no_transportada": return "la pieza no está en sus manos";
                case "pieza_protegida_no_vendible": return "residuo especial: no se vende";
                case "zona_llena": return "zona llena";
                case "categoria_incompatible": return "categoría incompatible con la zona";
                case "fondos_insuficientes": return "fondos insuficientes";
                case "maquina_incompatible_sellado": return "sellado: incompatible con la máquina";
                case "residuo_especial_no_procesable": return "residuo especial no procesable";
                case "contenedor_lleno": return "contenedor de material lleno";
                case "sin_receta": return "la pieza no tiene receta";
                case "no_es_residuo_especial": return "no es un residuo especial";
                case "insuficiente": return "contenedor vacío";
                case "zona_vacia": return "la zona está vacía";
                case "manos_ocupadas": return "manos ocupadas";
                case "requiere_carro_o_grua": return "requiere carro o grúa (P3+)";
                case "distancia_invalida": return "demasiado lejos";
                case "jugador_desconocido": return "jugador no reconocido";
                case "guardado_fallido": return "no se pudo guardar (la última copia válida quedó intacta)";
                case "partida_invalida": return "partida inválida";
                default: return reason ?? "rechazado";
            }
        }

        private void AddToast(string text, bool ok)
        {
            var l = MakeLabel(14, FontStyle.Bold,
                ok ? new Color(0.6f, 0.95f, 0.65f) : new Color(1f, 0.6f, 0.55f));
            l.text = text;
            l.style.backgroundColor = new Color(0.05f, 0.06f, 0.09f, 0.75f);
            l.style.paddingLeft = 8; l.style.paddingRight = 8;
            l.style.paddingTop = 3; l.style.paddingBottom = 3;
            l.style.borderTopLeftRadius = 6; l.style.borderTopRightRadius = 6;
            l.style.borderBottomLeftRadius = 6; l.style.borderBottomRightRadius = 6;
            l.style.marginBottom = 4;
            _toastBox.Add(l);
            _toasts.Add(new Toast { Label = l, Expires = Time.time + 4.5f });
            while (_toasts.Count > 5) RemoveToast(0);
        }

        private void RemoveToast(int i)
        {
            _toastBox.Remove(_toasts[i].Label);
            _toasts.RemoveAt(i);
        }

        // ---------------------------------------------------------------- update
        private void Update()
        {
            // saldo compartido
            var session = ETSVSessionView.Instance;
            string money = session != null ? "$ " + session.Money.Value : "$ —";
            if (_moneyLabel.text != money) _moneyLabel.text = money;

            // toasts vencidos
            for (int i = _toasts.Count - 1; i >= 0; i--)
                if (Time.time >= _toasts[i].Expires) RemoveToast(i);

            var c = DisassemblyPlayerController.Current;
            string toolbar = ToolbarText(c);
            if (_toolbarLabel.text != toolbar) _toolbarLabel.text = toolbar;

            if (c == null || !c.HasTarget)
            {
                if (_panel.style.display != DisplayStyle.None) _panel.style.display = DisplayStyle.None;
                return;
            }

            if (_panel.style.display != DisplayStyle.Flex) _panel.style.display = DisplayStyle.Flex;

            SetText(_objectName, c.ObjectName);
            SetText(_compName, c.CompName + (c.FastenerLabel != null ? " · " + c.FastenerLabel : ""));
            SetText(_stateLabel, c.StateLabel);
            SetText(_qualityLabel, c.CarriedPiece != null || c.FastenerLabel != null
                ? "Calidad " + c.Quality + "%" : null);
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
            sb.Append("    F5 guardar");
            if (c != null && c.CarriedPiece != null)
                sb.Append("    Llevando: ").Append(c.CarriedPiece.DefId)
                  .Append(" (Q").Append(c.CarriedPiece.Quality.Value).Append(")");
            return sb.ToString();
        }
    }
}
