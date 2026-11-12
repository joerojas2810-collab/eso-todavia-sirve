// [ETSV] Integración — escenografía estática de "Recuperaciones El Milagro"
// (Instrucción 10, patio visual). Capa de presentación pura.
//
// REGLAS DURAS (DIRECCION_ARTISTICA.md / MATRIZ_REEMPLAZO_VISUAL.md):
//  - La decoración NO es gameplay: cero colliders (no puede tapar el raycast de
//    interacción ni alterar el movimiento — la física NO se toca), cero
//    NetworkObjects (no viaja por red; todas las peers reconstruyen lo mismo
//    desde constantes deterministas respecto del ancla de la mesa).
//  - No modifica estaciones, mesa, spawn points ni offsets de ScrapyardLayout:
//    se construye ALREDEDOR (perímetro, fondo y detalles).
//  - Materiales del set ETSVArt (URP Lit + PBR CC0); sin los assets degrada a
//    colores planos (headless/tests).
//  - Presupuesto: ~60 primitivos estáticos, todos con materiales compartidos
//    ⇒ SRP Batcher; sin luces reales extra (las lámparas son emisivas).
//
// Sustitución futura: cuando lleguen modelos CC0 definitivos (matriz P4), este
// archivo se reemplaza por prefabs visuales SIN tocar nada más.

using UnityEngine;

namespace Etsv.Integration
{
    public static class ETSVScenery
    {
        private static GameObject _root;

        /// <summary>Construye (una sola vez) toda la escenografía del patio.</summary>
        public static void Build()
        {
            if (_root != null) return;
            Vector3 a = ScrapyardLayout.TableAnchor(); // misma ancla que estaciones/mesa
            _root = new GameObject("ETSV_Scenery"); // sin collider, sin NetworkObject

            BuildGroundPad(a);
            BuildShed(a);
            BuildContainers(a);
            BuildScrapPiles(a);
            BuildLamps(a);
            BuildSignage(a);
            BuildTires(a);
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Deco(PrimitiveType type, string name, Vector3 pos,
            Vector3 scale, float yawDeg, string matName, Color tint)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_root.transform, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            if (yawDeg != 0f) go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            ETSVArt.Apply(go.GetComponent<Renderer>(), ETSVArt.Tinted(matName, tint));
            // DECORACIÓN: sin collider (regla dura — no interfiere con raycasts ni física).
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
            return go;
        }

        private static readonly Color White = new Color(1f, 1f, 1f, 1f);
        private static Color T(float r, float g, float b) { return new Color(r, g, b, 1f); }

        // ---------------------------------------------------------------- suelo de trabajo

        /// <summary>Losa de hormigón bajo la zona de trabajo (las estaciones apoyan
        /// acá visualmente). Elevada 2 cm para no pelear con el terreno del upstream.</summary>
        private static void BuildGroundPad(Vector3 a)
        {
            Deco(PrimitiveType.Cube, "Scenery_Pad_Hormigon",
                a + new Vector3(0f, 0.02f, -0.7f), new Vector3(7.2f, 0.04f, 5.6f), 0f,
                ETSVArt.ConcreteWorn, T(0.85f, 0.83f, 0.78f));
            // Franja de grava delante de la losa (transición al terreno).
            Deco(PrimitiveType.Cube, "Scenery_Grava_Frente",
                a + new Vector3(0f, 0.015f, 2.6f), new Vector3(9f, 0.03f, 1.8f), 0f,
                ETSVArt.GravelYard, White);
        }

        // ---------------------------------------------------------------- galpón del fondo

        private static void BuildShed(Vector3 a)
        {
            Vector3 back = a + new Vector3(0f, 0f, -4.6f);
            // Pared de chapa acanalada oxidada (fondo del patio).
            Deco(PrimitiveType.Cube, "Scenery_Galpon_Pared",
                back + new Vector3(0f, 1.7f, 0f), new Vector3(11f, 3.4f, 0.12f), 0f,
                ETSVArt.CorrugatedRusty, T(0.9f, 0.88f, 0.85f));
            // Techo: losa que cubre la zona de trabajo (sombras largas del sol bajo).
            Deco(PrimitiveType.Cube, "Scenery_Galpon_Techo",
                back + new Vector3(0f, 3.5f, 1.3f), new Vector3(11.4f, 0.12f, 3.2f), 0f,
                ETSVArt.CorrugatedRusty, T(0.75f, 0.73f, 0.7f));
            // 4 postes de acero que sostienen el techo.
            float[] xs = { -5.2f, -1.8f, 1.8f, 5.2f };
            for (int i = 0; i < xs.Length; i++)
                Deco(PrimitiveType.Cube, "Scenery_Galpon_Poste" + i,
                    back + new Vector3(xs[i], 1.75f, 2.7f), new Vector3(0.14f, 3.5f, 0.14f), 0f,
                    ETSVArt.SteelRusty, White);
            // Viga cruzada sobre la mesa (de ahí cuelgan las lámparas).
            Deco(PrimitiveType.Cube, "Scenery_Galpon_Viga",
                a + new Vector3(0f, 3.05f, -0.4f), new Vector3(7.4f, 0.1f, 0.1f), 0f,
                ETSVArt.SteelRusty, T(0.8f, 0.8f, 0.82f));
        }

