using System;
using System.Collections.Generic;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Behaviors;

public class SetSealColorFromAttributes : CollectibleBehavior, IContainedMeshSource
{
    private ICoreAPI _api;

    private const string MeshRefsCacheKey = "EnvelopeRefs";

    public override void OnLoaded(ICoreAPI api)
    {
        _api = api;
        base.OnLoaded(api);
    }


    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);
        var meshRefs =
            ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api,
                MeshRefsCacheKey);
        meshRefs?.Foreach(meshRef => meshRef.Value?.Dispose());
        ObjectCacheUtil.Delete(api, MeshRefsCacheKey);
    }

    public SetSealColorFromAttributes(CollectibleObject collObj) : base(collObj)
    {
    }


    public MeshData? CreateMesh(ItemStack itemstack)
    {
        if (_api is not ICoreClientAPI capi)
        {
            return null;
        }


        var shape = GenShape(capi, itemstack);

        var tps = new ShapeTextureSource(capi, shape, "envelopesealsource");
        var cacheKey = GetMeshCacheKey(itemstack);
        capi.Tesselator.TesselateShape(cacheKey, shape, out var meshdata, tps);

        return meshdata;
    }

    private Shape GenShape(ICoreClientAPI api, ItemStack itemstack)
    {
        var shape = api.TesselatorManager.GetCachedShape(itemstack.Item.Shape.Base).Clone();
        var color = itemstack.Attributes.GetString(EnvelopeAttributes.WaxColor);

        if (string.IsNullOrEmpty(color))
        {
            return shape;
        }

        RemapShapeElementTextures(shape, "wax", color);
        RemapShapeElementTextures(shape, "wax2", color);
        RemapShapeElementTextures(shape, "waxPiece", color);
        RemapShapeElementTextures(shape, "waxPiece2", color);

        return shape;
    }

    public void RemapShapeElementTextures(Shape shape, string elementName, string newTextureName)
    {
        var elementByName = shape.GetElementByName(elementName);
        if (elementByName == null)
        {
            return;
        }

        foreach (var face in elementByName.FacesResolved)
        {
            if (face == null)
            {
                continue;
            }

            face.Texture = newTextureName;
        }
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        var meshRefs =
            ObjectCacheUtil.GetOrCreate(capi, MeshRefsCacheKey, () => new Dictionary<string, MultiTextureMeshRef>());

        var key = GetMeshCacheKey(itemstack);

        if (!meshRefs.TryGetValue(key, out var meshref))
        {
            var mesh = GenMesh(itemstack, capi.ItemTextureAtlas, null);
            meshref = capi.Render.UploadMultiTextureMesh(mesh);
            meshRefs[key] = meshref;
        }

        renderinfo.ModelRef = meshref;
        renderinfo.NormalShaded = true;

        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
    }

    public MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
    {
        return CreateMesh(itemstack);
    }

    public string GetMeshCacheKey(ItemStack itemstack)
    {
        var color = itemstack.Attributes.GetString(EnvelopeAttributes.WaxColor);

        return $"{itemstack.Collectible.Code.ToShortString()}-{color ?? "beeswax-white"}";
    }
}