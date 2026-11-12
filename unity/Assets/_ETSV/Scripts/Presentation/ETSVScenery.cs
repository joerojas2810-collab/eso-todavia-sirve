// [ETSV] Integración — escenografía estática de "Recuperaciones El Milagro"
// (Instrucción 10; REDISEÑO Instrucción 12 tras la primera ejecución real).
// Capa de presentación pura.
//
// PLAN DE ZONAS (offsets en METROS desde el ancla A = TableAnchor; +z = frente):
//   frente (+z, z 2..8): ENTRADA y circulación — despejada, solo elementos bajos;
//                        los 3 spawns quedan al norte del patio con ≥3 m libres.
//   A (0,0):             mesa de desmontaje al aire libre.
//   izq/frente (-2.6,1.4): balanza-venta · der/frente (2.7,1.2): trituradora.
//   fondo (-z):          galpón-taller (x ±6.7, z -0.6..-7.2, techo 4.3 m, frente
//                        abierto) con residuos, almacén y la fila de contenedores.
//   laterales (x ±9):    contenedores marítimos (5.8×2.6×2.4 m reales, ×7 City Kit).
//   perímetro (≥15 m):   hitos industriales (galpones lejanos, torre, chimenea,
//                        tanque) — separados entre sí (antes se solapaban).
//
// REGLAS DURAS (DIRECCION_ARTISTICA.md / MATRIZ_REEMPLAZO_VISUAL.md):
//  - La decoración NO es gameplay: cero colliders, cero NetworkObjects.
//  - No modifica estaciones, mesa, spawn points ni lógica: se construye
//    ALREDEDOR, con distancias mínimas verificadas (docs/ESCENARIO_v2.md).
//  - Materiales del set ETSVArt; sin assets degrada a primitivos (headless).
//  - Sin luces reales extra (emisivos M_LampGlow); budget 7 jugadores.

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
            BuildLandmarks(a);
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

        /// <summary>Intenta colocar un modelo CC0 (Instrucción 11). Devuelve true si
        /// lo colocó; false si no existe el recurso ⇒ el llamador usa su primitivo.
        /// Posición ABSOLUTA: _root está en el origen sin rotación ⇒ local == mundo.
        /// Sin collider, sin red.</summary>
        private static bool Model(string pack, string model, string name, Vector3 pos,
            float scale, float yawDeg, string matName, Color tint)
        {
            return ETSVModels.TryCreate(pack, model, name, _root.transform, pos,
                scale, yawDeg, ETSVArt.Tinted(matName, tint)) != null;
        }

        // ---------------------------------------------------------------- suelo de trabajo

        /// <summary>Losa de hormigón bajo mesa+estaciones y explanada de grava en la
        /// entrada (Instrucción 12: la losa antigua 7.2×5.6 quedaba chica para el nuevo
        /// plan de zonas). Elevadas 2 cm para no pelear con el terreno del upstream.</summary>
        private static void BuildGroundPad(Vector3 a)
        {
            Deco(PrimitiveType.Cube, "Scenery_Pad_Hormigon",
                a + new Vector3(0f, 0.02f, -1.6f), new Vector3(13.4f, 0.04f, 10.4f), 0f,
                ETSVArt.ConcreteWorn, T(0.88f, 0.86f, 0.81f));
            // Explanada de grava: entrada y circulación (z 3.4..7.8).
            Deco(PrimitiveType.Cube, "Scenery_Grava_Frente",
                a + new Vector3(0f, 0.015f, 5.6f), new Vector3(15f, 0.03f, 4.4f), 0f,
                ETSVArt.GravelYard, White);
        }

        // ---------------------------------------------------------------- galpón-taller del fondo

        /// <summary>Galpón de chapa con frente abierto hacia el patio (x ±6.7,
        /// z -0.6..-7.2, alero 4.3 m). Dentro: residuos, almacén y fila de
        /// contenedores. Postes MegaKit de 4 m (×1.02) en las esquinas.</summary>
        private static void BuildShed(Vector3 a)
        {
            // Pared del fondo.
            Deco(PrimitiveType.Cube, "Scenery_Galpon_ParedFondo",
                a + new Vector3(0f, 2.1f, -6.9f), new Vector3(13.4f, 4.2f, 0.14f), 0f,
                ETSVArt.CorrugatedRusty, T(0.9f, 0.88f, 0.85f));
            // Paredes laterales.
            Deco(PrimitiveType.Cube, "Scenery_Galpon_ParedIzq",
                a + new Vector3(-6.63f, 2.1f, -3.9f), new Vector3(0.14f, 4.2f, 6.14f), 0f,
                ETSVArt.CorrugatedRusty, T(0.86f, 0.84f, 0.8f));
            Deco(PrimitiveType.Cube, "Scenery_Galpon_ParedDer",
                a + new Vector3(6.63f, 2.1f, -3.9f), new Vector3(0.14f, 4.2f, 6.14f), 0f,
                ETSVArt.CorrugatedRusty, T(0.86f, 0.84f, 0.8f));
            // Techo (ligeramente más claro para leerse desde el patio).
            Deco(PrimitiveType.Cube, "Scenery_Galpon_Techo",
                a + new Vector3(0f, 4.28f, -3.9f), new Vector3(13.8f, 0.14f, 6.6f), 0f,
                ETSVArt.CorrugatedRusty, T(0.78f, 0.76f, 0.72f));
            // Fachaleta frontal (soporte del cartel principal).
            Deco(PrimitiveType.Cube, "Scenery_Galpon_Fachaleta",
                a + new Vector3(0f, 3.85f, -0.86f), new Vector3(13.4f, 0.55f, 0.1f), 0f,
                ETSVArt.MetalPainted, T(0.55f, 0.5f, 0.45f));
            // 4 postes estructurales MegaKit (4 m reales ⇒ ×1.02: remate a 4.28 m,
            // cara inferior exacta del techo — sin perforación).
            Vector3[] postes =
            {
                new Vector3(-6.3f, 0f, -1.0f), new Vector3(6.3f, 0f, -1.0f),
                new Vector3(-6.3f, 0f, -6.75f), new Vector3(6.3f, 0f, -6.75f),
            };
            for (int i = 0; i < postes.Length; i++)
            {
                Vector3 pp = a + postes[i];
                if (!Model(ETSVModels.Quaternius, "Column_MetalSupport", "Scenery_Galpon_Poste" + i,
                        pp, 1.02f, 0f, ETSVArt.SteelRusty, T(0.85f, 0.85f, 0.88f)))
                    Deco(PrimitiveType.Cube, "Scenery_Galpon_Poste" + i,
                        pp + new Vector3(0f, 2.1f, 0f), new Vector3(0.14f, 4.2f, 0.14f), 0f,
                        ETSVArt.SteelRusty, White);
            }
            // Rejilla de ventilación en la pared del fondo (cara al patio).
            Model(ETSVModels.Quaternius, "Prop_Vent_Small", "Scenery_Galpon_Vent",
                a + new Vector3(3.4f, 2.6f, -6.82f), 1f, 0f, ETSVArt.SteelRusty, White);
            // Viga de la que cuelgan las 3 lámparas del taller.
            Deco(PrimitiveType.Cube, "Scenery_Galpon_Viga",
                a + new Vector3(0f, 3.6f, -4.2f), new Vector3(12.6f, 0.1f, 0.1f), 0f,
                ETSVArt.SteelRusty, T(0.8f, 0.8f, 0.82f));
        }

        // ---------------------------------------------------------------- contenedores y cerramiento

        /// <summary>Contenedores Kenney REALES (×7 ⇒ 5.8 largo × 2.6 ancho × 2.4 alto,
        /// pivote en la base; el largo cae sobre +z con yaw 0). Izquierda: pila de dos
        /// en x -9.2 (z -5.1..0.7). Derecha: uno rotado 90° en x 9.2 (x 6.3..12.1) —
        /// ya NO se solapa con el tanque (movido a x 16.5, ver BuildLandmarks).
        /// Murete de entrada en dos tramos dejando portón de 10 m al frente.</summary>
        private static void BuildContainers(Vector3 a)
        {
            const float S = ETSVModels.KenneyCityScale;
            if (!Model(ETSVModels.KenneyCity, "shipping-container-a", "Scenery_Container_Izq_A",
                    a + new Vector3(-9.2f, 0f, -2.2f), S, 0f, ETSVArt.MetalPainted, T(0.45f, 0.65f, 0.68f)))
                Deco(PrimitiveType.Cube, "Scenery_Container_Izq_A",
                    a + new Vector3(-9.2f, 1.2f, -2.2f), new Vector3(2.6f, 2.4f, 5.8f), 0f,
                    ETSVArt.MetalPainted, T(0.35f, 0.55f, 0.58f));
            if (!Model(ETSVModels.KenneyCity, "shipping-container-b", "Scenery_Container_Izq_B",
                    a + new Vector3(-9.2f, 2.42f, -2.2f), S, 0f, ETSVArt.MetalPainted, T(0.55f, 0.68f, 0.5f)))
                Deco(PrimitiveType.Cube, "Scenery_Container_Izq_B",
                    a + new Vector3(-9.2f, 3.62f, -2.2f), new Vector3(2.6f, 2.4f, 5.8f), 0f,
                    ETSVArt.MetalPainted, T(0.45f, 0.58f, 0.42f));
            if (!Model(ETSVModels.KenneyCity, "shipping-container-c", "Scenery_Container_Der",
                    a + new Vector3(9.2f, 0f, 0.8f), S, 90f, ETSVArt.MetalPainted, T(0.72f, 0.45f, 0.38f)))
                Deco(PrimitiveType.Cube, "Scenery_Container_Der",
                    a + new Vector3(9.2f, 1.2f, 0.8f), new Vector3(5.8f, 2.4f, 2.6f), 0f,
                    ETSVArt.MetalPainted, T(0.62f, 0.35f, 0.3f));
            // Placa de señalización en la cara del contenedor derecho que mira al patio.
            Model(ETSVModels.Quaternius, "Decal_Sign", "Scenery_Sign_Container",
                a + new Vector3(6.27f, 1.3f, 0.8f), 1.4f, -90f, ETSVArt.SignAmber, White);
            // Cerramiento bajo de la entrada: dos tramos, portón central despejado
            // (x -5.2..5.2). El murete anterior cruzaba DELANTE del spawn de Player 3.
            Deco(PrimitiveType.Cube, "Scenery_Murete_Izq",
                a + new Vector3(-7.25f, 0.55f, 6.6f), new Vector3(4.1f, 1.1f, 0.25f), 0f,
                ETSVArt.CorrugatedRusty, T(0.8f, 0.78f, 0.75f));
            Deco(PrimitiveType.Cube, "Scenery_Murete_Der",
                a + new Vector3(7.25f, 0.55f, 6.6f), new Vector3(4.1f, 1.1f, 0.25f), 0f,
                ETSVArt.CorrugatedRusty, T(0.8f, 0.78f, 0.75f));
        }

        // ---------------------------------------------------------------- hitos del perímetro

        /// <summary>Decoración industrial LEJANA (City Kit ×7 = escala real: edificios
        /// de 9-15 m). Instrucción 12: alejados ≥15 m del patio y separados ENTRE SÍ
        /// (footprints verificados en docs/ESCENARIO_v2.md) — antes rodeaban el patio
        /// a 13 m y el tanque se solapaba con el contenedor derecho.</summary>
        private static void BuildLandmarks(Vector3 a)
        {
            const float S = ETSVModels.KenneyCityScale;
            Model(ETSVModels.KenneyCity, "building-c", "Scenery_Galpon_Lejano_A",
                a + new Vector3(-15f, 0f, -17f), S, 8f, ETSVArt.CorrugatedRusty, T(0.85f, 0.83f, 0.8f));
            Model(ETSVModels.KenneyCity, "water-tower", "Scenery_Torre_Agua",
                a + new Vector3(-24f, 0f, -5f), S, 0f, ETSVArt.SteelRusty, T(0.9f, 0.88f, 0.85f));
            Model(ETSVModels.KenneyCity, "building-m", "Scenery_Galpon_Lejano_B",
                a + new Vector3(14f, 0f, -20f), S, -12f, ETSVArt.MetalPainted, T(0.8f, 0.78f, 0.74f));
            Model(ETSVModels.KenneyCity, "detail-tank-large", "Scenery_Tanque",
                a + new Vector3(16.5f, 0f, -7.5f), S, -20f, ETSVArt.MetalPainted, T(0.75f, 0.72f, 0.68f));
            Model(ETSVModels.KenneyCity, "chimney-medium", "Scenery_Chimenea",
                a + new Vector3(24f, 0f, -4f), S, 0f, ETSVArt.CorrugatedRusty, T(0.85f, 0.8f, 0.75f));
        }

        // ---------------------------------------------------------------- chatarra decorativa

        private static void BuildScrapPiles(Vector3 a)
        {
            // Pila 1 (izquierda, junto a la entrada): cajones MegaKit + barril + caja.
            Vector3 p1 = a + new Vector3(-5.6f, 0f, 2.2f);
            if (!Model(ETSVModels.Quaternius, "Prop_Crate3", "Scenery_Chatarra_1a",
                    p1 + new Vector3(0f, 0f, 0f), 0.8f, 12f, ETSVArt.WoodPlanks, T(0.9f, 0.85f, 0.8f)))
                Deco(PrimitiveType.Cube, "Scenery_Chatarra_1a", p1 + new Vector3(0f, 0.25f, 0f),
                    new Vector3(0.9f, 0.5f, 0.7f), 12f, ETSVArt.SteelRusty, White);
            if (!Model(ETSVModels.Quaternius, "Prop_Crate4", "Scenery_Chatarra_1b",
                    p1 + new Vector3(0.15f, 0.75f, -0.05f), 0.5f, -20f, ETSVArt.MetalPainted, T(0.7f, 0.65f, 0.6f)))
                Deco(PrimitiveType.Cube, "Scenery_Chatarra_1b", p1 + new Vector3(0.1f, 0.65f, -0.05f),
                    new Vector3(0.6f, 0.35f, 0.5f), -20f, ETSVArt.MetalPainted, T(0.6f, 0.55f, 0.5f));
            if (!Model(ETSVModels.Quaternius, "Prop_Barrel_Large", "Scenery_Chatarra_1c",
                    p1 + new Vector3(0.85f, 0f, 0.45f), 0.8f, 0f, ETSVArt.MetalPainted, T(0.55f, 0.5f, 0.6f)))
                Deco(PrimitiveType.Cylinder, "Scenery_Chatarra_1c", p1 + new Vector3(0.7f, 0.3f, 0.4f),
                    new Vector3(0.4f, 0.3f, 0.4f), 0f, ETSVArt.SteelRusty, T(0.7f, 0.6f, 0.55f));
            Model(ETSVModels.KenneyFurniture, "cardboardBoxClosed", "Scenery_Caja_1",
                p1 + new Vector3(-0.7f, 0f, 0.5f), 1.3f, 25f, ETSVArt.WoodPlanks, T(0.85f, 0.75f, 0.6f));

            // Pila 2 (derecha, junto a la entrada): electrodomésticos viejos REALES.
            // (El microondas JUGABLE sigue siendo el sistema modular de 6 componentes.)
            Vector3 p2 = a + new Vector3(5.4f, 0f, 3.4f);
            if (!Model(ETSVModels.KenneyFurniture, "kitchenMicrowave", "Scenery_Chatarra_2a",
                    p2 + new Vector3(0f, 0.55f, 0f), 1.7f, 8f, ETSVArt.MetalPainted, T(0.8f, 0.78f, 0.74f)))
                Deco(PrimitiveType.Cube, "Scenery_Chatarra_2a", p2 + new Vector3(0f, 0.45f, 0f),
                    new Vector3(0.7f, 0.9f, 0.6f), 8f, ETSVArt.MetalPainted, T(0.75f, 0.72f, 0.68f));
            if (!Model(ETSVModels.KenneyFurniture, "televisionVintage", "Scenery_Chatarra_2b",
                    p2 + new Vector3(0.65f, 0f, 0.3f), 1.4f, 35f, ETSVArt.PlasticGrey, T(0.75f, 0.72f, 0.65f)))
                Deco(PrimitiveType.Cube, "Scenery_Chatarra_2b", p2 + new Vector3(0.5f, 0.08f, 0.35f),
                    new Vector3(0.6f, 0.04f, 0.5f), 35f, ETSVArt.PlasticGrey, T(0.8f, 0.8f, 0.8f));
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_2c", p2 + new Vector3(0f, 0.25f, 0f),
                new Vector3(0.8f, 0.5f, 0.65f), 4f, ETSVArt.WoodPlanks, T(0.7f, 0.6f, 0.45f));
            Model(ETSVModels.KenneyFurniture, "cardboardBoxOpen", "Scenery_Caja_2",
                p2 + new Vector3(-0.6f, 0f, 0.55f), 1.2f, -15f, ETSVArt.WoodPlanks, T(0.8f, 0.7f, 0.55f));

            // Pila 3 (borde der del galpón): vigas cruzadas + barril + mordaza.
            Vector3 p3 = a + new Vector3(4.8f, 0f, -0.4f);
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_3a", p3 + new Vector3(0f, 0.12f, 0f),
                new Vector3(1.6f, 0.14f, 0.14f), 15f, ETSVArt.SteelRusty, White);
            Deco(PrimitiveType.Cube, "Scenery_Chatarra_3b", p3 + new Vector3(0.1f, 0.3f, 0.1f),
                new Vector3(1.4f, 0.14f, 0.14f), -25f, ETSVArt.SteelRusty, T(0.85f, 0.8f, 0.75f));
            if (!Model(ETSVModels.Quaternius, "Prop_Barrel_Large", "Scenery_Barril",
                    p3 + new Vector3(-0.9f, 0f, 0.8f), 1f, 0f, ETSVArt.MetalPainted, T(0.5f, 0.42f, 0.55f)))
                Deco(PrimitiveType.Cylinder, "Scenery_Barril", p3 + new Vector3(-0.9f, 0.45f, 0.8f),
                    new Vector3(0.55f, 0.45f, 0.55f), 0f, ETSVArt.MetalPainted, T(0.5f, 0.42f, 0.55f));
            Model(ETSVModels.Quaternius, "Prop_Clamp", "Scenery_Mordaza",
                p3 + new Vector3(0.9f, 0.45f, -0.5f), 1f, 40f, ETSVArt.SteelRusty, White);
        }

        private static void BuildTires(Vector3 a)
        {
            // Neumáticos apilados en el borde izq de la entrada.
            Vector3 t = a + new Vector3(-4.3f, 0f, 4.45f);
            for (int i = 0; i < 3; i++)
                Deco(PrimitiveType.Cylinder, "Scenery_Neumatico" + i,
                    t + new Vector3(i * 0.03f, 0.16f + i * 0.28f, -i * 0.02f),
                    new Vector3(0.85f, 0.14f, 0.85f), i * 17f, ETSVArt.RubberBlack, White);
        }

        // ---------------------------------------------------------------- iluminación práctica

        /// <summary>Instrucción 12: 3 lámparas bajo la viga del galpón (y 3.6) + 2
        /// farolas exteriores de 4.4 m flanqueando la entrada. Todo emisivo
        /// (M_LampGlow), sin componentes Light ⇒ rendimiento intacto.</summary>
        private static void BuildLamps(Vector3 a)
        {
            // Lámparas colgantes del taller (bajo la viga z -4.2).
            float[] xs = { -3.2f, 0f, 3.2f };
            for (int i = 0; i < xs.Length; i++)
            {
                Vector3 at = a + new Vector3(xs[i], 0f, -4.2f);
                Deco(PrimitiveType.Cylinder, "Scenery_Lampara_Cable" + i,
                    at + new Vector3(0f, 3.4f, 0f), new Vector3(0.02f, 0.1f, 0.02f), 0f,
                    ETSVArt.RubberBlack, White);
                if (!Model(ETSVModels.KenneyFurniture, "lampSquareCeiling", "Scenery_Lampara_Artefacto" + i,
                        at + new Vector3(0f, 3.35f, 0f), 1.6f, 0f, ETSVArt.SteelRusty, T(0.55f, 0.55f, 0.58f)))
                    Deco(PrimitiveType.Cylinder, "Scenery_Lampara_Pantalla" + i,
                        at + new Vector3(0f, 3.25f, 0f), new Vector3(0.34f, 0.07f, 0.34f), 0f,
                        ETSVArt.SteelRusty, T(0.6f, 0.6f, 0.62f));
                Deco(PrimitiveType.Sphere, "Scenery_Lampara_Globo" + i,
                    at + new Vector3(0f, 2.95f, 0f), new Vector3(0.16f, 0.14f, 0.16f), 0f,
                    ETSVArt.LampGlow, White);
            }
            // Farolas de la entrada (poste de 4.4 m + cabezal emisivo).
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = a + new Vector3(i == 0 ? -5.8f : 5.8f, 0f, 4.6f);
                Deco(PrimitiveType.Cylinder, "Scenery_Farola_Poste" + i,
                    at + new Vector3(0f, 2.2f, 0f), new Vector3(0.09f, 2.2f, 0.09f), 0f,
                    ETSVArt.SteelRusty, T(0.7f, 0.7f, 0.72f));
                Deco(PrimitiveType.Cylinder, "Scenery_Farola_Pantalla" + i,
                    at + new Vector3(0f, 4.38f, 0f), new Vector3(0.4f, 0.06f, 0.4f), 0f,
                    ETSVArt.SteelRusty, T(0.6f, 0.6f, 0.62f));
                Deco(PrimitiveType.Sphere, "Scenery_Farola_Globo" + i,
                    at + new Vector3(0f, 4.28f, 0f), new Vector3(0.2f, 0.16f, 0.2f), 0f,
                    ETSVArt.LampGlow, White);
            }
        }

        // ---------------------------------------------------------------- señalización

        private static void BuildSignage(Vector3 a)
        {
            // Cartel principal sobre la fachaleta del galpón (legible desde el patio).
            Deco(PrimitiveType.Cube, "Scenery_Cartel_Tablero",
                a + new Vector3(0f, 3.85f, -0.80f), new Vector3(4.6f, 0.42f, 0.06f), 0f,
                ETSVArt.MetalPainted, T(0.95f, 0.75f, 0.3f)); // ámbar de la marca
            AddSign(a + new Vector3(0f, 3.85f, -0.755f), "RECUPERACIONES EL MILAGRO", 0.16f,
                T(0.15f, 0.13f, 0.1f), 0f);
            // Placa "ZONA DE DESMONTAJE" en la cara interna del murete izq (mira al patio).
            Deco(PrimitiveType.Cube, "Scenery_Cartel_Zona",
                a + new Vector3(-7.25f, 1.15f, 6.46f), new Vector3(1.7f, 0.35f, 0.05f), 0f,
                ETSVArt.SignGreen, White);
            AddSign(a + new Vector3(-7.25f, 1.15f, 6.42f), "ZONA DE DESMONTAJE", 0.09f,
                T(0.1f, 0.2f, 0.12f), 180f);
            // Placa "ENTRADA" en el murete der.
            Deco(PrimitiveType.Cube, "Scenery_Cartel_Entrada",
                a + new Vector3(7.25f, 1.15f, 6.46f), new Vector3(1.2f, 0.35f, 0.05f), 0f,
                ETSVArt.SignGreen, White);
            AddSign(a + new Vector3(7.25f, 1.15f, 6.42f), "ENTRADA", 0.1f,
                T(0.1f, 0.2f, 0.12f), 180f);
            // Franja de peligro en la cara del contenedor derecho que mira al patio.
            Deco(PrimitiveType.Cube, "Scenery_Franja_Peligro",
                a + new Vector3(6.29f, 2.2f, 0.8f), new Vector3(0.05f, 0.25f, 2.45f), 0f,
                ETSVArt.SignAmber, White);
        }

        private static void AddSign(Vector3 pos, string text, float charSize, Color color,
            float yawDeg = 0f)
        {
            var go = new GameObject("Scenery_Texto");
            go.transform.SetParent(_root.transform, false);
            go.transform.position = pos;
            if (yawDeg != 0f) go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
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
