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
            Vector3 localPos, Vector3 localScale, Color color, string libMat = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            // Instrucción 10: material artístico del set (URP Lit + PBR CC0) teñido con
            // el color del dominio; sin el asset disponible, fallback plano exacto de
            // antes ⇒ nada depende del arte. Sin collider propio: la interacción la
            // resuelve SIEMPRE el BoxCollider de la raíz (los ids y el gating no cambian).
            var rend = go.GetComponent<Renderer>();
            if (libMat != null) ETSVArt.Apply(rend, ETSVArt.Tinted(libMat, color));
            else rend.sharedMaterial = BlueprintBuilder.MakeMaterial(color);
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            return go;
        }

        /// <summary>Coloca un modelo CC0 como hijo VISUAL de la estación (sin collider:
        /// la interacción sigue siendo el BoxCollider de la raíz). false ⇒ usar el
        /// primitivo de fallback. Instrucción 11.</summary>
        private static bool ModelPart(GameObject root, string pack, string model, string name,
            Vector3 localPos, float scale, float yawDeg, string matName, Color tint)
        {
            return ETSVModels.TryCreate(pack, model, name, root.transform, localPos,
                scale, yawDeg, ETSVArt.Tinted(matName, tint)) != null;
        }

        private static void BuildScale(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.1f, 0.6f), new Color(0.35f, 0.4f, 0.48f), ETSVArt.SteelRusty);
            Part(root, PrimitiveType.Cube, "Plataforma", new Vector3(0f, 0.13f, 0f), new Vector3(0.55f, 0.06f, 0.55f), new Color(0.72f, 0.75f, 0.8f), ETSVArt.MetalPainted);
            // Visor: terminal real (Prop_Computer, 0.74×1.59×0.56 m a ×0.42 ≈ 0.31×0.67×0.24).
            if (!ModelPart(root, ETSVModels.Quaternius, "Prop_Computer", "Visor",
                    new Vector3(0f, 0.16f, -0.3f), 0.42f, 0f, ETSVArt.PlasticGrey, new Color(0.45f, 0.75f, 0.55f)))
            {
                Part(root, PrimitiveType.Cylinder, "Poste", new Vector3(0f, 0.32f, -0.25f), new Vector3(0.05f, 0.2f, 0.05f), new Color(0.4f, 0.42f, 0.45f), ETSVArt.SteelRusty);
                Part(root, PrimitiveType.Cube, "Visor", new Vector3(0f, 0.5f, -0.25f), new Vector3(0.3f, 0.25f, 0.06f), new Color(0.1f, 0.35f, 0.2f), ETSVArt.PlasticGrey);
                Part(root, PrimitiveType.Cube, "VisorMarco", new Vector3(0f, 0.5f, -0.27f), new Vector3(0.34f, 0.29f, 0.03f), new Color(0.3f, 0.32f, 0.35f), ETSVArt.MetalPainted);
            }
        }

        private static void BuildShredder(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Cuerpo", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 0.7f), new Color(0.45f, 0.2f, 0.18f), ETSVArt.MetalPainted);
            Part(root, PrimitiveType.Cube, "Bancada", new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.1f, 0.8f), new Color(0.3f, 0.31f, 0.34f), ETSVArt.SteelRusty);
            Part(root, PrimitiveType.Cylinder, "Tolva", new Vector3(0f, 1.0f, 0f), new Vector3(0.5f, 0.15f, 0.5f), new Color(0.3f, 0.32f, 0.36f), ETSVArt.SteelRusty);
            Part(root, PrimitiveType.Cube, "Salida", new Vector3(0f, 0.15f, 0.4f), new Vector3(0.35f, 0.3f, 0.15f), new Color(0.25f, 0.26f, 0.3f), ETSVArt.RubberBlack);
            Part(root, PrimitiveType.Cube, "FranjaPeligro", new Vector3(0f, 0.78f, 0.353f), new Vector3(0.81f, 0.1f, 0.01f), new Color(0.95f, 0.72f, 0.24f), ETSVArt.SignAmber);
            // Extractor plano sobre la tolva (Prop_Fan_Small 1.68 m de diámetro a ×0.34 ≈ 0.57 m)
            // y terminal de control al lado. Modelos MegaKit 1:1, pivotes en la base.
            ModelPart(root, ETSVModels.Quaternius, "Prop_Fan_Small", "Extractor",
                new Vector3(0f, 1.12f, 0f), 0.34f, 0f, ETSVArt.SteelRusty, new Color(0.8f, 0.8f, 0.82f));
            ModelPart(root, ETSVModels.Quaternius, "Prop_Computer", "Terminal",
                new Vector3(0.62f, 0f, -0.15f), 0.4f, -20f, ETSVArt.PlasticGrey, new Color(0.6f, 0.65f, 0.7f));
        }

        private static void BuildHazardBin(GameObject root)
        {
            // Tambor real (Prop_Barrel_Large 0.51×1.10 m a ×0.85 ≈ 0.43×0.94, pivote base).
            bool modelo = ModelPart(root, ETSVModels.Quaternius, "Prop_Barrel_Large", "Contenedor",
                new Vector3(0f, 0f, 0f), 0.85f, 0f, ETSVArt.MetalPainted, new Color(0.85f, 0.7f, 0.1f));
            if (!modelo)
            {
                Part(root, PrimitiveType.Cube, "Contenedor", new Vector3(0f, 0.35f, 0f), new Vector3(0.55f, 0.7f, 0.55f), new Color(0.85f, 0.7f, 0.1f), ETSVArt.MetalPainted);
                Part(root, PrimitiveType.Cube, "Tapa", new Vector3(0f, 0.73f, 0f), new Vector3(0.6f, 0.06f, 0.6f), new Color(0.35f, 0.3f, 0.1f), ETSVArt.RubberBlack);
            }
            // Franjas alternas de peligro (ámbar/negro) al frente.
            float fz = modelo ? 0.2f : 0.281f;
            float ancho = modelo ? 0.44f : 0.56f;
            for (int i = 0; i < 4; i++)
            {
                float y = 0.18f + i * 0.16f;
                Part(root, PrimitiveType.Cube, "Franja" + i, new Vector3(0f, y, fz),
                    new Vector3(ancho, 0.07f, 0.005f),
                    i % 2 == 0 ? new Color(0.95f, 0.72f, 0.24f) : new Color(0.15f, 0.14f, 0.13f),
                    i % 2 == 0 ? ETSVArt.SignAmber : ETSVArt.RubberBlack);
            }
        }

        private static void BuildRack(GameObject root)
        {
            var steel = new Color(0.8f, 0.45f, 0.15f);
            // Estantería real (Furniture Kit 1:1: bookcaseOpen 0.40×0.88×0.25 m a ×2.1
            // ≈ 0.84×1.85×0.53 — llena el volumen del collider raíz 1×1.6×1).
            bool modelo = ModelPart(root, ETSVModels.KenneyFurniture, "bookcaseOpen", "Estructura",
                new Vector3(0f, 0f, 0f), 2.1f, 0f, ETSVArt.MetalPainted, steel);
            if (!modelo)
            {
                Part(root, PrimitiveType.Cube, "PosteL", new Vector3(-0.45f, 0.9f, 0f), new Vector3(0.06f, 1.8f, 0.45f), steel, ETSVArt.MetalPainted);
                Part(root, PrimitiveType.Cube, "PosteR", new Vector3(0.45f, 0.9f, 0f), new Vector3(0.06f, 1.8f, 0.45f), steel, ETSVArt.MetalPainted);
                Part(root, PrimitiveType.Cube, "Travesanio", new Vector3(0f, 1.78f, 0f), new Vector3(0.95f, 0.05f, 0.06f), steel, ETSVArt.MetalPainted);
                for (int i = 0; i < 3; i++)
                    Part(root, PrimitiveType.Cube, "Estante" + i, new Vector3(0f, 0.35f + i * 0.55f, 0f), new Vector3(0.95f, 0.05f, 0.45f), new Color(0.5f, 0.42f, 0.3f), ETSVArt.WoodPlanks);
            }
            // Cajones guardados (decoración: la capacidad la lleva el dominio).
            ModelPart(root, ETSVModels.KenneyFurniture, "cardboardBoxClosed", "Cajon0",
                new Vector3(-0.15f, 0.48f, 0f), 1.1f, 10f, ETSVArt.WoodPlanks, new Color(0.8f, 0.72f, 0.58f));
            ModelPart(root, ETSVModels.KenneyFurniture, "cardboardBoxClosed", "Cajon1",
                new Vector3(0.18f, 1.03f, 0f), 0.9f, -8f, ETSVArt.WoodPlanks, new Color(0.75f, 0.68f, 0.55f));
            if (!modelo)
                for (int i = 0; i < 2; i++)
                    Part(root, PrimitiveType.Cube, "CajonF" + i, new Vector3(-0.15f + i * 0.3f, 0.48f + i * 0.55f, 0f),
                        new Vector3(0.3f, 0.2f, 0.3f), new Color(0.55f, 0.5f, 0.45f),
                        i == 0 ? ETSVArt.WoodPlanks : ETSVArt.PlasticGrey);
        }

        private static void BuildPallet(GameObject root)
        {
            // Palé procedural con tablones (no se encontró modelo CC0 con descarga
            // directa verificable — pendiente en MODELOS_3D.md; la geometría es
            // reemplazable sin tocar ids ni collider).
            var wood = new Color(0.5f, 0.36f, 0.2f);
            for (int t = 0; t < 5; t++) // tabla superior
                Part(root, PrimitiveType.Cube, "Tabla" + t, new Vector3(0f, 0.13f, -0.4f + t * 0.2f),
                    new Vector3(1.0f, 0.02f, 0.16f), wood, ETSVArt.WoodPlanks);
            for (int l = 0; l < 3; l++) // largueros
                Part(root, PrimitiveType.Cube, "Larguero" + l, new Vector3(-0.4f + l * 0.4f, 0.06f, 0f),
                    new Vector3(0.12f, 0.12f, 1.0f), new Color(0.44f, 0.31f, 0.17f), ETSVArt.WoodPlanks);
            // Segundo palé apilado encima, apenas rotado (visual; el dominio cuenta capacidad).
            for (int t = 0; t < 5; t++)
            {
                var tb = Part(root, PrimitiveType.Cube, "Palé2_Tabla" + t, new Vector3(0f, 0.27f, -0.4f + t * 0.2f),
                    new Vector3(1.0f, 0.02f, 0.16f), new Color(0.46f, 0.33f, 0.19f), ETSVArt.WoodPlanks);
                tb.transform.localRotation = Quaternion.Euler(0f, 7f, 0f);
            }
        }

        private static void BuildFloorZone(GameObject root)
        {
            Part(root, PrimitiveType.Cube, "Zona", new Vector3(0f, 0.01f, 0f), new Vector3(1.3f, 0.02f, 1.3f), new Color(0.3f, 0.33f, 0.38f), ETSVArt.ConcreteWorn);
            // Esquinas pintadas de la zona (marcas de suelo amarillas).
            float[] cx = { -0.6f, 0.6f }; float[] cz = { -0.6f, 0.6f };
            int n = 0;
            foreach (float x in cx) foreach (float z in cz)
                Part(root, PrimitiveType.Cube, "Marca" + (n++), new Vector3(x, 0.025f, z),
                    new Vector3(0.22f, 0.005f, 0.22f), new Color(0.95f, 0.72f, 0.24f), ETSVArt.SignAmber);
        }

        private static void BuildMaterialBin(GameObject root, string matId)
        {
            var m = EconomyData.Materials[matId];
            int hex = m.Color;
            var color = new Color(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
            // Tambor real teñido con el color del dominio (×0.7 ≈ 0.36×0.77 m, pivote base).
            if (!ModelPart(root, ETSVModels.Quaternius, "Prop_Barrel_Large", "Contenedor",
                    new Vector3(0f, 0f, 0f), 0.7f, 0f, ETSVArt.MetalPainted, color))
            {
                Part(root, PrimitiveType.Cylinder, "Contenedor", new Vector3(0f, 0.28f, 0f), new Vector3(0.36f, 0.28f, 0.36f), color, ETSVArt.MetalPainted);
                Part(root, PrimitiveType.Cylinder, "Aro", new Vector3(0f, 0.57f, 0f), new Vector3(0.4f, 0.02f, 0.4f), new Color(0.25f, 0.26f, 0.3f), ETSVArt.SteelRusty);
            }
            // Muestra del material en la cara frontal (mismo color del dominio).
            Part(root, PrimitiveType.Cube, "Muestra", new Vector3(0f, 0.3f, 0.185f), new Vector3(0.14f, 0.14f, 0.01f), color, ETSVArt.MetalPainted);
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
