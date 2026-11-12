// [ETSV] Integración — carga de modelos 3D reutilizables (Instrucción 11).
// Capa de presentación pura.
//
// Los modelos CC0 (Kenney City Kit Industrial, Kenney Furniture Kit,
// Quaternius Modular Sci-Fi MegaKit — ver Resources/ETSV/Meshes/_SOURCE.md)
// se cargan por Resources.LoadAll<Mesh> sobre el FBX. El mesh se aplica en un
// GameObject NUEVO con MeshFilter+MeshRenderer y el material del set de la
// Instrucción 10 (ETSVArt) — NO se usan los materiales/texturas originales de
// los packs (regla §7.1 de la biblia: un solo atlas de paleta).
//
// REGLAS DURAS:
//  - Un modelo NUNCA trae collider al escenario (addColliders: 0 en el
//    importer + no se agrega acá): los colliders funcionales siguen siendo los
//    existentes (raíz de estación, componentes del blueprint, piezas).
//  - Sin modelos disponibles (banco headless, Resources vacíos) TryCreate
//    devuelve false y el llamador usa su geometría primitiva de siempre:
//    la jugabilidad jamás depende del arte.
//  - Escalas medidas sobre los OBJ reales de cada pack (ver _SOURCE.md y
//    unity/docs/MODELOS_3D.md): City Kit ≈ 7 m/unidad; Furniture y MegaKit 1:1.

using UnityEngine;

namespace Etsv.Integration
{
    public static class ETSVModels
    {
        public const string KenneyCity = "KenneyCity";
        public const string KenneyFurniture = "KenneyFurniture";
        public const string Quaternius = "Quaternius";

        /// <summary>Escala del City Kit Industrial (medida sobre los OBJ: un
        /// shipping-container de 0.823 unidades → 5.76 m reales ⇒ ≈7).</summary>
        public const float KenneyCityScale = 7f;

        private const string Folder = "ETSV/Meshes/";
        private static System.Collections.Generic.Dictionary<string, Mesh[]> _cache;

        /// <summary>Todos los meshes del FBX (los packs traen modelos multi-mesh, p.ej.
        /// kitchenMicrowave), o array vacío si el recurso no existe.</summary>
        public static Mesh[] LoadAll(string packFolder, string modelName)
        {
            string key = packFolder + "/" + modelName;
            if (_cache == null)
                _cache = new System.Collections.Generic.Dictionary<string, Mesh[]>();
            Mesh[] m;
            if (_cache.TryGetValue(key, out m)) return m;
            var all = Resources.LoadAll<Mesh>(Folder + key);
            m = all ?? new Mesh[0]; // vacío también se cachea
            _cache[key] = m;
            return m;
        }

        /// <summary>Primer mesh del FBX, o null (utilidad/compatibilidad).</summary>
        public static Mesh Load(string packFolder, string modelName)
        {
            var all = LoadAll(packFolder, modelName);
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>
        /// Crea un GameObject visual con el modelo COMPLETO (un hijo por mesh del
        /// FBX, un solo material compartido). Devuelve null si el modelo no está
        /// disponible (el llamador debe tener fallback primitivo).
        /// SIN collider, SIN NetworkBehaviour: decoración/visual puro.
        /// </summary>
        public static GameObject TryCreate(string packFolder, string modelName, string goName,
            Transform parent, Vector3 localPos, float scale, float yawDeg, Material mat)
        {
            var meshes = LoadAll(packFolder, modelName);
            if (meshes.Length == 0) return null;
            var go = new GameObject(goName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(scale, scale, scale);
            if (yawDeg != 0f) go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            for (int i = 0; i < meshes.Length; i++)
            {
                // Mesh único ⇒ todo en la raíz; multi-mesh ⇒ un hijo por mesh.
                var holder = meshes.Length == 1 ? go : new GameObject(goName + "_M" + i);
                if (holder != go) holder.transform.SetParent(go.transform, false);
                var mf = holder.AddComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = meshes[i];
                var mr = holder.AddComponent<MeshRenderer>();
                ETSVArt.Apply(mr, mat);
            }
            return go;
        }

        public static void Reset() { _cache = null; }
    }
}
