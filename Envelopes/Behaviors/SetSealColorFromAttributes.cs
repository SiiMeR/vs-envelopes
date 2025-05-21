using System.Collections.Generic;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Behaviors;

public class SetSealColorFromAttributes : CollectibleBehavior, IContainedMeshSource
{
    private ICoreAPI _api;

    private const string MeshRefsCacheKey = "EnvelopeRefs";

    private Dictionary<string, MultiTextureMeshRef?> Meshrefs => ObjectCacheUtil.GetOrCreate(_api,
        MeshRefsCacheKey, () => new Dictionary<string, MultiTextureMeshRef?>());

    private Dictionary<string, CompositeTexture> _textures = new();

    public override void Initialize(JsonObject properties)
    {
        _textures = properties["textures"].AsObject(defaultValue: new Dictionary<string, CompositeTexture>());
        base.Initialize(properties);
    }

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

        if (itemstack.Collectible.Code.Path is not ("envelope-sealed" or "envelope-opened"))
        {
            capi.Tesselator.TesselateItem(itemstack.Item, out var meshData);
            return meshData;
        }

        var shape = GenShape(capi, itemstack);

        var texPositionSource = capi.Tesselator.GetTextureSource(itemstack.Item);
        var cacheKey = GetMeshCacheKey(itemstack);
        capi.Tesselator.TesselateShape(cacheKey, shape, out var meshData2, texPositionSource);

        return meshData2;
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
        RemapShapeElementTextures(shape, "waxPiece", color);

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
        return $"{itemstack.Collectible.Code.ToShortString()}-{color ?? "uncolored"}";
    }
}