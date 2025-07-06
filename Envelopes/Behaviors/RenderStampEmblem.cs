using System;
using System.Collections.Generic;
using System.Linq;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Envelopes.Behaviors;

public class RenderStampEmblem : CollectibleBehavior, IContainedMeshSource
{
    private ICoreAPI _api;

    private const string MeshRefsCacheKey = "StampRefs";

    private Dictionary<string, MultiTextureMeshRef> _meshRefs;

    private Shape? _baseShape;

    private Shape? BaseShape
    {
        get => _baseShape?.Clone();
        set => _baseShape = value;
    }

    public override void OnLoaded(ICoreAPI api)
    {
        _api = api;
        _meshRefs = ObjectCacheUtil.GetOrCreate(api, MeshRefsCacheKey,
            () => new Dictionary<string, MultiTextureMeshRef>());
        base.OnLoaded(api);
    }

    public void InvalidateMeshCacheKey(ItemStack itemstack)
    {
        var key = GetMeshCacheKey(itemstack);
        if (_meshRefs.TryGetValue(key, out var m))
        {
            Console.WriteLine("Threw away mesh for " + key);
            m.Dispose();
            _meshRefs.Remove(key);
        }
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);

        _meshRefs.Foreach(meshRef => meshRef.Value.Dispose());
        ObjectCacheUtil.Delete(api, MeshRefsCacheKey);
    }

    public RenderStampEmblem(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        var item = slot.Itemstack.Attributes.GetString(StampAttributes.StampDesign, string.Empty);

        if (byEntity.Api is ICoreClientAPI capi)
        {
            capi.ShowChatMessage("Stamp design: " + item.GetHashCode());
        }

        Console.WriteLine(item.GetHashCode());

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
    }


    public MeshData? CreateMesh(ItemStack itemstack)
    {
        if (_api is not ICoreClientAPI capi)
        {
            return null;
        }

        var cacheKey = GetMeshCacheKey(itemstack);
        var shape = GenShape(capi, itemstack);

        var tps = new ShapeTextureSource(capi, shape, "stampssealsource");
        capi.Tesselator.TesselateShape(cacheKey, shape, out var meshdata, tps);

        return meshdata;
    }

    private Shape GenShape(ICoreClientAPI api, ItemStack stack)
    {
        if (BaseShape == null)
        {
            var shape = api.TesselatorManager.GetCachedShape(stack.Item.Shape.Base);
            BaseShape = shape.Clone();
        }

        if (stack.Item.Code.Path.EndsWith("blank"))
        {
            Console.WriteLine("blank one found");
            return BaseShape;
        }

        var design = ParseDesign(stack);
        if (design.Length == 0) return BaseShape;


        var shapeClone = BaseShape;
        var stamp = shapeClone.GetElementByName("Stamp");
        if (stamp == null) return shapeClone;

        for (var i = 0; i < stamp.Children.Length; i++)
        {
            if (design[i])
            {
                foreach (var face in stamp.Children[i].FacesResolved)
                    face.Texture = "empty";
            }
        }

        return shapeClone;
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        var key = GetMeshCacheKey(itemstack);

        if (!_meshRefs.TryGetValue(key, out var meshref))
        {
            var id = itemstack.Attributes.GetLong(StampAttributes.StampId);
            Console.WriteLine("need to gen one for " + id + " will have key " + key);

            var mesh = CreateMesh(itemstack);
            meshref = capi.Render.UploadMultiTextureMesh(mesh);
            _meshRefs[key] = meshref;
        }

        renderinfo.ModelRef = meshref;
        renderinfo.NormalShaded = true;

        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
    }

    private static bool[] ParseDesign(ItemStack stack)
    {
        var designString = stack.Attributes.GetString(StampAttributes.StampDesign);
        if (string.IsNullOrEmpty(designString)) return Array.Empty<bool>();

        try
        {
            return designString.ToCharArray()
                .Select(x => x == '1')
                .ToArray();
        }
        catch
        {
            return Array.Empty<bool>();
        }
    }

    public MeshData? GenMesh(ItemStack itemstack, ITextureAtlasAPI targetAtlas, BlockPos atBlockPos)
    {
        return CreateMesh(itemstack);
    }

    public string GetMeshCacheKey(ItemStack itemstack)
    {
        // if (itemstack.Collectible.Code.Path.Contains("sealstamp-engraved") &&
        //     _meshCacheKey != null && _meshCacheKey.EndsWith("0"))
        // {
        //     Console.WriteLine("found engraved with 0");
        //
        //     var id = itemstack.Attributes.GetLong(StampAttributes.StampId);
        //     _meshCacheKey = $"{itemstack.Collectible.Code.ToShortString()}-{id}";
        //     Console.WriteLine(_meshCacheKey);
        // }
        //
        // if (string.IsNullOrEmpty(_meshCacheKey))
        // {
        //     var name = itemstack.Attributes.GetString(StampAttributes.StampTitle);
        //     Console.WriteLine($"no key yet for {name}, id {id}");
        //
        //     _meshCacheKey = $"{itemstack.Collectible.Code.ToShortString()}-{id}";
        //     Console.WriteLine(_meshCacheKey);
        // }

        if (itemstack.Collectible.Code.Path.EndsWith("blank"))
        {
            return $"{itemstack.Collectible.Code.ToShortString()}";
        }

        var stampDesign = itemstack.Attributes.GetString(StampAttributes.StampDesign, string.Empty);
        if (string.IsNullOrEmpty(stampDesign))
        {
            Console.WriteLine(itemstack.Attributes.ToJsonToken() + " has no stamp design");
        }

        return $"{itemstack.Collectible.Code.ToShortString()}-{stampDesign}";
    }
}