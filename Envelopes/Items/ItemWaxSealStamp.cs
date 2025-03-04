using System;
using System.Collections.Generic;
using System.Text;
using Envelopes.Gui;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Items;

public class ItemWaxSealStamp : Item, IContainedMeshSource
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handling)
    {
        if (api.Side == EnumAppSide.Server)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        if (byEntity.Controls.ShiftKey)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }


        var alreadyEngraved = slot.Itemstack.Collectible.Code.Path.Contains("engraved");
        if (firstEvent && !alreadyEngraved)
        {
            var dialog = new GuiSealStampDesigner(EnvelopesModSystem.Api as ICoreClientAPI);
            dialog.TryOpen(true);
            handling = EnumHandHandling.PreventDefault;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        var heldItemName = base.GetHeldItemName(itemStack);

        var attributes = itemStack.Attributes;
        var stampTitle = attributes.GetString(StampAttributes.StampTitle);
        if (stampTitle == null)
        {
            return heldItemName;
        }

        heldItemName += $" (“{stampTitle}”)";

        return heldItemName;
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        var attributes = inSlot.Itemstack.Attributes;
        var stampId = attributes.TryGetLong(StampAttributes.StampId);
        if (stampId != null)
        {
            dsc.AppendLine($"<font color=\"orange\">ID: {stampId}</font>");
        }
    }

    #region Render

    private Dictionary<string, MultiTextureMeshRef> Meshrefs =>
        ObjectCacheUtil.GetOrCreate(api, "stampmeshrefs",
            () => new Dictionary<string, MultiTextureMeshRef>());

    public MeshData GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
    {
        return GenMesh(itemstack);
    }

    public string GetMeshCacheKey(ItemStack itemstack)
    {
        return $"{Code}-{itemstack.Attributes.TryGetLong(StampAttributes.StampId)}-{Random.Shared.Next()}";
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        var stampId = itemstack.Attributes.TryGetLong(StampAttributes.StampId);

        if (stampId != null)
        {
            var meshCacheKey = GetMeshCacheKey(itemstack);

            if (Meshrefs.TryGetValue(meshCacheKey, out var meshref))
            {
                renderinfo.ModelRef = meshref;
            }
            else
            {
                var meshData = GenMesh(itemstack);
                if (meshData != null)
                {
                    var multiTextureMeshRef = capi.Render.UploadMultiTextureMesh(meshData);
                    renderinfo.ModelRef = Meshrefs[meshCacheKey] = multiTextureMeshRef;
                }
            }
        }

        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
    }


    private MeshData? GenMesh(ItemStack itemstack)
    {
        if (api is not ICoreClientAPI capi)
        {
            return null;
        }

        var stampId = itemstack.Attributes.TryGetLong(StampAttributes.StampId);
        if (stampId == null)
        {
            return null;
        }

        var stamp = EnvelopesModSystem.StampDatabase?.GetStamp(stampId.Value);
        if (stamp == null)
        {
            return null;
        }

        var cachedShape = capi.TesselatorManager.GetCachedShape(Shape.Base);
        var designElement = cachedShape.GetElementByName("Stamp");
        if (designElement == null)
        {
            return null;
        }

        var designPattern = BooleanArrayPacker.UnpackFromByteArray(stamp.Design);

        var designWidth = (int)stamp.Dimensions;
        var designHeight = (int)stamp.Dimensions;

        var commonFace = new ShapeElementFace
        {
            Texture = "steel",
            Uv = new[] { 6f, 6f, 8f, 8f }
        };

        var faces = new ShapeElementFace[6];
        for (var i = 0; i < 5; i++)
        {
            faces[i] = commonFace;
        }

        List<ShapeElement> designElements = new();

        var voxelSize = 0.05;
        var voxelHeight = 0.1;

        for (var y = 0; y < designHeight; y++)
        {
            for (var x = 0; x < designWidth; x++)
            {
                var index = y * designWidth + x;
                if (index < designPattern.Length && designPattern[index])
                {
                    var element = new ShapeElement
                    {
                        Name = $"design-{x}-{y}",
                        From = new[]
                        {
                            0.5 - designWidth * voxelSize / 2 + x * voxelSize,
                            0.0,
                            0.5 - designHeight * voxelSize / 2 + y * voxelSize
                        },
                        To = new[]
                        {
                            0.5 - designWidth * voxelSize / 2 + (x + 1) * voxelSize,
                            voxelHeight,
                            0.5 - designHeight * voxelSize / 2 + (y + 1) * voxelSize
                        },
                        FacesResolved = faces,
                        ParentElement = designElement
                    };

                    designElements.Add(element);
                }
            }
        }

        designElement.Children = designElements.ToArray();

        var texPositionSource = capi.Tesselator.GetTextureSource(this);
        capi.Tesselator.TesselateShape(GetMeshCacheKey(itemstack), cachedShape, out var meshData,
            texPositionSource);

        return meshData;
    }

    #endregion
}