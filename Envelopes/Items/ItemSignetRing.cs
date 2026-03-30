using System;
using System.Collections.Generic;
using System.Linq;
using Envelopes.Behaviors;
using Envelopes.Gui;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Util;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace Envelopes.Items;

public class ItemSignetRing : ItemWaxSealStamp, IAttachableToEntity, IWearableShapeSupplier
{
    protected override bool ShouldOpenEditor => false;

    private static readonly AssetLocation EntityShapeLoc = new AssetLocation("envelopes:entity/humanoid/seraph/clothing/arm/signetring");

    int IAttachableToEntity.RequiresBehindSlots { get; set; }

    bool IAttachableToEntity.IsAttachable(Entity toEntity, ItemStack itemStack) => true;

    string IAttachableToEntity.GetCategoryCode(ItemStack stack) => "arm";

    CompositeShape IAttachableToEntity.GetAttachedShape(ItemStack stack, string slotCode) =>
        new CompositeShape { Base = EntityShapeLoc };

    string[] IAttachableToEntity.GetDisableElements(ItemStack stack) => null;

    string[] IAttachableToEntity.GetKeepElements(ItemStack stack) => null;

    string IAttachableToEntity.GetTexturePrefixCode(ItemStack stack) => null;

    void IAttachableToEntity.CollectTextures(ItemStack stack, Shape shape, string texturePrefixCode,
        Dictionary<string, CompositeTexture> intoDict)
    {
        var metal = stack.Collectible.Variant?["metal"];
        if (metal != null)
            shape.Textures["metal"] = new AssetLocation($"game:block/metal/ingot/{metal}");
    }

    Shape IWearableShapeSupplier.GetShape(ItemStack stack, Entity forEntity, string texturePrefixCode)
    {
        var shapeLoc = EntityShapeLoc.CopyWithPathPrefixAndAppendixOnce("shapes/", ".json");
        var shape = forEntity.World.Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();
        if (shape == null) return null;

        shape.ResolveReferences(forEntity.World.Logger, EntityShapeLoc.ToString());

        var stamp = shape.GetElementByName("Stamp");
        if (stamp == null) return shape;

        var designString = stack.Attributes.GetString(StampAttributes.StampDesign);
        var design = string.IsNullOrEmpty(designString)
            ? Array.Empty<bool>()
            : designString.ToCharArray().Select(c => c == '1').ToArray();

        var cellSizeX = (stamp.To[0] - stamp.From[0]) / Constants.GridDimensions;
        var cellSizeZ = (stamp.To[2] - stamp.From[2]) / Constants.GridDimensions;
        var cellUvSize = 16f / Constants.GridDimensions;
        var stampHeight = stamp.To[1] - stamp.From[1];
        var yBottom = stampHeight;
        var yTop = yBottom + cellSizeX;

        var cells = new List<ShapeElement>();
        for (var row = 0; row < Constants.GridDimensions; row++)
        {
            for (var col = 0; col < Constants.GridDimensions; col++)
            {
                var idx = row * Constants.GridDimensions + col;
                if (design.Length == 0 || !design[idx])
                {
                    var uv = new[] { col * cellUvSize, row * cellUvSize, (col + 1) * cellUvSize, (row + 1) * cellUvSize };
                    var face = new ShapeElementFace { Texture = "metal", Uv = uv };
                    var faces = new ShapeElementFace[6];
                    faces[0] = face; faces[1] = face; faces[2] = face; faces[3] = face;
                    faces[4] = face;
                    cells.Add(new ShapeElement
                    {
                        Name = "SF" + idx,
                        From = new[] { row * cellSizeX, yBottom, col * cellSizeZ },
                        To = new[] { (row + 1) * cellSizeX, yTop, (col + 1) * cellSizeZ },
                        FacesResolved = faces
                    });
                }
            }
        }

        stamp.Children = cells.ToArray();
        return shape;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (firstEvent && byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Code?.Path?.StartsWith("waxstick") == true
                       && blockSel != null && GetUnsealedContainerSlot(blockSel) != null)
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        if (handling == EnumHandHandling.PreventDefault) return;

        if (!byEntity.Controls.ShiftKey && firstEvent)
        {
            var player = (byEntity as EntityPlayer)?.Player;
            var inv = player?.InventoryManager.GetOwnInventory("character");
            if (inv?[(int)EnumCharacterDressType.Arm].TryFlipWith(slot) == true)
            {
                handling = EnumHandHandling.PreventDefault;
                return;
            }
        }
    }

    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.LeftHandItemSlot?.Itemstack?.Collectible?.Code?.Path?.StartsWith("waxstick") != true) return false;
        if (blockSel == null) return false;
        return GetUnsealedContainerSlot(blockSel) != null && secondsUsed < 0.5f;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var waxSticks = api.World.Items
            .Where(i => i.Code?.Domain == "envelopes" && i.Code.Path.StartsWith("waxstick"))
            .Select(i => new ItemStack(i))
            .ToArray();

        return new[]
        {
            new WorldInteraction
            {
                ActionLangCode = $"{EnvelopesModSystem.ModId}:heldhelp-sealinworld",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = waxSticks
            }
        }.Append(base.GetHeldInteractionHelp(inSlot));
    }

    public override bool ConsumeCraftingIngredients(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe)
    {
        foreach (var slot in slots)
        {
            if (slot.Itemstack?.Collectible is ItemWaxSealStamp and not ItemSignetRing
                && slot.Itemstack.Collectible.Code.Path.Contains("engraved"))
            {
                var attrs = slot.Itemstack.Attributes;
                var stampId = attrs.TryGetLong(StampAttributes.StampId);
                if (stampId.HasValue)
                    outputSlot.Itemstack.Attributes.SetLong(StampAttributes.StampId, stampId.Value);
                var title = attrs.GetString(StampAttributes.StampTitle);
                if (title != null)
                    outputSlot.Itemstack.Attributes.SetString(StampAttributes.StampTitle, title);
                var design = attrs.GetString(StampAttributes.StampDesign);
                if (design != null)
                    outputSlot.Itemstack.Attributes.SetString(StampAttributes.StampDesign, design);
                break;
            }
        }
        return false;
    }
}
