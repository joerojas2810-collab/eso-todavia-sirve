// [ETSV] Integración — biblioteca de materiales artísticos (Instrucción 10).
// Capa de presentación pura: NO toca dominio, física ni red.
//
// Los materiales viven en Assets/_ETSV/Resources/ETSV/Materials/*.mat (URP Lit,
// texturas PBR CC0 de Poly Haven — ver Art/Textures/*/_SOURCE.md). Se cargan
// por Resources.Load con caché; si el asset no existe (p. ej. banco de tests
// headless, o un build sin los recursos), TODO degrada gracefully al material
// plano de color de BlueprintBuilder.MakeMaterial ⇒ la jugabilidad nunca
// depende del arte.
//
// Reglas (DIRECCION_ARTISTICA.md):
//  - Materiales COMPARTIDOS (SRP Batcher): una instancia por (nombre, tinte).
//  - Los tintes preservan la codificación de color del dominio (materiales,
//    acentos de estado) multiplicando la textura.
//  - La decoración creada con estos materiales NUNCA lleva collider ni
//    NetworkObject (ver ETSVScenery).

using System.Collections.Generic;
using UnityEngine;

namespace Etsv.Integration
{
    public static class ETSVArt
    {
        // Nombres de los materiales del set (Resources/ETSV/Materials/).
        public const string SteelRusty      = "M_SteelRusty";       // acero oxidado (estructuras, chatarra)
        public const string MetalPainted    = "M_MetalPainted";     // metal pintado (contenedores, máquinas)
        public const string CorrugatedRusty = "M_CorrugatedRusty";  // chapa acanalada oxidada (paredes)
        public const string ConcreteWorn    = "M_ConcreteWorn";     // hormigón desgastado (losas)
        public const string WoodPlanks      = "M_WoodPlanks";       // madera (palés, mesa)
        public const string GravelYard      = "M_GravelYard";       // grava/tierra (suelo del patio)
        public const string PlasticGrey     = "M_PlasticGrey";      // plástico (procedural, sin pack CC0 aún)
        public const string RubberBlack     = "M_RubberBlack";      // caucho (procedural)
        public const string GlassClear      = "M_GlassClear";       // vidrio (transparente procedural)
        public const string SignAmber       = "M_SignAmber";        // acento ámbar (señalización/peligro)
        public const string SignGreen       = "M_SignGreen";        // acento verde (operable)
        public const string LampGlow        = "M_LampGlow";         // lámpara emisiva cálida

        private const string Folder = "ETSV/Materials/";

        private static Dictionary<string, Material> _shared;   // nombre → material del set
        private static Dictionary<string, Material> _tinted;   // nombre+tinte → clon
        private static bool _probed;                            // ¿existen los assets en el build?

        /// <summary>Material compartido del set, o null si el recurso no existe.</summary>
        public static Material Get(string name)
        {
            if (name == null) return null;
            if (_shared == null) _shared = new Dictionary<string, Material>();
            Material m;
            if (_shared.TryGetValue(name, out m)) return m;
            m = Resources.Load<Material>(Folder + name);
            _shared[name] = m; // null también se cachea (no reintentar cada frame)
            return m;
        }

        /// <summary>
        /// Material del set teñido (clon por combinación nombre+tinte). Si el set no
        /// está disponible (headless/tests), devuelve el material plano equivalente:
        /// el color del dominio manda y nada se rompe.
        /// </summary>
        public static Material Tinted(string name, Color tint)
        {
            var src = Get(name);
            if (src == null) return BlueprintBuilder.MakeMaterial(tint); // fallback plano
            bool plain = tint.r >= 0.999f && tint.g >= 0.999f && tint.b >= 0.999f && tint.a >= 0.999f;
            if (plain) return src; // sin tinte ⇒ instancia compartida (SRP Batcher)

            if (_tinted == null) _tinted = new Dictionary<string, Material>();
            string key = name + "#" + tint.r.ToString("F3") + tint.g.ToString("F3")
                       + tint.b.ToString("F3") + tint.a.ToString("F3");
            Material m;
            if (_tinted.TryGetValue(key, out m)) return m;
            m = new Material(src);
            m.color = tint;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            _tinted[key] = m;
            return m;
        }

        /// <summary>Asigna material a un renderer con guardas (presentación nunca crashea).</summary>
        public static void Apply(Renderer rend, Material m)
        {
            if (rend != null && m != null) rend.sharedMaterial = m;
        }

        /// <summary>
        /// Material artístico para un COMPONENTE de objeto desmontable (por id del
        /// catálogo). Data-driven: los objetos 02–30 heredan el mapeo por palabras
        /// clave sin tocar este archivo caso por caso. null = mantener color plano.
        /// </summary>
        public static string ComponentMaterialFor(Domain.ComponentDef def)
        {
            if (def == null) return null;
            if (def.Sealed) return SignAmber;                 // sellado: ámbar de precaución (GDD)
            string id = def.Id ?? "";
            if (id.Contains("motor")) return SteelRusty;      // motores: acero oxidado/cobre
            if (id.Contains("puerta") || id.Contains("ventana")) return PlasticGrey;
            if (id.Contains("plato") || id.Contains("vidrio") || id.Contains("cristal")) return GlassClear;
            if (id.Contains("panel") || id.Contains("placa") || id.Contains("electron")) return PlasticGrey;
            if (id.Contains("carcasa") || id.Contains("chapa") || id.Contains("tapa")) return MetalPainted;
            if (id.Contains("rueda") || id.Contains("goma") || id.Contains("correa")) return RubberBlack;
            if (id.Contains("madera") || id.Contains("manija") || id.Contains("mango")) return WoodPlanks;
            return null; // sin mapeo: color plano del layout (regla §9 biblia)
        }

        /// <summary>Material artístico para una PIEZA suelta (por defId).</summary>
        public static string PieceMaterialFor(string defId)
        {
            if (defId == null) return null;
            if (defId.Contains("motor")) return SteelRusty;
            if (defId.Contains("plato") || defId.Contains("vidrio") || defId.Contains("cristal")) return GlassClear;
            if (defId.Contains("panel") || defId.Contains("placa") || defId.Contains("electron")) return PlasticGrey;
            if (defId.Contains("puerta") || defId.Contains("ventana")) return PlasticGrey;
            if (defId.Contains("carcasa") || defId.Contains("chapa") || defId.Contains("tapa")) return MetalPainted;
            if (defId.Contains("rueda") || defId.Contains("goma")) return RubberBlack;
            if (defId.Contains("madera") || defId.Contains("manija")) return WoodPlanks;
            return null;
        }

        /// <summary>Libera la caché (cambio de escena / shutdown).</summary>
        public static void Reset()
        {
            _shared = null;
            _tinted = null;
            _probed = false;
        }
    }
}
