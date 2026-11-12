// [ETSV] Integración — constructor GENÉRICO de visuales desde blueprints.
// Un solo builder para los 30 objetos del GDD: lee ObjectBlueprint + tabla de
// layout (o layout genérico en rejilla) y crea GameObjects primitivos por
// componente y por fijación. Sin prefabs por objeto, sin controladores por
// objeto. Solo presentación: no toca el dominio.

using System.Collections.Generic;
using Etsv.Domain;
using UnityEngine;

namespace Etsv.Integration
{
    /// <summary>Handles de los GameObjects construidos, indexados como el blueprint.</summary>
    public class ObjectVisuals
    {
        public GameObject[] ComponentGOs;
        public GameObject[][] FastenerGOs; // [compIdx][fIdx]; puede ser vacío
        public Renderer[] ComponentRenderers;
        public Renderer[][] FastenerRenderers;
        public Color[] BaseColors;
    }

    public static class BlueprintBuilder
    {
        private const string UrpLit = "Universal Render Pipeline/Lit";
        private const string FallbackShader = "Standard";

        public static Material MakeMaterial(Color c)
        {
            var shader = Shader.Find(UrpLit);
            if (shader == null) shader = Shader.Find(FallbackShader);
            var m = new Material(shader);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            return m;
        }

        private static GameObject MakePrimitive(string kind, string name, Transform parent,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            PrimitiveType t = kind == "cylinder" ? PrimitiveType.Cylinder
                            : kind == "sphere" ? PrimitiveType.Sphere
                            : PrimitiveType.Cube;
            var go = GameObject.CreatePrimitive(t); // trae su collider
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = MakeMaterial(color);
            return go;
        }

        /// <summary>
        /// Detalles visuales por componente (hijos SIN collider: no interfieren con el
        /// raycast de interacción, que pega contra el primitivo del componente). Solo
        /// se invocan cuando hay material artístico disponible.
        /// </summary>
        private static void AddComponentDetail(GameObject comp, ComponentDef def, CompLayout lay)
        {
            string id = def.Id ?? "";
            if (id.Contains("puerta"))
            {
                // Ventana de vidrio en la puerta (el marco lo aporta el cuerpo plástico).
                var glass = MakePrimitive("cube", "Detalle_Ventana", comp.transform,
                    new Vector3(0f, 0.05f, 0.52f), new Vector3(0.72f, 0.6f, 0.06f), Color.white);
                ETSVArt.Apply(glass.GetComponent<Renderer>(), ETSVArt.Tinted(ETSVArt.GlassClear, Color.white));
                StripCollider(glass);
            }
            else if (id.Contains("panel"))
            {
                // Perilla + botonera del panel de control.
                var knob = MakePrimitive("cylinder", "Detalle_Perilla", comp.transform,
                    new Vector3(0.25f, 0.2f, 0.55f), new Vector3(0.16f, 0.03f, 0.16f), Color.white);
                ETSVArt.Apply(knob.GetComponent<Renderer>(), ETSVArt.Tinted(ETSVArt.PlasticGrey, new Color(0.8f, 0.8f, 0.82f)));
                StripCollider(knob);
                var btn = MakePrimitive("cube", "Detalle_Botones", comp.transform,
                    new Vector3(-0.15f, 0.25f, 0.52f), new Vector3(0.5f, 0.18f, 0.05f), Color.white);
                ETSVArt.Apply(btn.GetComponent<Renderer>(), ETSVArt.Tinted(ETSVArt.SignGreen, new Color(0.5f, 0.7f, 0.55f)));
                StripCollider(btn);
            }
            else if (id.Contains("motor"))
            {
                // Eje visible del motor.
                var shaft = MakePrimitive("cylinder", "Detalle_Eje", comp.transform,
                    new Vector3(0f, 0.55f, 0f), new Vector3(0.18f, 0.12f, 0.18f), Color.white);
                ETSVArt.Apply(shaft.GetComponent<Renderer>(), ETSVArt.Tinted(ETSVArt.SteelRusty, new Color(0.75f, 0.72f, 0.7f)));
                StripCollider(shaft);
            }
            else if (id.Contains("plato"))
            {
                // Aro del plato giratorio.
                var ring = MakePrimitive("cylinder", "Detalle_Aro", comp.transform,
                    new Vector3(0f, 0.6f, 0f), new Vector3(1.02f, 0.25f, 1.02f), Color.white);
                ETSVArt.Apply(ring.GetComponent<Renderer>(), ETSVArt.Tinted(ETSVArt.GlassClear, new Color(0.9f, 0.95f, 0.95f)));
                StripCollider(ring);
            }
        }

        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Object.Destroy(c);
        }

