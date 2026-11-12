// [ETSV] Capa de integración — adaptador MÍNIMO entre Unity (futuro host
// autoritativo) y el dominio puro. No usa UnityEngine: es la costura donde más
// adelante se conectarán los ServerRpc/NetworkVariables de NGO (el host ejecuta
// las operaciones y sincroniza resultados). Hoy solo expone el estado agregado
// y el contexto de economía, iguales al ctx del prototipo JS.

using System;
using Etsv.Domain;

/// <summary>
/// Punto de acceso único al dominio desde la integración.
/// Ciclo de vida previsto: NewGame()/Load() al crear la sesión del host;
/// SaveToDto()/SaveToJson() al persistir; las ops se ejecutan con Run().
/// </summary>
public static class DomainHost
{
    public static GameStateData State { get; private set; }
    public static EconomyContext Ctx { get; private set; }

    /// <summary>Disparado tras cada operación exitosa (UI, autoguardado, sync futura).</summary>
    public static event Action Changed;

    public static void NewGame()
    {
        State = SaveSerializer.CreateGameState();
        RebuildContext();
    }

    /// <summary>Restaura desde un DTO ya validado; devuelve false si la versión no coincide.</summary>
    public static bool Load(SaveGame save)
    {
        var problems = SaveValidator.Validate(save);
        if (problems.Count > 0) return false;
        var gs = SaveSerializer.Deserialize(save);
        if (gs == null) return false;
        State = gs;
        RebuildContext();
        return true;
    }

    public static SaveGame SaveToDto()
    {
        return SaveSerializer.Serialize(State);
    }

    /// <summary>Ejecuta una operación atómica del dominio y notifica si tuvo éxito.</summary>
    public static OpResult Run(Func<EconomyContext, OpResult> op)
    {
        var r = op(Ctx);
        if (r.Ok && Changed != null) Changed();
        return r;
    }

    private static void RebuildContext()
    {
        Ctx = new EconomyContext
        {
            Reg = State.Reg,
            Cwh = State.Cwh,
            Mwh = State.Mwh,
            Carry = null,
            GetMoney = () => State.Money,
            AddMoney = n => { State.Money += n; },
            OnChanged = () => { if (Changed != null) Changed(); },
        };
    }
}
