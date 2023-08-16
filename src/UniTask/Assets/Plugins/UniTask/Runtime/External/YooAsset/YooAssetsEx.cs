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

        public static async UniTask InitializeAsync(string packageName, EPlayMode playMode)
        {
            YooAssets.Initialize();
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
                    EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(EditorSimulateModeHelper.SimulateBuild(EDefaultBuildPipeline.ScriptableBuildPipeline, packageName)),
                },

                EPlayMode.OfflinePlayMode => new OfflinePlayModeParameters
                {
                    BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(),
                },
                EPlayMode.HostPlayMode => throw new NotImplementedException(),
                EPlayMode.WebPlayMode => throw new NotImplementedException(),
                _ => throw new ArgumentOutOfRangeException(nameof(playMode), playMode, null)
            };

            InitializationOperation initializationOperation = package.InitializeAsync(parameters);
            await initializationOperation.ToUniTask();
            if (initializationOperation.Status != EOperationStatus.Succeed)
            {
                throw new Exception("Failed to initialize asset package!");
            }
        }

        public static async UniTask<string> RequestPackageVersionAsync(string packageName)
        {
            var package = YooAssets.GetPackage(packageName);
            RequestPackageVersionOperation requestPackageVersionOperation = package.RequestPackageVersionAsync();
            await requestPackageVersionOperation.ToUniTask();
            if (requestPackageVersionOperation.Status != EOperationStatus.Succeed)
            {
                throw new Exception("Failed to request asset package version!");
            }

            return requestPackageVersionOperation.PackageVersion;
        }

        public static async UniTask UpdatePackageManifestAsync(string packageName, string packageVersion)
        {
            var package = YooAssets.GetPackage(packageName);
            UpdatePackageManifestOperation updatePackageManifestOperation = package.UpdatePackageManifestAsync(packageVersion);
            await updatePackageManifestOperation.ToUniTask();
            if (updatePackageManifestOperation.Status != EOperationStatus.Succeed)
            {
                throw new Exception($"[YooAssetsEx] Failed to update asset package manifest with version {packageVersion}!");
            }
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
