// [ETSV] Integración — layout determinista de las estaciones de la chatarrería.
// TODAS las peers calculan las mismas posiciones desde el mismo ancla de escena
// ⇒ la geometría es local y estática (no viaja por red) y el host puede validar
// distancias contra StationPos() sin sincronizar transforms (Instrucción 07 §8:
// no replicar lo reconstruible). Presentación sencilla y reemplazable: cuando
// existan modelos definitivos, se sustituyen los primitivos de BuildStations
// SIN tocar reglas, ids de estación ni gating.

using System.Collections.Generic;
using UnityEngine;
using Etsv.Domain;

namespace Etsv.Integration
{
    public static class ScrapyardLayout
    {
        /// <summary>Ancla de la mesa de trabajo (misma que ETSVBootstrap).</summary>
        public static Vector3 TableAnchor()
        {
            var sp = GameObject.Find("Player 2 Spawn");
            if (sp == null) sp = GameObject.Find("Player 1 Spawn");
            Vector3 anchor = sp != null ? sp.transform.position : Vector3.zero;
            return anchor + new Vector3(1.8f, 0f, 1.2f);
        }

        // Offsets locales respecto de la mesa (misma convención en todas las peers).
        private static readonly Vector3[] Offsets =
        {
            new Vector3(-1.7f, 0f,  0.6f),  // 0 balanza
            new Vector3( 1.7f, 0f,  0.6f),  // 1 trituradora
            new Vector3( 1.7f, 0f, -1.0f),  // 2 residuos especiales
            new Vector3(-2.4f, 0f, -0.8f),  // 3 estantería
            new Vector3(-0.9f, 0f, -1.5f),  // 4 palé
            new Vector3( 0.5f, 0f, -1.7f),  // 5 suelo
            // 6..13 contenedores de materiales (fila trasera)
            new Vector3(-2.45f, 0f, -2.8f), new Vector3(-1.75f, 0f, -2.8f),
            new Vector3(-1.05f, 0f, -2.8f), new Vector3(-0.35f, 0f, -2.8f),
            new Vector3( 0.35f, 0f, -2.8f), new Vector3( 1.05f, 0f, -2.8f),
            new Vector3( 1.75f, 0f, -2.8f), new Vector3( 2.45f, 0f, -2.8f),
        };

        public static Vector3 StationPos(int stationId)
        {
            if (stationId < 0 || stationId >= Offsets.Length) return TableAnchor();
            return TableAnchor() + Offsets[stationId];
        }

        // ---------------------------------------------------------------- construcción visual
        public static void BuildStations()
        {
            for (int id = 0; id < StationsCatalog.Stations.Length && id < Offsets.Length; id++)
            {
                var def = StationsCatalog.Stations[id];
                var root = new GameObject("ETSV_Station_" + id + "_" + Sanitize(def.Name));
                root.transform.position = StationPos(id);

                switch (def.Kind)
                {
                    case StationKind.Balanza: BuildScale(root); break;
                    case StationKind.Trituradora: BuildShredder(root); break;
                    case StationKind.ResiduosEspeciales: BuildHazardBin(root); break;
                    case StationKind.Estanteria: BuildRack(root); break;
                    case StationKind.Pale: BuildPallet(root); break;
                    case StationKind.Suelo: BuildFloorZone(root); break;
                    case StationKind.ContenedorMaterial: BuildMaterialBin(root, def.MatId); break;
                }

                // Collider único de interacción en la raíz (los hijos no bloquean raycasts).
                var col = root.AddComponent<BoxCollider>();
                col.size = new Vector3(1.0f, 1.6f, 1.0f);
                col.center = new Vector3(0f, 0.8f, 0f);
                root.AddComponent<StationRef>().Init(id);
                AddLabel(root, def.Name, 1.75f);
            }
        }

        private static string Sanitize(string s)
        {
            var chars = new List<char>();
            foreach (var c in s) chars.Add(char.IsLetterOrDigit(c) ? c : '_');
            return new string(chars.ToArray());
        }

