using System;
using System.Collections.Generic;
using System.Linq;
using Envelopes.Gui;
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

    private static Dictionary<string, MultiTextureMeshRef> _meshRefs;

    public override void OnLoaded(ICoreAPI api)
    {
        _api = api;
        _meshRefs = ObjectCacheUtil.GetOrCreate(api, MeshRefsCacheKey,
            () => new Dictionary<string, MultiTextureMeshRef>());
        base.OnLoaded(api);
    }

    public static void InvalidateMeshCacheKey(ItemStack itemstack)
    {
        var key = GetMeshCacheKeyFor(itemstack);
        if (_meshRefs.TryGetValue(key, out var m))
        {
            Console.WriteLine("Envelopes: Threw away mesh for " + key);
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
        var shapeloc = stack.Item.Shape.Base.WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
        var shape = api.Assets.TryGet(shapeloc)?.ToObject<Shape>();

        if (shape == null)
        {
            api.Logger.Error("Could not find shape for stamp {0} at {1}", stack.Item.Code, shapeloc);
            return api.TesselatorManager.GetCachedShape(stack.Item.Shape.Base).Clone();
        }

        var design = ParseDesign(stack);

        var stamp = shape.GetElementByName("Stamp");
        if (stamp == null) return shape;

        var metal = new ShapeElementFace { Texture = "metal", Uv = new[] { 0f, 0f, 0.5f, 0.5f } };

        var array = new ShapeElementFace[6];
        array[0] = metal;
        array[1] = metal;
        array[2] = metal;
        array[3] = metal;
        // array[4] = metal; no need to render up face
        array[5] = metal;
        var list = new List<ShapeElement>();

        const double cellSize = 0.25;
        const double yBottom = -0.25;
        const double yTop = 0.00;

        for (var row = 0; row < Constants.GridDimensions; row++)
        {
            for (var col = 0; col < Constants.GridDimensions; col++)
            {
                var idx = row * Constants.GridDimensions + col;
                if (design.Length == 0 || !design[idx])
                {
                    var shapeElement = new ShapeElement
                    {
                        Name = "StampFace" + idx,
                        From = new[] { row * cellSize, yBottom, col * cellSize },
                        To = new[] { row * cellSize + cellSize, yTop, col * cellSize + cellSize },
                        FacesResolved = array
                    };
                    list.Add(shapeElement);
                }
            }
        }

        stamp.Children = list.ToArray();

        return shape;
    }

    public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        var key = GetMeshCacheKey(itemstack);

        if (!_meshRefs.TryGetValue(key, out var meshref))
        {
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
        return GetMeshCacheKeyFor(itemstack);
    }

    public static string GetMeshCacheKeyFor(ItemStack itemstack)
    {
        if (itemstack.Collectible.Code.Path.EndsWith("blank"))
        {
            return $"{itemstack.Collectible.Code.ToShortString()}";
        }

        var stampId = itemstack.Attributes.GetLong(StampAttributes.StampId);
        if (stampId == 0L)
        {
            return $"{itemstack.Collectible.Code.ToShortString()}";
        }

        return $"{itemstack.Collectible.Code.ToShortString()}-{stampId}";
    }
}