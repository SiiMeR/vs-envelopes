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

    public MeshData? CreateMesh(ItemStack itemstack)
    {
        if (_api is not ICoreClientAPI capi)
        {
            return null;
        }

        var cacheKey = GetMeshCacheKey(itemstack);
        var shape = GenShape(capi, itemstack);

        var tps = new ShapeTextureSource(capi, shape, "stampssealsource");
        capi.Tesselator.TesselateShape(cacheKey, shape, out var meshdata,
            tps);

        return meshdata;
    }

    private Shape GenShape(ICoreClientAPI api, ItemStack stack)
    {
        // var cachedShape = api.TesselatorManager.GetCachedShape(stack.Item.Shape.Base).Clone();

        AssetLocation shapeloc = stack.Item.Shape.Base.WithPathAppendixOnce(".json").WithPathPrefixOnce("shapes/");
        Shape? shape = api.Assets.TryGet(shapeloc)?.ToObject<Shape>();

        if (stack.Item.Code.Path.EndsWith("blank"))
        {
            Console.WriteLine("blank one found");
            return shape;
        }

        var design = ParseDesign(stack);
        if (design.Length == 0) return shape;


        var shapeClone = shape;
        var stamp = shapeClone.GetElementByName("Stamp");
        if (stamp == null) return shapeClone;

        //
        // var list = new List<ShapeElement>();
        // var sef = new ShapeElementFace
        // {
        //     Texture = "metal_dark",
        //     Uv = new float[] { 6f, 6f, 8f, 8f }
        // };
        // var array = new ShapeElementFace[6];
        // array[0] = sef;
        // array[1] = sef;
        // array[2] = sef;
        // array[3] = sef;
        // array[4] = sef;
        //
        // // for (int i = 0; i < Constants.GridDimensions; i++)
        // {
        //     for (int j = 0; j < Constants.GridDimensions; j++)
        //     {
        //         
        //     }
        // }

        for (var i = 0; i < stamp.Children.Length; i++)
        {
            if (design[i])
            {
                // Console.WriteLine(JsonUtil.ToString(stamp.Children[i].From));
                // Console.WriteLine(JsonUtil.ToString(stamp.Children[i].To));
                foreach (var face in stamp.Children[i].FacesResolved)
                    face.Texture = "empty"; // i just for testing
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
        if (itemstack.Collectible.Code.Path.EndsWith("blank"))
        {
            return $"{itemstack.Collectible.Code.ToShortString()}";
        }

        var stampId = itemstack.Attributes.GetLong(StampAttributes.StampId);
        if (stampId == 0L)
        {
            Console.WriteLine(itemstack.GetName() + " has no stamp ID on side " + _api.Side);
            return $"{itemstack.Collectible.Code.ToShortString()}";
        }

        return $"{itemstack.Collectible.Code.ToShortString()}-{stampId}";
    }
}