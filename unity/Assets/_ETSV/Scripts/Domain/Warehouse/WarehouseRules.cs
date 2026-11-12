// [ETSV] Dominio puro — ALMACENES: componentes y materiales.
// Port 1:1 de prototype/js/systems/warehouse.js (Instrucción 02 §5,
// GDD 5.0 §8.2/§8.5): capacidades físicas por tipo de espacio y cantidades
// compartidas de materiales con acumulador fraccionario (los residuos
// fraccionarios se conservan entre lotes hasta pisar el entero).

using System.Collections.Generic;

namespace Etsv.Domain
{
    public class ComponentZone
    {
        public StorageZoneDef Def;
        public List<string> Items = new List<string>(); // uids de piezas
    }

    public class ComponentWarehouse
    {
        public Dictionary<string, ComponentZone> Zones = new Dictionary<string, ComponentZone>();
    }

    public class MaterialWarehouse
    {
        // Acumulador float por material: Units = floor(Acc).
        public Dictionary<string, double> Acc = new Dictionary<string, double>();
    }

    public class WarehouseSnapshot
    {
        public Dictionary<string, List<string>> Zones = new Dictionary<string, List<string>>();
        public Dictionary<string, double> Acc = new Dictionary<string, double>();
    }

    public static class WarehouseRules
    {
        // ---------------------------------------------------------------- almacén de componentes
        public static ComponentWarehouse CreateComponentWarehouse()
        {
            var wh = new ComponentWarehouse();
            foreach (var z in EconomyData.StorageZones.Values)
                wh.Zones[z.Id] = new ComponentZone { Def = z };
            return wh;
        }

        public static int ZoneFreeSpace(ComponentWarehouse wh, string zoneId)
        {
            ComponentZone z;
            return wh.Zones.TryGetValue(zoneId, out z) ? z.Def.Capacity - z.Items.Count : 0;
        }

        // ¿Puede esta pieza (clase de peso) entrar en esta zona con espacio disponible?
        public static OpResult CanStorePiece(ComponentWarehouse wh, string zoneId, StoredPart piece)
        {
            ComponentZone z;
            if (!wh.Zones.TryGetValue(zoneId, out z)) return OpResult.Fail("zona_inexistente");
            WeightClassDef wc;
            if (!EconomyData.WeightClasses.TryGetValue(piece.WeightClass, out wc)) return OpResult.Fail("clase_invalida");
            if (System.Array.IndexOf(z.Def.Accepts, piece.WeightClass) < 0)
                return OpResult.Fail("categoria_incompatible");
            if (z.Items.Count >= z.Def.Capacity)
                return OpResult.Fail("zona_llena");
            return OpResult.Success();
        }

        public static void StorePieceUid(ComponentWarehouse wh, string zoneId, string uid)
        {
            wh.Zones[zoneId].Items.Add(uid);
        }

        public static void RemovePieceUid(ComponentWarehouse wh, string zoneId, string uid)
        {
            var items = wh.Zones[zoneId].Items;
            int i = items.IndexOf(uid);
            if (i >= 0) items.RemoveAt(i);
        }

        public static List<string> StoredUids(ComponentWarehouse wh, string zoneId)
        {
            return new List<string>(wh.Zones[zoneId].Items);
        }

        public static List<string> AllStoredUids(ComponentWarehouse wh)
        {
            var all = new List<string>();
            foreach (var z in wh.Zones.Values) all.AddRange(z.Items);
            return all;
        }

        // ---------------------------------------------------------------- almacén de materiales
        public static MaterialWarehouse CreateMaterialWarehouse()
        {
            var mw = new MaterialWarehouse();
            foreach (var id in EconomyData.MatIds) mw.Acc[id] = 0;
            return mw;
        }

        public static int MaterialUnits(MaterialWarehouse mw, string id)
        {
            return (int)System.Math.Floor(mw.Acc[id] + 1e-9);
        }

        public static double MaterialFraction(MaterialWarehouse mw, string id)
        {
            return mw.Acc[id] - System.Math.Floor(mw.Acc[id] + 1e-9);
        }

        // Agrega rendimiento fraccionario; las unidades aparecen al pisar el entero.
        public static OpResult AddMaterialYield(MaterialWarehouse mw, string id, double amount)
        {
            if (!mw.Acc.ContainsKey(id)) return OpResult.Fail("material_inexistente");
            if (amount < 0) return OpResult.Fail("cantidad_negativa");
            double room = EconomyData.MaterialContainerCap - mw.Acc[id];
            double added = System.Math.Max(0, System.Math.Min(amount, room));
            mw.Acc[id] += added;
            return new OpResult
            {
                Ok = true,
                Added = added,
                Restante = MaterialUnits(mw, id),
                Overflow = amount - added,
            };
        }

        // Consumo atómico: solo si hay unidades enteras suficientes.
        public static OpResult ConsumeMaterialUnits(MaterialWarehouse mw, string id, int units)
        {
            if (!mw.Acc.ContainsKey(id)) return OpResult.Fail("material_inexistente");
            if (units <= 0) return OpResult.Fail("cantidad_invalida");
            if (MaterialUnits(mw, id) < units) return OpResult.Fail("insuficiente");
            mw.Acc[id] -= units;
            return new OpResult { Ok = true, Restante = MaterialUnits(mw, id) };
        }

        // ---- serialización ----
        public static WarehouseSnapshot SerializeWarehouses(ComponentWarehouse cwh, MaterialWarehouse mwh)
        {
            var snap = new WarehouseSnapshot();
            foreach (var kv in cwh.Zones) snap.Zones[kv.Key] = new List<string>(kv.Value.Items);
            foreach (var kv in mwh.Acc) snap.Acc[kv.Key] = kv.Value;
            return snap;
        }

        public static void DeserializeInto(ComponentWarehouse cwh, MaterialWarehouse mwh, WarehouseSnapshot snap)
        {
            if (snap == null) return;
            foreach (var kv in snap.Zones)
            {
                ComponentZone z;
                if (cwh.Zones.TryGetValue(kv.Key, out z)) z.Items = new List<string>(kv.Value);
            }
            foreach (var kv in snap.Acc)
                if (mwh.Acc.ContainsKey(kv.Key)) mwh.Acc[kv.Key] = kv.Value;
        }
    }
}
