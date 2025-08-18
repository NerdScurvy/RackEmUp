using Vintagestory.API.Common;

[assembly: ModInfo("Carry On Mold Rack Transfer",
    modID: "carryonmoldracktransfer",
    Version = "1.0.0",
    Description = "Adds the capability to transfer molds to and from the mold rack",
    Website = "https://github.com/NerdScurvy/CarryOnMoldRackTransfer",
    Authors = new[] { "NerdScurvy" })]
[assembly: ModDependency("game", "1.21.0-rc.4")]

namespace CarryOn.MoldRackTransfer
{
    /// <summary> Main system for the "Carry On" mod, which allows certain
    ///           blocks such as chests to be picked up and carried around. </summary>
    public class Core : ModSystem
    { 
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockBehaviorClass(BlockBehaviorMoldRackTransfer.Name, typeof(BlockBehaviorMoldRackTransfer));

        }
    }
}