        private static GameObject Part(GameObject parent, PrimitiveType type, string name,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = BlueprintBuilder.MakeMaterial(color);
            // Sin collider propio: la interacción la resuelve el BoxCollider de la raíz.
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            return go;
        }

        private static void BuildScale(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.1f, 0.6f), new Color(0.35f, 0.4f, 0.48f));
            Part(root, PrimitiveType.Cube, "Plataforma", new Vector3(0f, 0.13f, 0f), new Vector3(0.55f, 0.06f, 0.55f), new Color(0.72f, 0.75f, 0.8f));
            Part(root, PrimitiveType.Cube, "Visor", new Vector3(0f, 0.5f, -0.25f), new Vector3(0.3f, 0.25f, 0.06f), new Color(0.1f, 0.35f, 0.2f));
        }

        private static void BuildShredder(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Cuerpo", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 0.7f), new Color(0.45f, 0.2f, 0.18f));
            Part(root, PrimitiveType.Cylinder, "Tolva", new Vector3(0f, 1.0f, 0f), new Vector3(0.5f, 0.15f, 0.5f), new Color(0.3f, 0.32f, 0.36f));
            Part(root, PrimitiveType.Cube, "Salida", new Vector3(0f, 0.15f, 0.4f), new Vector3(0.35f, 0.3f, 0.15f), new Color(0.25f, 0.26f, 0.3f));
        }

        private static void BuildHazardBin(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Contenedor", new Vector3(0f, 0.35f, 0f), new Vector3(0.55f, 0.7f, 0.55f), new Color(0.85f, 0.7f, 0.1f));
            Part(root, PrimitiveType.Cube, "Tapa", new Vector3(0f, 0.73f, 0f), new Vector3(0.6f, 0.06f, 0.6f), new Color(0.35f, 0.3f, 0.1f));
        }

        private static void BuildRack(GameObject root)
        {
            var steel = new Color(0.8f, 0.45f, 0.15f);
            Part(root, PrimitiveType.Cube, "PosteL", new Vector3(-0.45f, 0.9f, 0f), new Vector3(0.06f, 1.8f, 0.45f), steel);
            Part(root, PrimitiveType.Cube, "PosteR", new Vector3(0.45f, 0.9f, 0f), new Vector3(0.06f, 1.8f, 0.45f), steel);
            for (int i = 0; i < 3; i++)
                Part(root, PrimitiveType.Cube, "Estante" + i, new Vector3(0f, 0.35f + i * 0.55f, 0f), new Vector3(0.95f, 0.05f, 0.45f), new Color(0.5f, 0.52f, 0.56f));
        }

        private static void BuildPallet(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Palé", new Vector3(0f, 0.07f, 0f), new Vector3(1.0f, 0.14f, 1.0f), new Color(0.5f, 0.36f, 0.2f));
        }

        private static void BuildFloorZone(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Zona", new Vector3(0f, 0.01f, 0f), new Vector3(1.3f, 0.02f, 1.3f), new Color(0.3f, 0.33f, 0.38f));
        }

        private static void BuildMaterialBin(GameObject root, string matId)
        {
            var m = EconomyData.Materials[matId];
            int hex = m.Color;
            var color = new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
            Part(root, PrimitiveType.Cylinder, "Contenedor", new Vector3(0f, 0.28f, 0f), new Vector3(0.36f, 0.28f, 0.36f), color);
            Part(root, PrimitiveType.Cylinder, "Aro", new Vector3(0f, 0.57f, 0f), new Vector3(0.4f, 0.02f, 0.4f), new Color(0.25f, 0.26f, 0.3f));
        }

        private static void AddLabel(GameObject root, string text, float height)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = 0.09f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            // Fuente built-in: Unity 6 usa LegacyRuntime.ttf; fallback Arial (versiones previas).
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                tm.font = font;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;
            }
        }
    }

    /// <summary>Marca el collider de una estación (id de StationsCatalog).</summary>
    public class StationRef : MonoBehaviour
    {
        public int StationId { get; private set; }
        public void Init(int id) { StationId = id; }
    }
}