        // ---------------------------------------------------------------- contenedores apilados

        private static void BuildContainers(Vector3 a)
        {
            // Izquierda: dos contenedores apilados (azul verdoso / verde).
            Deco(PrimitiveType.Cube, "Scenery_Container_Izq_A",
                a + new Vector3(-6.4f, 1.25f, -1.2f), new Vector3(2.4f, 2.5f, 6.0f), 0f,
                ETSVArt.MetalPainted, T(0.35f, 0.55f, 0.58f));
            Deco(PrimitiveType.Cube, "Scenery_Container_Izq_B",
                a + new Vector3(-6.4f, 3.75f, -1.2f), new Vector3(2.4f, 2.5f, 6.0f), 0f,
                ETSVArt.MetalPainted, T(0.45f, 0.58f, 0.42f));
            // Derecha: uno rojo oxidado, rotado.
            Deco(PrimitiveType.Cube, "Scenery_Container_Der",
                a + new Vector3(6.6f, 1.25f, -0.4f), new Vector3(6.0f, 2.5f, 2.4f), 0f,
                ETSVArt.MetalPainted, T(0.62f, 0.35f, 0.3f));
            // Cerramiento bajo al frente (no tapa la cámara: 1.1 m).
            Deco(PrimitiveType.Cube, "Scenery_Murete_Frente",
                a + new Vector3(0f, 0.55f, 5.4f), new Vector3(9.5f, 1.1f, 0.25f), 0f,
                ETSVArt.CorrugatedRusty, T(0.8f, 0.78f, 0.75f));
        }

        // ---------------------------------------------------------------- chatarra decorativa

