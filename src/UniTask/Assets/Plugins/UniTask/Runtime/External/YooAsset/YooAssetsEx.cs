namespace YooAsset
{
    using System;
    using System.Collections.Generic;
    using Cysharp.Threading.Tasks;
    using UnityEngine;
    using Object = UnityEngine.Object;

    public static class YooAssetsEx
    {
        private static readonly Dictionary<Object, AssetHandle> Obj2Handles = new();
        private static readonly Dictionary<GameObject, Object> Go2Obj = new();

        public static UniTask InitializeAsync(EPlayMode playMode)
        {
            YooAssets.Initialize();
            const string packageName = "DefaultPackage";
            var package = YooAssets.TryGetPackage(packageName);
            if (package == null)
            {
                package = YooAssets.CreatePackage(packageName);
                YooAssets.SetDefaultPackage(package);
            }

            InitializeParameters parameters = playMode switch
            {
                EPlayMode.EditorSimulateMode => new EditorSimulateModeParameters
                {
                    SimulateManifestFilePath = EditorSimulateModeHelper.SimulateBuild(EDefaultBuildPipeline.BuiltinBuildPipeline.ToString(), packageName),
                },

                EPlayMode.OfflinePlayMode => new OfflinePlayModeParameters(),
                EPlayMode.HostPlayMode => throw new NotImplementedException(),
                EPlayMode.WebPlayMode => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException(nameof(playMode), playMode, null)
            };

            InitializationOperation initializationOperation = package.InitializeAsync(parameters);
            return initializationOperation.ToUniTask();
        }

        public static async UniTask<T> LoadAssetAsync<T>(string location, IProgress<float> progress = null) where T : Object
        {
            var handle = YooAssets.LoadAssetAsync<T>(location);

            await handle.ToUniTask(progress);

            if (!handle.IsValid)
            {
                throw new Exception($"[YooAssetsEx] Failed to load asset: {location}");
            }

            Obj2Handles.TryAdd(handle.AssetObject, handle);

            return handle.AssetObject as T;
        }

        public static void Release(Object obj)
        {
            if (obj is null)
            {
                return;
            }

            Obj2Handles.Remove(obj, out AssetHandle handle);

            handle?.Release();
        }

        public static async UniTask<GameObject> InstantiateAsync(
            string location,
            Transform parentTransform = null,
            bool stayWorldSpace = false,
            IProgress<float> progress = null)
        {
            Object obj = await LoadAssetAsync<Object>(location, progress);

            if (Object.Instantiate(obj, parentTransform, stayWorldSpace) is not GameObject go)
            {
                Release(obj);
                throw new Exception($"[YooAssetsEx] Failed to instantiate asset: {location}");
            }

            Go2Obj.Add(go, obj);

            return go;
        }

        public static void ReleaseInstance(GameObject go)
        {
            if (go is null)
            {
                return;
            }

            Object.Destroy(go);

            Go2Obj.Remove(go, out Object obj);

            Release(obj);
        }
    }
}
