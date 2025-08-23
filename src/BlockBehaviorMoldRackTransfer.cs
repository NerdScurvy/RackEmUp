using System.Linq;
using CarryOn.API.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;
using static CarryOn.API.Common.CarryCode;
using static CarryOn.Utility.JsonHelper;

namespace CarryOn.RackEmUp
{
    public class BlockBehaviorMoldRackTransfer : BlockBehavior, ICarryableTransfer
    {

        public static string Name { get; } = "MoldRackTransfer";

        public BlockBehaviorMoldRackTransfer(Block block) : base(block)
        {
        }

        public CarryOnLib.Core CarryOnLib { get; set; }

        // Could implement a per-slot index delay
        private float? TransferDelay { get; set; }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            if (TryGetFloat(properties, "transferDelay", out var t)) TransferDelay = t;

        }

        public override void OnLoaded(ICoreAPI api)
        {
            CarryOnLib = api.ModLoader.GetModSystem<CarryOnLib.Core>();

            base.OnLoaded(api);
        }

        public bool IsTransferEnabled(ICoreAPI api)
        {
            return true;

            //return api?.World?.Config?.GetBool("carryon:TransferMoldRackEnabled") ?? false;
        }

        /// <summary>
        /// Checks if an item can be put into the mold rack.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="blockEntity"></param>
        /// <param name="index">Index of the slot in the rack</param>
        /// <param name="itemStack">Item stack to put into the mold rack</param>
        /// <param name="blockEntityData">Block Entity Data for the carried itemStack</param>
        /// <param name="failureCode">
        ///     Used to define error codes or control codes for the interaction.
        ///         __stop__ - Stop all further CarryOn interactions and default handling
        ///         __default__ - Stop all further CarryOn interactions and continue with default handling
        /// </param>
        /// <param name="onScreenErrorMessage"></param>
        /// <returns>
        ///     True if the item can be put into the mold rack. Interaction Spinner will start and default handling will be prevented.
        ///     False if not. CarryOn will display an error message if OnScreenErrorMessage is set. 
        ///         Error message will stop further processing similar to the __stop__ code.
        ///         CarryOn can be directed to stop all further interactions if failureCode is set to "__stop__". 
        ///         Otherwise will continue checking CarryOn interactions.
        /// </returns>
        public bool CanPutCarryable(IPlayer player, BlockEntity blockEntity, int index, ItemStack itemStack, ITreeAttribute blockEntityData, out float? transferDelay, out string failureCode, out string onScreenErrorMessage)
        {
            failureCode = null;
            onScreenErrorMessage = null;
            transferDelay = TransferDelay;

            var moldRack = blockEntity as BlockEntityMoldRack;
            if (moldRack == null || index < 0 || index >= moldRack.Inventory.Count)
            {
                // Invalid slot - tell caller to continue to next interaction
                return false;
            }

            var world = player.Entity.Api.World;

            var blockName = block.GetPlacedBlockName(world, blockEntity.Pos);

            if (moldRack.Inventory[index]?.Empty == false)
            {
                failureCode = FailureCode.Stop;
                onScreenErrorMessage = Lang.Get("carryon:mold-rack-transfer-occupied", blockName);
                return false;
            }

            // Check if the itemStack is mold rack compatible
            var moldRackable = itemStack?.Collectible?.Attributes?["moldrackable"]?.AsBool() ?? false;
            if (!moldRackable)
            {
                failureCode = "mold-rack-transfer-incompatible";
                onScreenErrorMessage = Lang.Get($"carryon:{failureCode}", blockName);
                return false;
            }

            // Only server has blockEntityData
            if (world.Side == EnumAppSide.Server && blockEntityData != null)
            {

                if (blockEntityData.GetAsBool("shattered", false))
                {
                    failureCode = "mold-rack-transfer-shattered";
                    onScreenErrorMessage = Lang.Get($"carryon:{failureCode}", blockName);
                    return false;
                }


                if (blockEntityData.GetAsInt("fillLevel", -1) > 0)
                {
                    failureCode = "mold-rack-transfer-nonempty";
                    onScreenErrorMessage = Lang.Get($"carryon:{failureCode}", blockName);
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if an item can be taken from the mold rack.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="blockEntity"></param>
        /// <param name="index"></param>
        /// <param name="failureCode"></param>
        /// <param name="onScreenErrorMessage"></param>
        /// <returns>
        ///     True if the item can be taken from the mold rack. Interaction Spinner will start and default handling will be prevented.
        ///     False if not. CarryOn will display an error message if OnScreenErrorMessage is set.
        ///         Error message will stop further processing similar to the __stop__ code.
        ///         CarryOn can be directed to stop all further interactions if failureCode is set to "__stop__". 
        ///         Otherwise will continue checking CarryOn interactions.
        /// </returns>
        public bool CanTakeCarryable(IPlayer player, BlockEntity blockEntity, int index, out float? transferDelay, out string failureCode, out string onScreenErrorMessage)
        {
            failureCode = null;
            onScreenErrorMessage = null;
            transferDelay = TransferDelay;

            // Ensure correct type
            var moldRack = blockEntity as BlockEntityMoldRack;

            // Check if the moldRack is valid and the slot is not empty
            if (moldRack == null || index < 0 || index >= moldRack.Inventory.Count)
            {
                // target slot is invalid
                return false;
            }

            var sourceSlot = moldRack?.Inventory?[index];
            if (sourceSlot?.Empty == true)
            {
                // Slot is empty - tell the caller to continue to the next interaction (pickup the rack if carryable)
                return false;
            }

            if (!HasBehavior(sourceSlot.Itemstack.Block, "BlockBehaviorCarryable"))
            {
                // Item in slot is not carryable - skip further CarryOn interactions and allow default handling
                // If the item in the slot is a shield then pick it up normally
                failureCode = FailureCode.Default;

                return false;
            }
            return true;
        }


        /// <summary>
        /// Try to put carried item into the mold rack.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="blockEntity"></param>
        /// <param name="index"></param>
        /// <param name="itemstack"></param>
        /// <param name="blockEntityData"></param>
        /// <param name="failureCode"></param>
        /// <param name="onScreenErrorMessage"></param>
        /// <returns></returns>
        public bool TryPutCarryable(IPlayer player, BlockEntity blockEntity, int index, ItemStack itemstack, ITreeAttribute blockEntityData, out string failureCode, out string onScreenErrorMessage)
        {

            if (!CanPutCarryable(player, blockEntity, index, itemstack, blockEntityData, out _, out failureCode, out onScreenErrorMessage))
            {
                return false;
            }

            if (player.Entity.Api.Side == EnumAppSide.Client)
            {
                // Prevent transfer on client side but tell to continue server side
                failureCode = FailureCode.Continue;
                return false;
            }

            var world = player.Entity.Api.World;
            var moldRack = blockEntity as BlockEntityMoldRack;

            // Place the itemStack into the slot
            var sinkSlot = moldRack.Inventory[index];
            sinkSlot.Itemstack = itemstack.Clone();

            sinkSlot.MarkDirty();
            moldRack.MarkDirty(true);
            world.PlaySoundAt(new AssetLocation("sounds/player/build"), player);
            AssetLocation code = itemstack?.Collectible.Code;
            world.Logger.Audit($"{player.PlayerName} Put 1x{code} into Rack at {blockEntity.Pos}.");
            return true;
        }

        /// <summary>
        /// Try to take item from the mold rack to be carried.
        /// </summary>
        /// <param name="player"></param>
        /// <param name="blockEntity"></param>
        /// <param name="index"></param>
        /// <param name="itemstack"></param>
        /// <param name="blockEntityData"></param>
        /// <param name="failureCode"></param>
        /// <param name="onScreenErrorMessage"></param>
        /// <returns></returns>
        public bool TryTakeCarryable(IPlayer player, BlockEntity blockEntity, int index, out ItemStack itemstack, out ITreeAttribute blockEntityData, out string failureCode, out string onScreenErrorMessage)
        {
            itemstack = null;
            blockEntityData = null;

            if (!CanTakeCarryable(player, blockEntity, index, out _, out failureCode, out onScreenErrorMessage))
            {
                return false;
            }

            if (player.Entity.Api.Side == EnumAppSide.Client)
            {
                // Prevent transfer on client side but tell to continue server side
                failureCode = FailureCode.Continue;
                return false;
            }

            var world = player.Entity.Api.World;
            var moldRack = blockEntity as BlockEntityMoldRack;
            var sourceSlot = moldRack.Inventory[index];
            // Clone the itemStack to return
            itemstack = sourceSlot.Itemstack.Clone();

            // Remove the item from the inventory (replicating TryTake core logic)
            sourceSlot.Itemstack = null;
            sourceSlot.MarkDirty();
            moldRack.MarkDirty(true);
            world.PlaySoundAt(new AssetLocation("sounds/player/build"), player);
            AssetLocation code = itemstack?.Collectible.Code;
            world.Logger.Audit($"{player.PlayerName} Took 1x{code} from Rack at {blockEntity.Pos}.");
            return true;
        }

        private BlockEntityMoldRack GetMoldRackBlockEntity(IWorldAccessor world, BlockPos pos)
        {
            return world.BlockAccessor.GetBlockEntity(pos) as BlockEntityMoldRack;
        }

        /// <summary>
        /// Determines whether the block can be carried by the player.
        /// </summary>
        public bool IsBlockCarryAllowed(IPlayer player, BlockSelection selection)
        {

            var moldRack = GetMoldRackBlockEntity(player.Entity.Api.World, selection.Position);
            if (moldRack == null)
            {
                // Not a mold rack block entity - allow carry by default
                return true;
            }

            if (selection.SelectionBoxIndex < 0 || selection.SelectionBoxIndex >= moldRack.Inventory.Count)
            {
                return true;
            }

            var slot = moldRack.Inventory[selection.SelectionBoxIndex];
            if (slot.Itemstack == null)
            {
                // Slot is empty - allow carry 
                return true;
            }

            return false;
        }

        private ItemSlot GetSlotFromMoldRack(BlockEntityMoldRack moldRack, int selectionBoxIndex)
        {
            if (moldRack == null || selectionBoxIndex < 0 || selectionBoxIndex >= moldRack.Inventory.Count)
            {
                return null;
            }
            return moldRack.Inventory[selectionBoxIndex];
        }

        private bool HasBehavior(Block block, string behaviorClassName)
        {
            return block?.BlockBehaviors?.Any(b => b.GetType().Name == behaviorClassName) ?? false;
        }



        public override WorldInteraction[] GetPlacedBlockInteractionHelp(
                    IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handled)
        {

            // TODO: Implement CarryOnLib API to this info
            var carriedInHands = CarryOnLib?.CarryManager?.GetCarried(forPlayer?.Entity, CarrySlot.Hands);

            var moldRack = GetMoldRackBlockEntity(world, selection.Position);
            var slot = GetSlotFromMoldRack(moldRack, selection.SelectionBoxIndex);

            if (moldRack == null || slot == null)
            {
                return null; // Not a mold rack or invalid selection
            }

            WorldInteraction[] interactions = null;

            if (slot.Empty)
            {
                if (carriedInHands != null)
                {
                    // Slot is empty, allow putting items into it
                    return [
                        new WorldInteraction {
                            ActionLangCode  = CarryOnCode("blockhelp-put"),
                            HotKeyCode      = HotKeyCode.Pickup,
                            MouseButton     = EnumMouseButton.Right,
                            RequireFreeHand = true,
                        }
                    ];
                }
            }
            else if (carriedInHands == null)
            {
                interactions = [
                    new WorldInteraction {
                        ActionLangCode  = CarryOnCode("blockhelp-take"),
                        HotKeyCode      = HotKeyCode.Pickup,
                        MouseButton     = EnumMouseButton.Right,
                        RequireFreeHand = true,
                    }
                ];
            }

            return interactions;
        }
    }
}