        /// <summary>
        /// Construye el objeto completo bajo `root`. Los colliders de componentes y
        /// fijaciones quedan en el layer de root (Default) para los raycasts de
        /// interacción.
        /// </summary>
        public static ObjectVisuals Build(ObjectBlueprint bp, Transform root)
        {
            var layout = DisassemblyLayouts.For(bp) ?? DisassemblyLayouts.Generic(bp);
            var vis = new ObjectVisuals
            {
                ComponentGOs = new GameObject[bp.Components.Length],
                FastenerGOs = new GameObject[bp.Components.Length][],
                ComponentRenderers = new Renderer[bp.Components.Length],
                FastenerRenderers = new Renderer[bp.Components.Length][],
                BaseColors = new Color[bp.Components.Length],
            };

            for (int ci = 0; ci < bp.Components.Length; ci++)
            {
                var def = bp.Components[ci];
                var lay = layout[ci];
                var go = MakePrimitive(lay.Primitive, "Comp_" + def.Id, root, lay.Pos, lay.Scale, lay.Color);
                go.AddComponent<ComponentRef>().Init(ci);
                vis.ComponentGOs[ci] = go;
                vis.ComponentRenderers[ci] = go.GetComponent<Renderer>();
                vis.BaseColors[ci] = lay.Color;

                // Instrucción 10: material artístico por componente (data-driven, sirve
                // para los 30 objetos del GDD). Sin el asset, queda el color plano de
                // arriba. BaseColors pasa a blanco cuando hay textura: los tintes de
                // estado de ApplyVisuals (bloqueado/liberado) multiplican la textura en
                // vez de ensuciarla. La GEOMETRÍA, los pivotes y las fijaciones NO
                // cambian: el sistema modular del dominio queda intacto.
                string lib = ETSVArt.ComponentMaterialFor(def);
                if (lib != null)
                {
                    ETSVArt.Apply(vis.ComponentRenderers[ci], ETSVArt.Tinted(lib, Color.white));
                    vis.BaseColors[ci] = Color.white;
                    AddComponentDetail(go, def, lay);
                }

                int nf = def.Fasteners != null ? def.Fasteners.Length : 0;
                var fgo = new GameObject[nf];
                var frend = new Renderer[nf];
                for (int fi = 0; fi < nf; fi++)
                {
                    Vector3 off = fi < lay.FastenerOffsets.Length
                        ? lay.FastenerOffsets[fi]
                        : new Vector3(0f, 0.15f, 0f);
                    // Fijación = esfera pequeña en la superficie del componente (hija del
                    // componente: se oculta/mueve con él). Sobresale para que el raycast
                    // del jugador pueda apuntarle.
                    var f = MakePrimitive("sphere", "Fastener_" + ci + "_" + fi, go.transform,
                        off, new Vector3(0.05f, 0.05f, 0.05f), new Color(0.85f, 0.25f, 0.2f));
                    f.AddComponent<FastenerRef>().Init(ci, fi);
                    fgo[fi] = f;
                    frend[fi] = f.GetComponent<Renderer>();
                }
                vis.FastenerGOs[ci] = fgo;
                vis.FastenerRenderers[ci] = frend;
            }
            return vis;
        }
    }
}