        private static void BuildScrapPiles(Vector3 a)
        {
            // Pila 1 (izquierda, junto al contenedor): cajas y cilindros apilados.
            Vector3 p1 = a + new Vector3(-4.2f, 0f, 1.6f);
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_1a", p1 + new Vector3(0f, 0.25f, 0f),
                new Vector3(0.9f, 0.5f, 0.7f), 12f, ETSVArt.SteelRusty, White);
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_1b", p1 + new Vector3(0.1f, 0.65f, -0.05f),
                new Vector3(0.6f, 0.35f, 0.5f), -20f, ETSVArt.MetalPainted, T(0.6f, 0.55f, 0.5f));
            Deco(PrimitiveType.Cylinder, "Scenery_Chatarra_1c", p1 + new Vector3(0.7f, 0.3f, 0.4f),
                new Vector3(0.4f, 0.3f, 0.4f), 0f, ETSVArt.SteelRusty, T(0.7f, 0.6f, 0.55f));
            // Pila 2 (derecha): electrodoméstico viejo (cubo + puerta caída).
            Vector3 p2 = a + new Vector3(3.9f, 0f, 2.1f);
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_2a", p2 + new Vector3(0f, 0.45f, 0f),
                new Vector3(0.7f, 0.9f, 0.6f), 8f, ETSVArt.MetalPainted, T(0.75f, 0.72f, 0.68f));
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_2b", p2 + new Vector3(0.5f, 0.08f, 0.35f),
                new Vector3(0.6f, 0.04f, 0.5f), 35f, ETSVArt.PlasticGrey, T(0.8f, 0.8f, 0.8f));
            // Pila 3 (fondo derecha): vigas cruzadas.
            Vector3 p3 = a + new Vector3(4.4f, 0f, -3.1f);
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_3a", p3 + new Vector3(0f, 0.12f, 0f),
                new Vector3(1.6f, 0.14f, 0.14f), 15f, ETSVArt.SteelRusty, White);
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_3b", p3 + new Vector3(0.1f, 0.3f, 0.1f),
                new Vector3(1.4f, 0.14f, 0.14f), -25f, ETSVArt.SteelRusty, T(0.85f, 0.8f, 0.75f));
            Deco(PrimitiveType.Cylinder, "Scenery_Barril", p3 + new Vector3(-0.9f, 0.45f, 0.8f),
                new Vector3(0.55f, 0.45f, 0.55f), 0f, ETSVArt.MetalPainted, T(0.5f, 0.42f, 0.55f));
        }

        private static void BuildTires(Vector3 a)
        {
            // Neumáticos apilados junto a la pila 1 (cilindros achatados de caucho).
            Vector3 t = a + new Vector3(-3.1f, 0f, 2.4f);
            for (int i = 0; i < 3; i++)
                Deco(PrimitiveType.Cylinder, "Scenery_Neumatico" + i,
                    t + new Vector3(i * 0.03f, 0.16f + i * 0.28f, -i * 0.02f),
                    new Vector3(0.85f, 0.14f, 0.85f), i * 17f, ETSVArt.RubberBlack, White);
        }

        // ---------------------------------------------------------------- iluminación práctica

        /// <summary>3 lámparas colgantes de galpón: soporte oscuro + globo EMISIVO
        /// (M_LampGlow). Sin componentes Light ⇒ cero coste de sombras adicionales;
        /// la luz "real" sigue siendo el sol + ambiental (decisión de rendimiento §6
        /// de la biblia: 7 jugadores).</summary>
        private static void BuildLamps(Vector3 a)
        {
            Vector3[] at =
            {
                a + new Vector3(0f, 0f, -0.4f),     // sobre la mesa
                a + new Vector3(-1.7f, 0f, 0.2f),   // sobre la balanza
                a + new Vector3(1.7f, 0f, 0.2f),    // sobre la trituradora
            };
            for (int i = 0; i < at.Length; i++)
            {
                Deco(PrimitiveType.Cylinder, "Scenery_Lampara_Cable" + i,
                    at[i] + new Vector3(0f, 2.98f, 0f), new Vector3(0.02f, 0.08f, 0.02f), 0f,
                    ETSVArt.RubberBlack, White);
                Deco(PrimitiveType.Cylinder, "Scenery_Lampara_Pantalla" + i,
                    at[i] + new Vector3(0f, 2.88f, 0f), new Vector3(0.34f, 0.07f, 0.34f), 0f,
                    ETSVArt.SteelRusty, T(0.6f, 0.6f, 0.62f));
                Deco(PrimitiveType.Sphere, "Scenery_Lampara_Globo" + i,
                    at[i] + new Vector3(0f, 2.80f, 0f), new Vector3(0.16f, 0.14f, 0.16f), 0f,
                    ETSVArt.LampGlow, White);
            }
        }

        // ---------------------------------------------------------------- señalización

        private static void BuildSignage(Vector3 a)
        {
            // Cartel principal sobre la pared del galpón.
            Vector3 wall = a + new Vector3(0f, 0f, -4.5f);
            Deco(PrimitiveType.Cube, "Scenery_Cartel_Tablero",
                wall + new Vector3(0f, 2.7f, 0.02f), new Vector3(3.6f, 0.7f, 0.06f), 0f,
                ETSVArt.MetalPainted, T(0.95f, 0.75f, 0.3f)); // ámbar de la marca
            AddSign(wall + new Vector3(0f, 2.7f, 0.08f), "RECUPERACIONES EL MILAGRO", 0.16f, T(0.15f, 0.13f, 0.1f));
            // Placa chica "ZONA DE DESMONTAJE" sobre el murete del frente (cara interna).
            Deco(PrimitiveType.Cube, "Scenery_Cartel_Zona",
                a + new Vector3(-2.6f, 1.15f, 5.35f), new Vector3(1.5f, 0.35f, 0.05f), 180f,
                ETSVArt.SignGreen, White);
            AddSign(a + new Vector3(-2.6f, 1.15f, 5.30f), "ZONA DE DESMONTAJE", 0.09f, T(0.1f, 0.2f, 0.12f));
            // Franja de peligro en el contenedor derecho.
            Deco(PrimitiveType.Cube, "Scenery_Franja_Peligro",
                a + new Vector3(6.6f, 2.35f, -0.4f), new Vector3(6.05f, 0.22f, 2.45f), 0f,
                ETSVArt.SignAmber, White);
        }

        private static void AddSign(Vector3 pos, string text, float charSize, Color color)
        {
            var go = new GameObject("Scenery_Texto");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.characterSize = charSize;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
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
}
