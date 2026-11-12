// [ETSV] Capa de integración — adaptador JSON del save.
// El dominio produce/consume DTOs puros (SaveGame); la serialización a texto
// vive acá para mantener ETSV.Domain sin dependencias externas.
// Usa Newtonsoft.Json, ya presente en el proyecto (dependencia transitiva de
// los paquetes de UGS en Packages/packages-lock.json: com.unity.nuget.newtonsoft-json).
//
// Flujo previsto (Instrucción 05 §6, sin sistema de guardado paralelo todavía):
//   DomainHost.SaveToDto() → ToJson() → medio de almacenamiento del host (archivo/
//   PlayerPrefs/Nube) y viceversa con FromJson() → SaveValidator → DomainHost.Load().

using Etsv.Domain;
using Newtonsoft.Json;

public static class SaveJsonAdapter
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        // DTOs con campos públicos: configuración por defecto de Newtonsoft los cubre.
        Formatting = Formatting.None,
        MissingMemberHandling = MissingMemberHandling.Ignore, // tolerante a campos viejos
        NullValueHandling = NullValueHandling.Include,
    };

    public static string ToJson(SaveGame save)
    {
        return JsonConvert.SerializeObject(save, Settings);
    }

    /// <summary>Devuelve null si el texto no es un SaveGame válido (JSON roto, etc.).</summary>
    public static SaveGame FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonConvert.DeserializeObject<SaveGame>(json, Settings); }
        catch (JsonException) { return null; }
    }
}
