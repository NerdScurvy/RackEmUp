using CarryOn.API.Common;
using Vintagestory.API.Common;

[assembly: ModInfo("CarryOn RackEmUp",
    modID: "rackemup",
    Version = "1.0.0",
    Description = "Adds the capability to transfer carryable molds to and from the mold rack",
    Website = "https://github.com/NerdScurvy/RackEmUp",
    Authors = new[] { "NerdScurvy" })]
[assembly: ModDependency("game", "1.21.0-rc.6")]

namespace CarryOn.RackEmUp
{
    /// <summary> Main system for the "Carry On" mod, which allows certain
    ///           blocks such as chests to be picked up and carried around. </summary>
    public class Core : ModSystem
    { 

        public ICarryManager CarryManager { get; set; }
        
        public override void Start(ICoreAPI api)
        {
            var carryOnLib = api.ModLoader.GetModSystem<CarryOnLib.Core>();
            CarryManager = carryOnLib.CarryManager;

            api.RegisterBlockBehaviorClass(BlockBehaviorMoldRackTransfer.Name, typeof(BlockBehaviorMoldRackTransfer));

        }
    }
}